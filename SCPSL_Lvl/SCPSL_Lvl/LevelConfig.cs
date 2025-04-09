using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    public class LevelConfig
    {
        [YamlMember(Alias = "LevelThresholds")]
        public Dictionary<int, int> LevelThresholds { get; set; } = new Dictionary<int, int>()
        {
            {1, 0},
            {2, 100},
            {3, 300},
            {4, 600},
            {5, 1000}
        };

        public int GetLevelFromXp(int totalXp)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Calculating level from XP: {totalXp}");

            var sorted = LevelThresholds.OrderBy(k => k.Key);
            int currentLevel = 1;

            foreach (var kvp in sorted)
            {
                int lvl = kvp.Key;
                int reqXp = kvp.Value;
                if (totalXp >= reqXp)
                    currentLevel = lvl;
                else
                    break;
            }

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Determined level: {currentLevel}");

            return currentLevel;
        }

        public static LevelConfig LoadOrCreate(string path)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Loading or creating: {path}");

            if (!File.Exists(path))
            {
                var def = new LevelConfig();
                def.Save(path);
                return def;
            }

            var text = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var result = deserializer.Deserialize<LevelConfig>(text) ?? new LevelConfig();
            return result;
        }

        public void Save(string path)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Saving to file: {path}");

            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(this);
            File.WriteAllText(path, yaml);

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug("[LevelConfig] Saved.");
        }
    }
}
