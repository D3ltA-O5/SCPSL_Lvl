using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.API.Enums;
using MEC;
using PlayerRoles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hints;

namespace SCPSL_Lvl
{
    public class EventHandlers
    {
        private CoroutineHandle _reminderCoroutine;
        private CoroutineHandle _onlineXpCoroutine;

        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug($"[EventHandlers] OnPlayerVerified: {ev.Player?.UserId}");

            if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
                return;

            // "Горячая" база: получаем данные, которые при необходимости
            // подтянутся из холодной базы или создадутся
            var data = Plugin.Instance.PlayerDatabaseManager.GetPlayerData(ev.Player.UserId);

            // Сохраняем оригинальный ник (если не сохранён)
            if (string.IsNullOrEmpty(data.OriginalNickname))
            {
                data.OriginalNickname = ev.Player.Nickname;
                Plugin.Instance.PlayerDatabaseManager.SaveHotDatabase();

                if (debug)
                    Log.Debug($"[EventHandlers] Original nickname saved: {data.OriginalNickname}");
            }

            // Проверяем генерацию заданий
            var today = DateTime.Now.Date;
            if (data.LastTasksGeneratedDate < today || data.CurrentDailyTasks.Count < 3)
            {
                GenerateDailyTasksForPlayer(data);
                data.LastTasksGeneratedDate = today;
                Plugin.Instance.PlayerDatabaseManager.SaveHotDatabase();
            }

            UpdateNickname(ev.Player);
        }

        public void OnPlayerJoined(JoinedEventArgs ev)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[EventHandlers] OnPlayerJoined: {ev.Player?.Nickname}");
        }

        public void OnPlayerSpawning(SpawningEventArgs ev)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[EventHandlers] OnPlayerSpawning: {ev.Player?.Nickname}");
            Timing.CallDelayed(0.5f, () => UpdateNickname(ev.Player));
        }

        /// <summary>
        /// Вызывается, когда игрок вышел (Left).
        /// Синхронизируем изменения в холодную базу, убираем запись из горячей.
        /// </summary>
        public void OnPlayerLeft(LeftEventArgs ev)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug($"[EventHandlers] OnPlayerLeft: {ev.Player?.UserId}");

            if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
                return;

            Plugin.Instance.PlayerDatabaseManager.RemoveFromHotDatabase(ev.Player.UserId);
        }

        public void OnPlayerDied(DiedEventArgs ev)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            if (debug)
                Log.Debug($"[EventHandlers] OnPlayerDied: victim={ev.Player?.Nickname} attacker={ev.Attacker?.Nickname}");

            if (ev.Player == null)
                return;

            var victim = ev.Player;
            var killer = ev.Attacker;
            if (killer == null || killer == victim)
                return;

            var config = Plugin.Instance.ManualConfig;
            if (!config.EnableKillXp)
                return;

            int xpToAdd = GetXpForKill(victim);
            if (xpToAdd > 0)
            {
                if (debug)
                    Log.Debug($"[EventHandlers] Awarding {xpToAdd} XP for kill to {killer.Nickname}");

                AddPlayerXp(killer, xpToAdd, Plugin.Instance.MyTranslation.XpKillHint);
            }

            CheckTaskProgressOnKill(killer, victim);
        }

        private int GetXpForKill(Player victim)
        {
            var cfg = Plugin.Instance.ManualConfig;
            switch (victim.Role.Team)
            {
                case Team.FoundationForces:
                    return cfg.KillXpMtf;
                case Team.Scientists:
                    return cfg.KillXpScientist;
                case Team.ClassD:
                    return cfg.KillXpDClass;
                case Team.ChaosInsurgency:
                    return cfg.KillXpChaos;
                case Team.SCPs:
                    return cfg.KillXpScp;
                default:
                    return 10;
            }
        }

        public void OnRoundStarted()
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug("[EventHandlers] OnRoundStarted called.");

            var config = Plugin.Instance.ManualConfig;
            if (config.EnableDailyTasks)
            {
                if (_reminderCoroutine.IsRunning)
                    Timing.KillCoroutines(_reminderCoroutine);

                _reminderCoroutine = Timing.CallDelayed(config.ReminderDelaySeconds, () =>
                {
                    foreach (var pl in Player.List)
                    {
                        ShowTextHint(pl, Plugin.Instance.MyTranslation.DailyTaskReminder, 5f);
                    }

                    if (debug)
                        Log.Debug("[EventHandlers] Daily task reminder shown to all players.");
                });
            }

            ResetRoundBasedTasks();
            StartOnlineXpCoroutine();
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug("[EventHandlers] OnRoundEnded called.");

            var config = Plugin.Instance.ManualConfig;
            if (config.EnableTeamWinXp)
            {
                var winningTeam = ev.LeadingTeam;
                if (winningTeam != LeadingTeam.Draw)
                {
                    foreach (var pl in Player.List)
                    {
                        if (GetLeadingTeamFromTeam(pl.Role.Team) == winningTeam)
                        {
                            AddPlayerXp(pl, config.TeamWinXpAmount, Plugin.Instance.MyTranslation.XpTeamWinHint);
                        }
                    }
                }
            }

            if (_onlineXpCoroutine.IsRunning)
                Timing.KillCoroutines(_onlineXpCoroutine);
        }

        private void GenerateDailyTasksForPlayer(PlayerData data)
        {
            data.CurrentDailyTasks.Clear();
            data.DailyTaskProgress.Clear();
            data.CompletedDailyTasks.Clear();

            var allEnabledTasks = Plugin.Instance.TasksConfig.PossibleTasks
                .Where(t => t.Enabled)
                .ToList();

            var debug = Plugin.Instance.ManualConfig.Debug;
            if (allEnabledTasks.Count == 0)
            {
                if (debug)
                    Log.Debug("[EventHandlers] GenerateDailyTasksForPlayer: no enabled tasks found!");
                return;
            }

            var rnd = new Random();
            var chosen = allEnabledTasks.OrderBy(x => rnd.Next()).Take(3).ToList();

            foreach (var taskDef in chosen)
            {
                data.CurrentDailyTasks.Add(taskDef.Id);
                data.DailyTaskProgress[taskDef.Id] = 0;
            }

            if (debug)
                Log.Debug($"[EventHandlers] Generated 3 tasks for player {data.UserId}: {string.Join(", ", data.CurrentDailyTasks)}");
        }

        private void StartOnlineXpCoroutine()
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug("[EventHandlers] Starting online XP coroutine.");

            if (!Plugin.Instance.ManualConfig.EnableTimePlayedXp)
                return;

            _onlineXpCoroutine = Timing.RunCoroutine(OnlineXpChecker());
        }

        private IEnumerator<float> OnlineXpChecker()
        {
            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            while (Round.IsStarted)
            {
                yield return Timing.WaitForSeconds(30f);

                foreach (var pl in Player.List)
                {
                    if (pl == null || !pl.IsVerified)
                        continue;

                    var data = dbManager.GetPlayerData(pl.UserId);
                    if ((DateTime.Now - data.LastTimeXpGiven).TotalMinutes >= 5.0)
                    {
                        int amount = Plugin.Instance.ManualConfig.TimePlayedXpAmount;
                        string msg = Plugin.Instance.MyTranslation.XpTimePlayedHint.Replace("{xp}", amount.ToString());

                        AddPlayerXp(pl, amount, msg);
                        data.LastTimeXpGiven = DateTime.Now;

                        if (Plugin.Instance.ManualConfig.Debug)
                            Log.Debug($"[EventHandlers] Gave {amount} XP for time played to {pl.Nickname}");
                    }
                }

                dbManager.SaveHotDatabase();
            }
        }

        private void AddPlayerXp(Player player, int xpAmount, string translationHint)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId))
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            var data = dbManager.GetPlayerData(player.UserId);

            data.TotalXp += xpAmount;
            string hintMsg = translationHint.Replace("{xp}", xpAmount.ToString());

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[EventHandlers] Player {player.Nickname} now has {data.TotalXp} XP total.");

            ShowTextHint(player, hintMsg, 3f);

            dbManager.SaveHotDatabase();
            UpdateNickname(player);
        }

        private void ShowTextHint(Player player, string text, float duration)
        {
            player.HintDisplay.Show(
                new TextHint(
                    text,
                    new HintParameter[] { new StringHintParameter("") },
                    HintEffectPresets.FadeInAndOut(duration),
                    duration
                )
            );
        }

        private void UpdateNickname(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId))
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            var data = dbManager.GetPlayerData(player.UserId);

            var level = Plugin.Instance.LevelConfig.GetLevelFromXp(data.TotalXp);

            var originalName = string.IsNullOrEmpty(data.OriginalNickname)
                ? player.Nickname
                : data.OriginalNickname;

            var config = Plugin.Instance.ManualConfig;
            string format = string.IsNullOrEmpty(config.NicknameFormat)
                ? "[level] / {nickname}"
                : config.NicknameFormat;

            string newNickname = format
                .Replace("[level]", level.ToString())
                .Replace("{nickname}", originalName);

            player.DisplayNickname = newNickname;

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[EventHandlers] Updated nickname for {player.UserId}. New nickname: {newNickname}");
        }

        private void CheckTaskProgressOnKill(Player killer, Player victim)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug($"[EventHandlers] CheckTaskProgressOnKill: killer={killer?.Nickname}, victim={victim?.Nickname}");

            if (killer == null)
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            var data = dbManager.GetPlayerData(killer.UserId);

            foreach (var taskId in data.CurrentDailyTasks)
            {
                if (data.CompletedDailyTasks.Contains(taskId))
                    continue;

                var def = Plugin.Instance.TasksConfig.PossibleTasks.FirstOrDefault(t => t.Id == taskId);
                if (def == null)
                    continue;

                switch (taskId)
                {
                    case "Kill5DclassOneRound":
                        if (victim.Role.Team == Team.ClassD)
                        {
                            data.DailyTaskProgress[taskId]++;
                            if (debug)
                                Log.Debug($"[EventHandlers] Kill5DclassOneRound progress: {data.DailyTaskProgress[taskId]}");

                            if (data.DailyTaskProgress[taskId] >= 5)
                                CompleteDailyTask(killer, def);
                        }
                        break;

                    case "KillScpMicrohid":
                        if (victim.Role.Team == Team.SCPs)
                        {
                            if (killer.CurrentItem?.Base.ItemTypeId == ItemType.MicroHID)
                                CompleteDailyTask(killer, def);
                        }
                        break;
                }
            }

            dbManager.SaveHotDatabase();
        }

        private void CompleteDailyTask(Player player, TaskDefinition def)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug($"[EventHandlers] Completing daily task {def.Id} for {player.Nickname}");

            var data = Plugin.Instance.PlayerDatabaseManager.GetPlayerData(player.UserId);
            data.CompletedDailyTasks.Add(def.Id);

            string msg = Plugin.Instance.MyTranslation.XpDailyTaskCompleted.Replace("{xp}", def.XpReward.ToString());
            AddPlayerXp(player, def.XpReward, msg);

            Plugin.Instance.PlayerDatabaseManager.SaveHotDatabase();
        }

        private void ResetRoundBasedTasks()
        {
            var debug = Plugin.Instance.ManualConfig.Debug;
            if (debug)
                Log.Debug("[EventHandlers] ResetRoundBasedTasks called.");

            var tasksConfig = Plugin.Instance.TasksConfig;
            var roundBasedIds = tasksConfig.PossibleTasks
                .Where(t => t.MustBeDoneInOneRound)
                .Select(t => t.Id)
                .ToHashSet();

            var dbField = Plugin.Instance.PlayerDatabaseManager
                .GetType()
                .GetField("_hotPlayerDataDict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (!(dbField?.GetValue(Plugin.Instance.PlayerDatabaseManager) is Dictionary<string, PlayerData> dict))
                return;

            foreach (var kvp in dict)
            {
                var data = kvp.Value;
                foreach (var taskId in roundBasedIds)
                {
                    if (!data.CompletedDailyTasks.Contains(taskId))
                    {
                        if (data.DailyTaskProgress.ContainsKey(taskId))
                            data.DailyTaskProgress[taskId] = 0;
                    }
                }
            }

            Plugin.Instance.PlayerDatabaseManager.SaveHotDatabase();
        }

        private LeadingTeam GetLeadingTeamFromTeam(Team team)
        {
            switch (team)
            {
                case Team.FoundationForces:
                case Team.Scientists:
                    return LeadingTeam.FacilityForces;
                case Team.ClassD:
                case Team.ChaosInsurgency:
                case Team.SCPs:
                    return LeadingTeam.Anomalies;
                default:
                    return LeadingTeam.Draw;
            }
        }
    }
}
