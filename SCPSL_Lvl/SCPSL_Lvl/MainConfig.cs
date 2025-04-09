using System.IO;
using YamlDotNet.Serialization;
using System.ComponentModel;

namespace SCPSL_Lvl
{
    /// <summary>
    /// Ваш "настоящий" конфиг, который хранит настройки плагина
    /// и лежит в папке SCPSL_Lvl/MainConfig.yml.
    /// </summary>
    public class MainConfig
    {
        [Description("Is the plugin enabled? If false, all features are disabled.")]
        [YamlMember(Alias = "IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable detailed debugging logs?")]
        [YamlMember(Alias = "Debug")]
        public bool Debug { get; set; } = true;

        [Description("Nickname format. [level] is replaced with the player's level, {nickname} with the original nickname.")]
        [YamlMember(Alias = "NicknameFormat")]
        public string NicknameFormat { get; set; } = "[level] / {nickname}";

        [Description("Enable XP gain every 5 minutes on the server?")]
        [YamlMember(Alias = "EnableTimePlayedXp")]
        public bool EnableTimePlayedXp { get; set; } = true;

        [Description("How much XP is gained every 5 minutes on the server?")]
        [YamlMember(Alias = "TimePlayedXpAmount")]
        public int TimePlayedXpAmount { get; set; } = 5;

        [Description("Enable XP gain for killing other players?")]
        [YamlMember(Alias = "EnableKillXp")]
        public bool EnableKillXp { get; set; } = true;

        [Description("XP for killing a D-Class.")]
        [YamlMember(Alias = "KillXpDClass")]
        public int KillXpDClass { get; set; } = 10;

        [Description("XP for killing a Scientist.")]
        [YamlMember(Alias = "KillXpScientist")]
        public int KillXpScientist { get; set; } = 10;

        [Description("XP for killing a Guard.")]
        [YamlMember(Alias = "KillXpGuard")]
        public int KillXpGuard { get; set; } = 20;

        [Description("XP for killing MTF (NTF, etc.).")]
        [YamlMember(Alias = "KillXpMtf")]
        public int KillXpMtf { get; set; } = 30;

        [Description("XP for killing Chaos Insurgency.")]
        [YamlMember(Alias = "KillXpChaos")]
        public int KillXpChaos { get; set; } = 30;

        [Description("XP for killing an SCP.")]
        [YamlMember(Alias = "KillXpScp")]
        public int KillXpScp { get; set; } = 350;

        [Description("Enable XP for team win at the end of the round?")]
        [YamlMember(Alias = "EnableTeamWinXp")]
        public bool EnableTeamWinXp { get; set; } = true;

        [Description("How much XP is given to each surviving player of the winning team?")]
        [YamlMember(Alias = "TeamWinXpAmount")]
        public int TeamWinXpAmount { get; set; } = 50;

        [Description("Enable daily tasks system?")]
        [YamlMember(Alias = "EnableDailyTasks")]
        public bool EnableDailyTasks { get; set; } = true;

        [Description("How many seconds after the round starts do we remind players about daily tasks?")]
        [YamlMember(Alias = "ReminderDelaySeconds")]
        public float ReminderDelaySeconds { get; set; } = 60f;

        /// <summary>
        /// Загружает конфиг из файла или создаёт новый, если файла нет.
        /// </summary>
        public static MainConfig LoadOrCreate(string path)
        {
            if (!File.Exists(path))
            {
                var def = new MainConfig();
                def.Save(path);
                return def;
            }

            var text = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var loaded = deserializer.Deserialize<MainConfig>(text) ?? new MainConfig();
            return loaded;
        }

        /// <summary>
        /// Сохраняет конфиг в указанный путь в формате YAML.
        /// </summary>
        public void Save(string path)
        {
            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(this);
            File.WriteAllText(path, yaml);
        }
    }
}
