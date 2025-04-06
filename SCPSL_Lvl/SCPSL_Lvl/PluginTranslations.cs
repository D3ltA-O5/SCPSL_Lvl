using System.IO;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    public class PluginTranslations
    {
        [YamlMember(Alias = "XpKillHint")]
        public string XpKillHint { get; set; } = "You received {xp} XP for killing a player!";

        [YamlMember(Alias = "XpTimePlayedHint")]
        public string XpTimePlayedHint { get; set; } = "You received {xp} XP for playing on the server!";

        [YamlMember(Alias = "XpTeamWinHint")]
        public string XpTeamWinHint { get; set; } = "You received {xp} XP for your team's victory!";

        [YamlMember(Alias = "DailyTaskReminder")]
        public string DailyTaskReminder { get; set; } = "You have daily tasks! Type 'tasks' in the console to see them.";

        [YamlMember(Alias = "XpDailyTaskCompleted")]
        public string XpDailyTaskCompleted { get; set; } = "You completed a daily task and earned {xp} XP!";

        [YamlMember(Alias = "DailyTasksHeader")]
        public string DailyTasksHeader { get; set; } = "Your daily tasks:";

        [YamlMember(Alias = "DailyTaskEntry")]
        public string DailyTaskEntry { get; set; } = "- [{status}] {desc} (XP: {xp})";

        [YamlMember(Alias = "LvlCommandInfo")]
        public string LvlCommandInfo { get; set; } = "Your current level is {level}. You have {currentXp} XP. You need {xpLeft} more XP to reach the next level.";

        // Новое поле: показываем при повышении уровня
        [YamlMember(Alias = "LevelUpHint")]
        public string LevelUpHint { get; set; } = "You have leveled up to level {level}!";

        public static PluginTranslations LoadOrCreate(string path)
        {
            if (!File.Exists(path))
            {
                var def = new PluginTranslations();
                def.Save(path);
                return def;
            }
            var text = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var loaded = deserializer.Deserialize<PluginTranslations>(text) ?? new PluginTranslations();
            return loaded;
        }

        public void Save(string path)
        {
            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(this);
            File.WriteAllText(path, yaml);
        }
    }
}
