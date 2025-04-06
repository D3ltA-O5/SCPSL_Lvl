using Exiled.API.Features;
using Exiled.API.Interfaces;
using System;
using System.IO;
// Важно: не забудьте нужные using для RoundStartedEventArgs, RoundEndedEventArgs и т.д.
using Server = Exiled.Events.Handlers.Server;
using Player = Exiled.Events.Handlers.Player;

namespace SCPSL_Lvl
{
    public class Plugin : Plugin<MainConfig>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "ScpSlLevelSystem";
        public override string Author => "YourNameHere";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        /// <summary>
        /// Если хотите хранить свои файлы (LevelConfig, TaskConfig) в папке SCPSL_Lvl:
        /// </summary>
        public string PluginFolderPath => Path.Combine(Paths.Configs, "SCPSL_Lvl");

        public LevelConfig LevelConfig { get; private set; }
        public TasksConfig TasksConfig { get; private set; }
        public PlayerDatabaseManager PlayerDatabaseManager { get; private set; }
        public PluginTranslations MyTranslation { get; private set; }

        public EventHandlers EventHandlers { get; private set; }

        public override void OnEnabled()
        {
            base.OnEnabled();

            Instance = this;

            if (Config.Debug)
                Log.Debug("[Plugin] OnEnabled called.");

            try
            {
                // Создадим папку SCPSL_Lvl, если нужно
                Directory.CreateDirectory(PluginFolderPath);

                // Грузим любые дополнительные конфиги (пример, если у вас есть)
                // LevelConfig = LevelConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "LevelThresholds.yml"));
                // TasksConfig = TasksConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "TasksConfig.yml"));
                // PlayerDatabaseManager = new PlayerDatabaseManager(Path.Combine(PluginFolderPath, "players.yml"));
                // MyTranslation = PluginTranslations.LoadOrCreate(Path.Combine(PluginFolderPath, "Translations.yml"));

                // Создаём и подписываемся
                EventHandlers = new EventHandlers();
                SubscribeEvents();

                Log.Info($"[{Name}] Plugin enabled. Version: {Version}");
            }
            catch (Exception e)
            {
                Log.Error($"[{Name}] Error enabling plugin: {e}");
            }
        }

        public override void OnDisabled()
        {
            base.OnDisabled();

            if (Config.Debug)
                Log.Debug("[Plugin] OnDisabled called.");

            UnsubscribeEvents();

            // Сохранение если нужно
            // PlayerDatabaseManager?.SaveDatabase();

            Log.Info($"[{Name}] Plugin disabled.");
            Instance = null;
        }

        private void SubscribeEvents()
        {
            if (Config.Debug)
                Log.Debug("[Plugin] Subscribing to events...");

            Player.Joined += EventHandlers.OnPlayerJoined;
            Player.Verified += EventHandlers.OnPlayerVerified;
            Player.Spawning += EventHandlers.OnPlayerSpawning;
            Player.Died += EventHandlers.OnPlayerDied;

            Server.RoundStarted += EventHandlers.OnRoundStarted;
            Server.RoundEnded += EventHandlers.OnRoundEnded;
        }

        private void UnsubscribeEvents()
        {
            if (Config.Debug)
                Log.Debug("[Plugin] Unsubscribing from events...");

            Player.Joined -= EventHandlers.OnPlayerJoined;
            Player.Verified -= EventHandlers.OnPlayerVerified;
            Player.Spawning -= EventHandlers.OnPlayerSpawning;
            Player.Died -= EventHandlers.OnPlayerDied;

            Server.RoundStarted -= EventHandlers.OnRoundStarted;
            Server.RoundEnded -= EventHandlers.OnRoundEnded;
        }
    }
}
