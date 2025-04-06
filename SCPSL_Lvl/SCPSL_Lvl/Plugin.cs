using Exiled.API.Features;
using Exiled.API.Interfaces;
using System;
using System.IO;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;

namespace SCPSL_Lvl
{
    public class Plugin : Plugin<MainConfig>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "ScpSlLevelSystem";
        public override string Author => "YourNameHere";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        public string PluginFolderPath => Path.Combine(Paths.Configs, "SCPSL_Lvl");

        // Конфиги уровней
        public LevelConfig LevelConfig { get; private set; }

        // Конфиг с общими определениями задач (TasksConfig)
        public TasksConfig TasksConfig { get; private set; }

        // База игроков
        public PlayerDatabaseManager PlayerDatabaseManager { get; private set; }

        // Переводы
        public PluginTranslations MyTranslation { get; private set; }

        public EventHandlers EventHandlers { get; private set; }

        public override void OnEnabled()
        {
            base.OnEnabled();

            Instance = this;

            if (Config.Debug)
                Log.Debug("[Plugin] OnEnabled called. (Instance set)");

            try
            {
                Directory.CreateDirectory(PluginFolderPath);

                if (Config.Debug)
                    Log.Debug("[Plugin] Loading LevelConfig, TasksConfig, PlayerDatabaseManager, and Translations...");

                LevelConfig = LevelConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "LevelThresholds.yml"));
                TasksConfig = TasksConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "TasksConfig.yml"));
                PlayerDatabaseManager = new PlayerDatabaseManager(Path.Combine(PluginFolderPath, "players.yml"));

                string translationsPath = Path.Combine(PluginFolderPath, "Translations.yml");
                MyTranslation = PluginTranslations.LoadOrCreate(translationsPath);

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

            PlayerDatabaseManager?.SaveDatabase();
            MyTranslation?.Save(Path.Combine(PluginFolderPath, "Translations.yml"));

            Log.Info($"[{Name}] Plugin disabled.");
            Instance = null;
        }

        private void SubscribeEvents()
        {
            if (Config.Debug)
                Log.Debug("[Plugin] Subscribing to events...");

            Player.Verified += EventHandlers.OnPlayerVerified;
            Player.Died += EventHandlers.OnPlayerDied;
            Player.Spawning += EventHandlers.OnPlayerSpawning;
            Player.Joined += EventHandlers.OnPlayerJoined;

            Server.RoundStarted += EventHandlers.OnRoundStarted;
            Server.RoundEnded += EventHandlers.OnRoundEnded;
        }

        private void UnsubscribeEvents()
        {
            if (Config.Debug)
                Log.Debug("[Plugin] Unsubscribing from events...");

            Player.Verified -= EventHandlers.OnPlayerVerified;
            Player.Died -= EventHandlers.OnPlayerDied;
            Player.Spawning -= EventHandlers.OnPlayerSpawning;
            Player.Joined -= EventHandlers.OnPlayerJoined;

            Server.RoundStarted -= EventHandlers.OnRoundStarted;
            Server.RoundEnded -= EventHandlers.OnRoundEnded;
        }
    }
}
