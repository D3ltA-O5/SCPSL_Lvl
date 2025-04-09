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
        private Dictionary<string, PlayerData> _playerDataDict = new Dictionary<string, PlayerData>();

        public PlayerDatabaseManager(string path)
        {
            _databasePath = path;
            LoadDatabase();
        }

        public PlayerData GetPlayerData(string userId)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            if (debug)
                Log.Debug($"[PlayerDatabaseManager] GetPlayerData called for {userId}");

            if (_playerDataDict.TryGetValue(userId, out var data))
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Found existing PlayerData for {userId}");
                return data;
            }
            else
            {
                var newData = new PlayerData
                {
                    UserId = userId,
                    TotalXp = 0,
                    LastTimeXpGiven = DateTime.Now,
                };

                _playerDataDict[userId] = newData;

                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Created new PlayerData for {userId}");

                SaveDatabase();
                return newData;
            }
        }

        public void SaveDatabase()
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            try
            {
                if (debug)
                    Log.Debug($"[PlayerDatabaseManager] Saving player database to {_databasePath}");

                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(_playerDataDict.Values.ToList());
                File.WriteAllText(_databasePath, yaml);

                if (debug)
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
                if (Plugin.Instance.ManualConfig.Debug)
                    Log.Debug("[PlayerDatabaseManager] Database file not found. Creating new empty file.");

                SaveDatabase();
                return;
            }

            try
            {
                var text = File.ReadAllText(_databasePath);
                var deserializer = new DeserializerBuilder().Build();
                var list = deserializer.Deserialize<List<PlayerData>>(text) ?? new List<PlayerData>();

                _playerDataDict = list.ToDictionary(p => p.UserId, p => p);

                if (Plugin.Instance.ManualConfig.Debug)
                    Log.Debug("[PlayerDatabaseManager] Database loaded successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerDatabaseManager] Error loading database: {e}");
                _playerDataDict = new Dictionary<string, PlayerData>();
            }
        }

        public void ResetDailyTasks(List<string> currentDailyTasks)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            if (debug)
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
