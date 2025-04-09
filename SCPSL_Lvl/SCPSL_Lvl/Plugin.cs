using Exiled.API.Features;
using System;
using System.IO;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;

namespace SCPSL_Lvl
{
    public class Plugin : Plugin<ExiledStubConfig>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "ScpSlLevelSystem";
        public override string Author => "YourNameHere";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        public override string Prefix => "ScpSlLevelSystem";

        /// <summary>
        /// Папка, в которой лежат MainConfig.yml, LevelThresholds.yml, players_cold.yml, players_hot.yml и т.д.
        /// </summary>
        public string PluginFolderPath => Path.Combine(Paths.Configs, "SCPSL_Lvl");

        /// <summary>
        /// Ваш основной конфиг (все настройки), загружается вручную из SCPSL_Lvl/MainConfig.yml.
        /// </summary>
        public MainConfig ManualConfig { get; private set; }

        // Остальные объекты
        public LevelConfig LevelConfig { get; private set; }
        public TasksConfig TasksConfig { get; private set; }

        /// <summary>
        /// Менеджер баз данных (холодной и горячей).
        /// </summary>
        public PlayerDatabaseManager PlayerDatabaseManager { get; private set; }

        public PluginTranslations MyTranslation { get; private set; }
        public EventHandlers EventHandlers { get; private set; }

        public override void OnEnabled()
        {
            base.OnEnabled();

            // Проверяем ExiledStubConfig (IsEnabled=false => плагин не запускается)
            if (!Config.IsEnabled)
            {
                Log.Info($"[{Name}] Disabled via ExiledStubConfig (IsEnabled=false).");
                return;
            }

            Instance = this;

            try
            {
                Directory.CreateDirectory(PluginFolderPath);

                // Загружаем наш "настоящий" конфиг
                var mainConfigPath = Path.Combine(PluginFolderPath, "MainConfig.yml");
                ManualConfig = MainConfig.LoadOrCreate(mainConfigPath);

                if (!ManualConfig.IsEnabled)
                {
                    Log.Info($"[{Name}] Disabled via MainConfig (IsEnabled=false).");
                    return;
                }

                if (ManualConfig.Debug)
                    Log.Debug("[Plugin] Loading other configs and database...");

                // Загружаем остальные конфиги
                LevelConfig = LevelConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "LevelThresholds.yml"));
                TasksConfig = TasksConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "TasksConfig.yml"));

                // Инициализируем базы данных (холодную и горячую)
                string coldDbPath = Path.Combine(PluginFolderPath, "players_cold.yml");
                string hotDbPath = Path.Combine(PluginFolderPath, "players_hot.yml");
                PlayerDatabaseManager = new PlayerDatabaseManager(coldDbPath, hotDbPath);

                // Переводы
                string translationsPath = Path.Combine(PluginFolderPath, "Translations.yml");
                MyTranslation = PluginTranslations.LoadOrCreate(translationsPath);

                // Регистрируем обработчики
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

            // Сохраняем ManualConfig (на случай, если что-то менялось)
            if (ManualConfig != null)
            {
                try
                {
                    var mainConfigPath = Path.Combine(PluginFolderPath, "MainConfig.yml");
                    ManualConfig.Save(mainConfigPath);
                }
                catch (Exception ex)
                {
                    Log.Error($"[{Name}] Error saving MainConfig on disable: {ex}");
                }
            }

            // Отписываемся
            UnsubscribeEvents();

            // Сохраняем базы данных (холодная + горячая) - на случай корректного завершения
            PlayerDatabaseManager?.SaveColdDatabase();
            PlayerDatabaseManager?.SaveHotDatabase();

            MyTranslation?.Save(Path.Combine(PluginFolderPath, "Translations.yml"));

            Log.Info($"[{Name}] Plugin disabled.");
            Instance = null;
        }

        private void SubscribeEvents()
        {
            if (ManualConfig != null && ManualConfig.Debug)
                Log.Debug("[Plugin] Subscribing to events...");

            Player.Verified += EventHandlers.OnPlayerVerified;
            Player.Died += EventHandlers.OnPlayerDied;
            Player.Spawning += EventHandlers.OnPlayerSpawning;
            Player.Joined += EventHandlers.OnPlayerJoined;

            // Новый слушатель на выход игрока
            Player.Left += EventHandlers.OnPlayerLeft;

            Server.RoundStarted += EventHandlers.OnRoundStarted;
            Server.RoundEnded += EventHandlers.OnRoundEnded;
        }

        private void UnsubscribeEvents()
        {
            if (EventHandlers == null)
                return;

            if (ManualConfig != null && ManualConfig.Debug)
                Log.Debug("[Plugin] Unsubscribing from events...");

            Player.Verified -= EventHandlers.OnPlayerVerified;
            Player.Died -= EventHandlers.OnPlayerDied;
            Player.Spawning -= EventHandlers.OnPlayerSpawning;
            Player.Joined -= EventHandlers.OnPlayerJoined;

            // Отписываемся от Left
            Player.Left -= EventHandlers.OnPlayerLeft;

            Server.RoundStarted -= EventHandlers.OnRoundStarted;
            Server.RoundEnded -= EventHandlers.OnRoundEnded;
        }
    }
}
