using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    /// <summary>
    /// Управляет двумя базами данных:
    /// - "Холодная" (cold) - общий файл со всеми игроками (даже оффлайн).
    /// - "Горячая" (hot) - файл с игроками, которые сейчас на сервере.
    /// </summary>
    public class PlayerDatabaseManager
    {
        private readonly string _coldDatabasePath;
        private readonly string _hotDatabasePath;

        /// <summary>
        /// Холодная БД: данные обо всех игроках.
        /// </summary>
        private Dictionary<string, PlayerData> _coldPlayerDataDict = new Dictionary<string, PlayerData>();

        /// <summary>
        /// Горячая БД: данные только о тех игроках, которые сейчас на сервере.
        /// </summary>
        private Dictionary<string, PlayerData> _hotPlayerDataDict = new Dictionary<string, PlayerData>();

        public PlayerDatabaseManager(string coldPath, string hotPath)
        {
            _coldDatabasePath = coldPath;
            _hotDatabasePath = hotPath;

            LoadColdDatabase();
            LoadHotDatabase();
            // Если вы хотите начинать каждое включение сервера с пустой горячей базы, 
            // то вместо LoadHotDatabase() сделайте _hotPlayerDataDict = new Dictionary<string, PlayerData>();
        }

        /// <summary>
        /// Основной метод, который вызывается кодом плагина.
        /// Возвращает PlayerData из горячей БД, создаёт новую запись (и копирует из холодной),
        /// если её там ещё нет.
        /// </summary>
        public PlayerData GetPlayerData(string userId)
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            // Сначала смотрим в горячую базу
            if (_hotPlayerDataDict.TryGetValue(userId, out var hotData))
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Found existing HotData for {userId}.");
                return hotData;
            }

            // Если в горячей нет, значит игрок только подключился. Попробуем взять из холодной
            if (_coldPlayerDataDict.TryGetValue(userId, out var coldData))
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Loading data from ColdDB for {userId}.");
                // Копируем объект
                var newData = ClonePlayerData(coldData);
                _hotPlayerDataDict[userId] = newData;
                SaveHotDatabase();
                return newData;
            }
            else
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Creating new data for {userId} (not found in ColdDB).");
                // Совсем новый игрок
                var newData = new PlayerData
                {
                    UserId = userId,
                    TotalXp = 0,
                    LastTimeXpGiven = DateTime.Now,
                };
                _hotPlayerDataDict[userId] = newData;
                SaveHotDatabase();
                return newData;
            }
        }

        /// <summary>
        /// Вызывается, когда игрок выходит. 
        /// Синхронизирует его данные из горячей базы обратно в холодную, удаляет из горячей.
        /// </summary>
        public void RemoveFromHotDatabase(string userId)
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            if (_hotPlayerDataDict.TryGetValue(userId, out var hotData))
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Removing {userId} from HotDB and syncing to ColdDB.");

                // Переносим актуальные данные в холодную
                _coldPlayerDataDict[userId] = ClonePlayerData(hotData);

                // Убираем из горячей
                _hotPlayerDataDict.Remove(userId);

                // Сохраняем
                SaveColdDatabase();
                SaveHotDatabase();
            }
            else
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Player {userId} not found in HotDB. Nothing to remove.");
            }
        }

        /// <summary>
        /// Полностью загружает холодную БД из файла.
        /// </summary>
        public void LoadColdDatabase()
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            if (!File.Exists(_coldDatabasePath))
            {
                if (debug)
                    Log.Debug("[PlayerDatabaseManager] ColdDB file not found. Creating new empty file.");
                SaveColdDatabase();
                return;
            }

            try
            {
                var text = File.ReadAllText(_coldDatabasePath);
                var deserializer = new DeserializerBuilder().Build();
                var list = deserializer.Deserialize<List<PlayerData>>(text) ?? new List<PlayerData>();
                _coldPlayerDataDict = list.ToDictionary(p => p.UserId, p => p);

                if (debug)
                    Log.Debug("[PlayerDatabaseManager] ColdDB loaded successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error loading cold DB: {e}");
                _coldPlayerDataDict = new Dictionary<string, PlayerData>();
            }
        }

        /// <summary>
        /// Сохраняет холодную БД в файл.
        /// </summary>
        public void SaveColdDatabase()
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            try
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Saving ColdDB to {_coldDatabasePath}");

                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(_coldPlayerDataDict.Values.ToList());
                File.WriteAllText(_coldDatabasePath, yaml);

                if (debug)
                    Log.Debug("[PlayerDatabaseManager] ColdDB saved successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error saving cold DB: {e}");
            }
        }

        /// <summary>
        /// Загружает горячую БД из файла.
        /// Если файла нет, создаёт пустую.
        /// </summary>
        public void LoadHotDatabase()
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            if (!File.Exists(_hotDatabasePath))
            {
                if (debug)
                    Log.Debug("[PlayerDatabaseManager] HotDB file not found. Creating new empty file.");
                SaveHotDatabase();
                return;
            }

            try
            {
                var text = File.ReadAllText(_hotDatabasePath);
                var deserializer = new DeserializerBuilder().Build();
                var list = deserializer.Deserialize<List<PlayerData>>(text) ?? new List<PlayerData>();
                _hotPlayerDataDict = list.ToDictionary(p => p.UserId, p => p);

                if (debug)
                    Log.Debug("[PlayerDatabaseManager] HotDB loaded successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error loading hot DB: {e}");
                _hotPlayerDataDict = new Dictionary<string, PlayerData>();
            }
        }

        /// <summary>
        /// Сохраняет горячую БД в файл.
        /// </summary>
        public void SaveHotDatabase()
        {
            bool debug = Plugin.Instance.ManualConfig.Debug;

            try
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Saving HotDB to {_hotDatabasePath}");

                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(_hotPlayerDataDict.Values.ToList());
                File.WriteAllText(_hotDatabasePath, yaml);

                if (debug)
                    Log.Debug("[PlayerDatabaseManager] HotDB saved successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error saving hot DB: {e}");
            }
        }

        /// <summary>
        /// Утилита для копирования PlayerData, чтобы при передаче данных
        /// между холодной и горячей базами мы не держали одну и ту же ссылку.
        /// </summary>
        private PlayerData ClonePlayerData(PlayerData original)
        {
            // Можно использовать сериализацию/десериализацию YAML, JSON и т.д.,
            // но здесь просто вручную копируем нужные поля.
            return new PlayerData
            {
                UserId = original.UserId,
                TotalXp = original.TotalXp,
                LastTimeXpGiven = original.LastTimeXpGiven,
                OriginalNickname = original.OriginalNickname,
                LastTasksGeneratedDate = original.LastTasksGeneratedDate,
                CurrentDailyTasks = new List<string>(original.CurrentDailyTasks),
                CompletedDailyTasks = new HashSet<string>(original.CompletedDailyTasks),
                DailyTaskProgress = new Dictionary<string, int>(original.DailyTaskProgress),
            };
        }
    }
}
