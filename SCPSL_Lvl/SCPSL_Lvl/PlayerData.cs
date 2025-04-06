using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SCPSL_Lvl
{
    public class PlayerData
    {
        [YamlMember(Alias = "UserId")]
        public string UserId { get; set; }

        [YamlMember(Alias = "TotalXp")]
        public int TotalXp { get; set; }

        [YamlIgnore]
        public DateTime LastTimeXpGiven { get; set; } = DateTime.Now;

        [YamlMember(Alias = "LastTimeXpGivenTicks")]
        public long LastTimeXpGivenTicks
        {
            get => LastTimeXpGiven.Ticks;
            set => LastTimeXpGiven = new DateTime(value);
        }

        // Для отслеживания прогресса заданий
        [YamlMember(Alias = "DailyTaskProgress")]
        public Dictionary<string, int> DailyTaskProgress { get; set; } = new Dictionary<string, int>();

        [YamlMember(Alias = "CompletedDailyTasks")]
        public HashSet<string> CompletedDailyTasks { get; set; } = new HashSet<string>();

        // Сохраняем ник
        [YamlMember(Alias = "OriginalNickname")]
        public string OriginalNickname { get; set; } = "";

        /// <summary>
        /// Дата, когда мы в последний раз генерировали личные ежедневные задания для этого игрока
        /// </summary>
        [YamlMember(Alias = "LastTasksGeneratedDate")]
        public DateTime LastTasksGeneratedDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Список (Id) ежедневных заданий, назначенных **лично этому игроку** сегодня (по умолчанию - 3 задания).
        /// </summary>
        [YamlMember(Alias = "CurrentDailyTasks")]
        public List<string> CurrentDailyTasks { get; set; } = new List<string>();
    }
}
