using Exiled.API.Interfaces;
using System.ComponentModel;

namespace SCPSL_Lvl
{
    public class MainConfig : IConfig
    {
        [Description("Is the plugin enabled? If false, all features are disabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable detailed debugging logs?")]
        public bool Debug { get; set; } = true;

        [Description("Nickname format. [level] is replaced with the player's level, {nickname} with the original nickname.")]
        public string NicknameFormat { get; set; } = "[level] / {nickname}";

        [Description("Enable XP gain every 5 minutes on the server?")]
        public bool EnableTimePlayedXp { get; set; } = true;

        [Description("How much XP is gained every 5 minutes on the server?")]
        public int TimePlayedXpAmount { get; set; } = 5;

        [Description("Enable XP gain for killing other players?")]
        public bool EnableKillXp { get; set; } = true;

        [Description("XP for killing a D-Class.")]
        public int KillXpDClass { get; set; } = 10;

        [Description("XP for killing a Scientist.")]
        public int KillXpScientist { get; set; } = 10;

        [Description("XP for killing a Guard.")]
        public int KillXpGuard { get; set; } = 20;

        [Description("XP for killing MTF (NTF, etc.).")]
        public int KillXpMtf { get; set; } = 30;

        [Description("XP for killing Chaos Insurgency.")]
        public int KillXpChaos { get; set; } = 30;

        [Description("XP for killing an SCP.")]
        public int KillXpScp { get; set; } = 350;

        [Description("Enable XP for team win at the end of the round?")]
        public bool EnableTeamWinXp { get; set; } = true;

        [Description("How much XP is given to each surviving player of the winning team?")]
        public int TeamWinXpAmount { get; set; } = 50;

        [Description("Enable daily tasks system?")]
        public bool EnableDailyTasks { get; set; } = true;

        [Description("How many seconds after the round starts do we remind players about daily tasks?")]
        public float ReminderDelaySeconds { get; set; } = 60f;
    }
}
