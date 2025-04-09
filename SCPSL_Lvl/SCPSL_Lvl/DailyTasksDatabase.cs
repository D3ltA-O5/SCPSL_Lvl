using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    public class DailyTasksDatabase
    {
        [YamlMember(Alias = "LastGeneratedDate")]
        public DateTime LastGeneratedDate { get; set; } = DateTime.MinValue;

        [YamlMember(Alias = "CurrentDailyTasks")]
        public List<string> CurrentDailyTasks { get; set; } = new List<string>();

        public void GenerateNewDailyTasks(TasksConfig tasksConfig, int tasksCount = 3)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            LastGeneratedDate = DateTime.Now.Date;
            CurrentDailyTasks.Clear();

            var enabledTasks = tasksConfig.PossibleTasks.Where(t => t.Enabled).ToList();
            if (enabledTasks.Count == 0)
            {
                if (debug)
                    Log.Debug("[DailyTasksDatabase] No enabled tasks found. Can't generate daily tasks.");
                return;
            }

            var rnd = new Random();
            var shuffled = enabledTasks.OrderBy(x => rnd.Next()).Take(tasksCount);
            CurrentDailyTasks.AddRange(shuffled.Select(t => t.Id));

            if (debug)
                Log.Debug("[DailyTasksDatabase] New daily tasks generated.");
        }

        public static DailyTasksDatabase LoadOrCreate(string path)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            if (debug)
                Log.Debug($"[DailyTasksDatabase] Loading or creating: {path}");

            if (!File.Exists(path))
            {
                var defaultDb = new DailyTasksDatabase();
                defaultDb.Save(path);
                return defaultDb;
            }

            var text = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var db = deserializer.Deserialize<DailyTasksDatabase>(text) ?? new DailyTasksDatabase();

            if (debug)
                Log.Debug("[DailyTasksDatabase] Loaded successfully.");

            return db;
        }

        public void Save(string path)
        {
            var debug = Plugin.Instance.ManualConfig.Debug;

            if (debug)
                Log.Debug($"[DailyTasksDatabase] Saving to {path}");

            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(this);
            File.WriteAllText(path, yaml);

            if (debug)
                Log.Debug("[DailyTasksDatabase] Saved.");
        }

        public void Save()
        {
            string path = Path.Combine(Plugin.Instance.PluginFolderPath, "DailyTasks.yml");
            Save(path);
        }
    }
}
