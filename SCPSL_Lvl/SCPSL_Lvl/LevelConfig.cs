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
        /// <summary>
        /// Словарь: {Уровень} -> {XP, необходимый для достижения этого уровня}.
        /// По умолчанию генерируются уровни 1..100, с возрастающим количеством XP.
        /// Пользователь может вручную отредактировать их в конфиге (LevelThresholds.yml).
        /// </summary>
        [YamlMember(Alias = "LevelThresholds")]
        public Dictionary<int, int> LevelThresholds { get; set; } = GenerateDefaultThresholds();

        /// <summary>
        /// Возвращает уровень игрока, исходя из его общего опыта (TotalXp).
        /// </summary>
        public int GetLevelFromXp(int totalXp)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Calculating level from XP: {totalXp}");

            // Сортируем уровни по возрастанию ключа (уровня).
            var sorted = LevelThresholds.OrderBy(k => k.Key);
            int currentLevel = 1;

            foreach (var kvp in sorted)
            {
                int lvl = kvp.Key;
                int requiredXp = kvp.Value;
                if (totalXp >= requiredXp)
                    currentLevel = lvl;
                else
                    break;
            }

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[LevelConfig] Determined level: {currentLevel}");

            return currentLevel;
        }

        /// <summary>
        /// Загружает или создаёт LevelConfig (LevelThresholds.yml).
        /// Если файл отсутствует, записывается дефолт.
        /// </summary>
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

        /// <summary>
        /// Сохраняет текущий LevelConfig в YAML-файл.
        /// </summary>
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

        /// <summary>
        /// Генерирует словарь по умолчанию для 1..100 уровней, с
        /// постепенно увеличивающимися требованиями XP.
        /// </summary>
        private static Dictionary<int, int> GenerateDefaultThresholds()
        {
            // Логика: 
            // Уровень 1 -> 0 XP
            // Уровень 2 -> 100 XP
            // Уровень 3 -> 300 XP
            // Уровень 4 -> 600 XP
            // Уровень 5 -> 1000 XP
            // ...
            // (продолжаем до 100 уровня, используя ту же схему наращивания).
            //
            // Формула: разница между уровнем n и (n-1) растёт на +100, +200, +300 и т.д.
            // Эквивалентно: XP(n) = 100 + 200 + ... + (n-1)*100 (для n>1).
            // Или XP(n) = 50 * (n-1) * n.

            var dict = new Dictionary<int, int>();
            for (int lvl = 1; lvl <= 100; lvl++)
            {
                if (lvl == 1)
                {
                    dict[lvl] = 0;
                }
                else
                {
                    // формула: 50 * (lvl-1) * lvl
                    // (lvl-1)*lvl / 2 * 100, упрощая получаем 50*(lvl-1)*lvl
                    int xpRequired = 50 * (lvl - 1) * lvl;
                    dict[lvl] = xpRequired;
                }
            }
            return dict;
        }
    }
}
