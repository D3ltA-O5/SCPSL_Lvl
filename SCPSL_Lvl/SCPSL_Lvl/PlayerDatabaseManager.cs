using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    public class PlayerDatabaseManager
    {
        private readonly string _databasePath;

        // Закрытое поле, где храним все PlayerData
        private Dictionary<string, PlayerData> _playerDataDict = new Dictionary<string, PlayerData>();

        public PlayerDatabaseManager(string path)
        {
            _databasePath = path;
            LoadDatabase();
        }

        public PlayerData GetPlayerData(string userId)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[PlayerDatabaseManager] GetPlayerData called for {userId}");

            if (_playerDataDict.TryGetValue(userId, out var data))
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[PlayerDatabaseManager] Found existing PlayerData for {userId}");
                return data;
            }
            else
            {
                // Создаём новую запись
                var newData = new PlayerData
                {
                    UserId = userId,
                    TotalXp = 0,
                    LastTimeXpGiven = DateTime.Now,
                };

                _playerDataDict[userId] = newData;

                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[PlayerDatabaseManager] Created new PlayerData for {userId}");

                SaveDatabase();
                return newData;
            }
        }

        public void SaveDatabase()
        {
            try
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[PlayerDatabaseManager] Saving player database to {_databasePath}");

                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(_playerDataDict.Values.ToList());
                File.WriteAllText(_databasePath, yaml);

                if (Plugin.Instance.Config.Debug)
                    Log.Debug("[PlayerDatabaseManager] Database saved successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error saving database: {e}");
            }
        }

        public void LoadDatabase()
        {
            if (!File.Exists(_databasePath))
            {
                if (Plugin.Instance.Config.Debug)
                    Log.Debug($"[PlayerDatabaseManager] Database file not found. Creating new empty file.");

                SaveDatabase();
                return;
            }

            try
            {
                var text = File.ReadAllText(_databasePath);
                var deserializer = new DeserializerBuilder().Build();
                var list = deserializer.Deserialize<List<PlayerData>>(text) ?? new List<PlayerData>();

                _playerDataDict = list.ToDictionary(p => p.UserId, p => p);

                if (Plugin.Instance.Config.Debug)
                    Log.Debug("[PlayerDatabaseManager] Database loaded successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error loading database: {e}");
                _playerDataDict = new Dictionary<string, PlayerData>();
            }
        }

        /// <summary>
        /// Сбрасываем ежедневные задания у всех игроков (прогресс), если они ещё не выполнены.
        /// </summary>
        public void ResetDailyTasks(List<string> currentDailyTasks)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[PlayerDatabaseManager] ResetDailyTasks called.");

            foreach (var data in _playerDataDict.Values)
            {
                data.DailyTaskProgress.Clear();
                data.CompletedDailyTasks.Clear();

                foreach (var taskId in currentDailyTasks)
                {
                    data.DailyTaskProgress[taskId] = 0;
                }
            }
            SaveDatabase();
        }
    }
}
