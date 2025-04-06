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
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] OnPlayerVerified: {ev.Player?.UserId}");

            if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            if (dbManager == null)
                return;

            var data = dbManager.GetPlayerData(ev.Player.UserId);

            if (string.IsNullOrEmpty(data.OriginalNickname))
            {
                data.OriginalNickname = ev.Player.Nickname;
                dbManager.SaveDatabase();

                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[EventHandlers] Original nickname saved: {data.OriginalNickname}");
            }

            // Check personal tasks (if new day or not 3 tasks, generate them)
            var today = DateTime.Now.Date;
            if (data.LastTasksGeneratedDate < today || data.CurrentDailyTasks.Count < 3)
            {
                GenerateDailyTasksForPlayer(data);
                data.LastTasksGeneratedDate = today;
                dbManager.SaveDatabase();
            }

            UpdateNickname(ev.Player);
        }

        public void OnPlayerJoined(JoinedEventArgs ev)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] OnPlayerJoined: {ev.Player?.Nickname}");
        }

        public void OnPlayerSpawning(SpawningEventArgs ev)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] OnPlayerSpawning: {ev.Player?.Nickname}");

            Timing.CallDelayed(0.5f, () => UpdateNickname(ev.Player));
        }

        public void OnPlayerDied(DiedEventArgs ev)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] OnPlayerDied: victim={ev.Player?.Nickname} attacker={ev.Attacker?.Nickname}");

            if (ev.Player == null)
                return;

            var victim = ev.Player;
            var killer = ev.Attacker;
            if (killer == null || killer == victim)
                return;

            var config = Plugin.Instance.Config;
            if (!config.EnableKillXp)
                return;

            int xpToAdd = GetXpForKill(victim);
            if (xpToAdd > 0)
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[EventHandlers] Awarding {xpToAdd} XP for kill to {killer.Nickname}");

                AddPlayerXp(killer, xpToAdd, Plugin.Instance.MyTranslation.XpKillHint);
            }

            CheckTaskProgressOnKill(killer, victim);
        }

        private int GetXpForKill(Player victim)
        {
            var cfg = Plugin.Instance.Config;
            switch (victim.Role.Team)
            {
                case Team.FoundationForces:
                    // Foundation forces can be MTF or Guard
                    // We can differentiate by RoleTypeId: e.g. if victim is FacilityGuard => KillXpGuard,
                    // else => KillXpMtf
                    if (victim.Role.Type == RoleTypeId.FacilityGuard)
                        return cfg.KillXpGuard;
                    else
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
                    // fallback XP
                    return 5;
            }
        }

        public void OnRoundStarted()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] OnRoundStarted called.");

            var config = Plugin.Instance.Config;
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

                    if (Plugin.Instance.Config.Debug)
                        Log.Debug("[EventHandlers] Daily task reminder shown to all players.");
                });
            }

            ResetRoundBasedTasks();
            StartOnlineXpCoroutine();
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] OnRoundEnded called.");

            var config = Plugin.Instance.Config;
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

        /// <summary>
        /// Generate 3 random tasks for this player from the global TasksConfig.
        /// </summary>
        private void GenerateDailyTasksForPlayer(PlayerData data)
        {
            data.CurrentDailyTasks.Clear();
            data.DailyTaskProgress.Clear();
            data.CompletedDailyTasks.Clear();

            var allEnabled = Plugin.Instance.TasksConfig.PossibleTasks.Where(t => t.Enabled).ToList();
            if (allEnabled.Count == 0)
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug("[EventHandlers] GenerateDailyTasksForPlayer => no enabled tasks found!");
                return;
            }

            // pick 3 random
            var rnd = new Random();
            var chosen = allEnabled.OrderBy(x => rnd.Next()).Take(3).ToList();

            foreach (var tdef in chosen)
            {
                data.CurrentDailyTasks.Add(tdef.Id);
                data.DailyTaskProgress[tdef.Id] = 0;
            }

            data.LastTasksGeneratedDate = DateTime.Now.Date;

            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Generated tasks for {data.UserId}: {string.Join(", ", data.CurrentDailyTasks)}");
        }

        private void StartOnlineXpCoroutine()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] Starting online XP coroutine.");

            if (!Plugin.Instance.Config.EnableTimePlayedXp)
                return;

            _onlineXpCoroutine = MEC.Timing.RunCoroutine(OnlineXpChecker());
        }

        private IEnumerator<float> OnlineXpChecker()
        {
            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            while (Round.IsStarted)
            {
                yield return MEC.Timing.WaitForSeconds(30f);

                foreach (var pl in Player.List)
                {
                    if (pl == null || !pl.IsVerified)
                        continue;

                    var data = dbManager.GetPlayerData(pl.UserId);
                    if ((DateTime.Now - data.LastTimeXpGiven).TotalMinutes >= 5.0)
                    {
                        int amount = Plugin.Instance.Config.TimePlayedXpAmount;
                        string msg = Plugin.Instance.MyTranslation.XpTimePlayedHint.Replace("{xp}", amount.ToString());

                        AddPlayerXp(pl, amount, msg);
                        data.LastTimeXpGiven = DateTime.Now;

                        if (Plugin.Instance.Config.Debug)
                            Log.Debug($"[EventHandlers] Gave {amount} XP for time played to {pl.Nickname}");
                    }
                }

                dbManager.SaveDatabase();
            }
        }

        /// <summary>
        /// Add XP to player, then check if they leveled up => show level up hint.
        /// </summary>
        private void AddPlayerXp(Player player, int xpAmount, string translationHint)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId))
                return;

            var data = Plugin.Instance.PlayerDatabaseManager.GetPlayerData(player.UserId);

            // old level
            var oldLevel = Plugin.Instance.LevelConfig.GetLevelFromXp(data.TotalXp);

            data.TotalXp += xpAmount;

            // new level
            var newLevel = Plugin.Instance.LevelConfig.GetLevelFromXp(data.TotalXp);

            string hintMsg = translationHint.Replace("{xp}", xpAmount.ToString());
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Player {player.Nickname} now has {data.TotalXp} XP total (was L{oldLevel}, now L{newLevel}).");

            ShowTextHint(player, hintMsg, 3f);

            // Если уровень вырос => показываем LevelUpHint
            if (newLevel > oldLevel)
            {
                string lvlHint = Plugin.Instance.MyTranslation.LevelUpHint
                    .Replace("{level}", newLevel.ToString());
                ShowTextHint(player, lvlHint, 4f);

                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[EventHandlers] Player {player.Nickname} leveled up from L{oldLevel} to L{newLevel}!");
            }

            Plugin.Instance.PlayerDatabaseManager.SaveDatabase();
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

            var data = Plugin.Instance.PlayerDatabaseManager.GetPlayerData(player.UserId);
            var oldTotalXp = data.TotalXp;

            var level = Plugin.Instance.LevelConfig.GetLevelFromXp(data.TotalXp);

            var originalName = string.IsNullOrEmpty(data.OriginalNickname)
                ? player.Nickname
                : data.OriginalNickname;

            var config = Plugin.Instance.Config;
            string format = string.IsNullOrEmpty(config.NicknameFormat)
                ? "[level] / {nickname}"
                : config.NicknameFormat;

            string newNickname = format
                .Replace("[level]", level.ToString())
                .Replace("{nickname}", originalName);

            player.DisplayNickname = newNickname;

            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Updated nickname for {player.UserId}. New nickname: {newNickname}");
        }

        private void CheckTaskProgressOnKill(Player killer, Player victim)
        {
            if (Plugin.Instance.Config.Debug)
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
                        if (victim.Role.Team == PlayerRoles.Team.ClassD)
                        {
                            data.DailyTaskProgress[taskId]++;
                            if (Plugin.Instance.Config.Debug)
                                Log.Debug($"[EventHandlers] Kill5DclassOneRound progress: {data.DailyTaskProgress[taskId]}");

                            if (data.DailyTaskProgress[taskId] >= 5)
                                CompleteDailyTask(killer, def);
                        }
                        break;

                    case "KillScpMicrohid":
                        if (victim.Role.Team == PlayerRoles.Team.SCPs)
                        {
                            if (killer.CurrentItem?.Base.ItemTypeId == ItemType.MicroHID)
                                CompleteDailyTask(killer, def);
                        }
                        break;

                    case "DieFallDamage":
                        // This is obviously a "die" task, not kill. So it won't be advanced here. 
                        // Just an example that you might track in OnPlayerDied from victim perspective or so.
                        break;

                    case "TieChaos":
                        // This is not related to kills. You can add the logic somewhere else (OnPlayer079Tied or whatever).
                        break;
                }
            }

            dbManager.SaveDatabase();
        }

        private void CompleteDailyTask(Player player, TaskDefinition def)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Completing daily task {def.Id} for {player.Nickname}");

            var data = Plugin.Instance.PlayerDatabaseManager.GetPlayerData(player.UserId);
            data.CompletedDailyTasks.Add(def.Id);

            // add XP immediately
            string msg = Plugin.Instance.MyTranslation
                .XpDailyTaskCompleted.Replace("{xp}", def.XpReward.ToString());
            AddPlayerXp(player, def.XpReward, msg);
        }

        private void ResetRoundBasedTasks()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] ResetRoundBasedTasks called.");

            var tasksConfig = Plugin.Instance.TasksConfig;
            var roundBasedIds = tasksConfig.PossibleTasks
                .Where(t => t.MustBeDoneInOneRound)
                .Select(t => t.Id)
                .ToHashSet();

            var dbField = Plugin.Instance.PlayerDatabaseManager
                .GetType()
                .GetField("_playerDataDict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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

            Plugin.Instance.PlayerDatabaseManager.SaveDatabase();
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
