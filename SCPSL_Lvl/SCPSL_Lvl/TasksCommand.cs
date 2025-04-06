using CommandSystem;
using Exiled.API.Features;
using System;
using System.Linq;

namespace SCPSL_Lvl
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class TasksCommand : ICommand
    {
        public string Command { get; } = "tasks";
        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "Shows your current daily tasks.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[TasksCommand] Execute called.");

            var player = Player.Get(sender);
            if (player == null)
            {
                response = "This command must be used in the client console by a player.";
                return false;
            }

            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                response = "Plugin not found.";
                return false;
            }

            var db = plugin.PlayerDatabaseManager;
            var tasksCfg = plugin.TasksConfig;
            if (db == null || tasksCfg == null)
            {
                response = "Database or tasks config not found. Contact admin.";
                return false;
            }

            // Получаем PlayerData этого игрока
            var data = db.GetPlayerData(player.UserId);
            if (data == null)
            {
                response = "No data found for your user. Try reconnecting.";
                return false;
            }

            var tr = plugin.MyTranslation;

            string header = tr.DailyTasksHeader;
            string result = header + "\n";

            // Личные задания
            foreach (var taskId in data.CurrentDailyTasks)
            {
                var def = tasksCfg.PossibleTasks.FirstOrDefault(t => t.Id == taskId);
                if (def == null)
                    continue;

                bool done = data.CompletedDailyTasks.Contains(taskId);
                string status = done ? "Done" : "NotDone";

                string entry = tr.DailyTaskEntry
                    .Replace("{status}", status)
                    .Replace("{desc}", def.Description)
                    .Replace("{xp}", def.XpReward.ToString());

                result += entry + "\n";
            }

            response = result;
            return true;
        }
    }
}
