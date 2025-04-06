using Exiled.API.Interfaces;
using System.ComponentModel;

namespace SCPSL_Lvl
{
    public class Translation : ITranslation
    {
        [Description("Сообщение при получении опыта за убийство.")]
        public string XpKillHint { get; set; } = "You received {xp} XP for killing a player!";

        [Description("Сообщение при получении опыта за нахождение на сервере.")]
        public string XpTimePlayedHint { get; set; } = "You received {xp} XP for playing on the server!";

        [Description("Сообщение при получении опыта за победу команды.")]
        public string XpTeamWinHint { get; set; } = "You received {xp} XP for your team's victory!";

        [Description("Сообщение о том, что у игрока есть задания на сегодня.")]
        public string DailyTaskReminder { get; set; } = "You have daily tasks! Type 'tasks' in your console to check them.";

        [Description("Сообщение при получении опыта за выполнение задания.")]
        public string XpDailyTaskCompleted { get; set; } = "You completed a daily task and earned {xp} XP!";

        [Description("Сообщение при вводе команды tasks (заголовок).")]
        public string DailyTasksHeader { get; set; } = "Your daily tasks:";

        [Description("Шаблон одной строки задания. {status} заменяется на (Done/NotDone), {desc} на описание, {xp} на награду.")]
        public string DailyTaskEntry { get; set; } = "- [{status}] {desc} (XP: {xp})";

        [Description("Сообщение при вводе команды lvl.")]
        public string LvlCommandInfo { get; set; } = "Your current level is {level}. You have {currentXp} XP. You need {xpLeft} more XP to reach next level (or you are max level if no next thresholds).";

        // Добавляйте любые другие строки перевода.
    }
}
