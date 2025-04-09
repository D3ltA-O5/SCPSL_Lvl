using Exiled.API.Features;
using System;
using System.IO;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;

namespace SCPSL_Lvl
{
    /// <summary>
    /// Основной класс плагина:
    /// - Наследуется от Plugin<ExiledStubConfig>, чтобы EXILED не выдавал ошибок.
    /// - Использует ManualConfig (MainConfig) из SCPSL_Lvl для всех "настоящих" настроек.
    /// </summary>
    public class Plugin : Plugin<ExiledStubConfig>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "ScpSlLevelSystem";
        public override string Author => "YourNameHere";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        /// <summary>
        /// Здесь можно при желании задать префикс для логов, если нужно.
        /// </summary>
        public override string Prefix => "ScpSlLevelSystem";

        /// <summary>
        /// Папка, в которой лежат MainConfig.yml, LevelThresholds.yml и т.д.
        /// </summary>
        public string PluginFolderPath => Path.Combine(Paths.Configs, "SCPSL_Lvl");

        /// <summary>
        /// Ваш основной конфиг, который хранит все настройки плагина (EnableKillXp и т.д.).
        /// Загружается вручную из SCPSL_Lvl/MainConfig.yml.
        /// </summary>
        public MainConfig ManualConfig { get; private set; }

        // Остальные подсистемы
        public LevelConfig LevelConfig { get; private set; }
        public TasksConfig TasksConfig { get; private set; }
        public PlayerDatabaseManager PlayerDatabaseManager { get; private set; }
        public PluginTranslations MyTranslation { get; private set; }
        public EventHandlers EventHandlers { get; private set; }

        public override void OnEnabled()
        {
            // Сначала даём возможность EXILED загрузить ExiledStubConfig
            base.OnEnabled();

            // Если в ExiledStubConfig стоит IsEnabled = false, то не загружаем MainConfig
            if (!Config.IsEnabled)
            {
                Log.Info($"[{Name}] Disabled via ExiledStubConfig (IsEnabled=false).");
                return;
            }

            Instance = this;

            try
            {
                // Создаём папку SCPSL_Lvl (если её нет), чтобы хранить все YAML-файлы
                Directory.CreateDirectory(PluginFolderPath);

                // Загружаем ваш основной конфиг (ManualConfig)
                var mainConfigPath = Path.Combine(PluginFolderPath, "MainConfig.yml");
                ManualConfig = MainConfig.LoadOrCreate(mainConfigPath);

                // Если в MainConfig (ManualConfig) тоже стоит IsEnabled=false, завершаем
                if (!ManualConfig.IsEnabled)
                {
                    Log.Info($"[{Name}] Disabled via MainConfig (IsEnabled=false).");
                    return;
                }

                if (ManualConfig.Debug)
                    Log.Debug("[Plugin] Loading other configs and database...");

                // Загружаем остальные YAML-конфиги
                LevelConfig = LevelConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "LevelThresholds.yml"));
                TasksConfig = TasksConfig.LoadOrCreate(Path.Combine(PluginFolderPath, "TasksConfig.yml"));
                PlayerDatabaseManager = new PlayerDatabaseManager(Path.Combine(PluginFolderPath, "players.yml"));

                // Переводы
                string translationsPath = Path.Combine(PluginFolderPath, "Translations.yml");
                MyTranslation = PluginTranslations.LoadOrCreate(translationsPath);

                // Подключаем обработчики событий
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

            // Если мы успели загрузить ManualConfig, сохраним его (вдруг что-то менялось в runtime)
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

            // Отписываемся от событий
            UnsubscribeEvents();

            // Сохраняем базу игроков и переводы (если они существуют)
            PlayerDatabaseManager?.SaveDatabase();
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

            Server.RoundStarted -= EventHandlers.OnRoundStarted;
            Server.RoundEnded -= EventHandlers.OnRoundEnded;
        }
    }
}
