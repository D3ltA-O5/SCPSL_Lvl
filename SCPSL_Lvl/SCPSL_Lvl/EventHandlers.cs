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

        /// <summary>
        /// Вызывается, когда игрок уже аутентифицирован (Verified). Здесь мы генерируем/проверяем личные задания.
        /// </summary>
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

            // Сохраняем оригинальный ник (если не сохранён)
            if (string.IsNullOrEmpty(data.OriginalNickname))
            {
                data.OriginalNickname = ev.Player.Nickname;
                dbManager.SaveDatabase();

                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[EventHandlers] Original nickname saved: {data.OriginalNickname}");
            }

            // Проверяем, не сменился ли день (или нет ли у игрока заданий)
            var today = DateTime.Now.Date;
            if (data.LastTasksGeneratedDate < today || data.CurrentDailyTasks.Count < 3)
            {
                // Сгенерировать 3 личных задания для этого игрока
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
            // Редко трогаем ник здесь, лучше всё в Verified
        }

        public void OnPlayerSpawning(SpawningEventArgs ev)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] OnPlayerSpawning: {ev.Player?.Nickname}");

            // Чуть подождём и обновим ник (чтобы роль точно применялась)
            Timing.CallDelayed(0.5f, () => UpdateNickname(ev.Player));
        }

        /// <summary>
        /// Событие смерти игрока: проверяем, кто убил, выдаём опыт, проверяем квесты.
        /// </summary>
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

            // Начисляем опыт за убийство (если включено)
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

            // Проверяем личные квесты на убийство
            CheckTaskProgressOnKill(killer, victim);
        }

        private int GetXpForKill(Player victim)
        {
            // Можно расширить, если у вас DummyRole, Tutorial, etc.
            var cfg = Plugin.Instance.Config;
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
                    return 10; // fallback XP
            }
        }

        public void OnRoundStarted()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] OnRoundStarted called.");

            // Убрали логику глобального CheckDailyTasks — теперь задачи личные
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

            // Победа команды
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

        // Генерируем игроку ровно 3 рандомных задания (Enabled = true) из TasksConfig
        private void GenerateDailyTasksForPlayer(PlayerData data)
        {
            data.CurrentDailyTasks.Clear();
            data.DailyTaskProgress.Clear();
            data.CompletedDailyTasks.Clear();

            var allEnabledTasks = Plugin.Instance.TasksConfig.PossibleTasks
                .Where(t => t.Enabled)
                .ToList();

            if (allEnabledTasks.Count == 0)
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug("[EventHandlers] GenerateDailyTasksForPlayer: no enabled tasks found!");
                return;
            }

            // Берём случайные 3 (если меньше 3 осталось, возьмём все)
            var rnd = new Random();
            var chosen = allEnabledTasks.OrderBy(x => rnd.Next()).Take(3).ToList();

            foreach (var taskDef in chosen)
            {
                data.CurrentDailyTasks.Add(taskDef.Id);
                data.DailyTaskProgress[taskDef.Id] = 0;
            }

            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Generated 3 tasks for player {data.UserId}: {string.Join(", ", data.CurrentDailyTasks)}");
        }

        private void StartOnlineXpCoroutine()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] Starting online XP coroutine.");

            if (!Plugin.Instance.Config.EnableTimePlayedXp)
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

        private void AddPlayerXp(Player player, int xpAmount, string translationHint)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId))
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            var data = dbManager.GetPlayerData(player.UserId);

            data.TotalXp += xpAmount;
            string hintMsg = translationHint.Replace("{xp}", xpAmount.ToString());

            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] Player {player.Nickname} now has {data.TotalXp} XP total.");

            ShowTextHint(player, hintMsg, 3f);

            dbManager.SaveDatabase();
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

        /// <summary>
        /// Важно: теперь берём задачи не из глобального DailyTasks, а из PlayerData.CurrentDailyTasks.
        /// </summary>
        private void CheckTaskProgressOnKill(Player killer, Player victim)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[EventHandlers] CheckTaskProgressOnKill: killer={killer?.Nickname}, victim={victim?.Nickname}");

            if (killer == null)
                return;

            var dbManager = Plugin.Instance.PlayerDatabaseManager;
            var data = dbManager.GetPlayerData(killer.UserId);

            // Личные задания
            foreach (var taskId in data.CurrentDailyTasks)
            {
                // Если уже выполнено, пропускаем
                if (data.CompletedDailyTasks.Contains(taskId))
                    continue;

                var def = Plugin.Instance.TasksConfig.PossibleTasks.FirstOrDefault(t => t.Id == taskId);
                if (def == null)
                    continue;

                // Идём по задаче
                switch (taskId)
                {
                    case "Kill5DclassOneRound":
                        if (victim.Role.Team == Team.ClassD)
                        {
                            data.DailyTaskProgress[taskId]++;
                            if (Plugin.Instance.Config.Debug)
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

                        // Здесь можно дополнять другими заданиями
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

            // Сразу добавляем экспу (по желанию)
            string msg = Plugin.Instance.MyTranslation.XpDailyTaskCompleted.Replace("{xp}", def.XpReward.ToString());
            AddPlayerXp(player, def.XpReward, msg);

            // Сохраняем
            Plugin.Instance.PlayerDatabaseManager.SaveDatabase();
        }

        private void ResetRoundBasedTasks()
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[EventHandlers] ResetRoundBasedTasks called.");

            var tasksConfig = Plugin.Instance.TasksConfig;
            // Ищем те задания, у которых MustBeDoneInOneRound = true
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
                    // Если задание не выполнено — сбрасываем прогресс
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
