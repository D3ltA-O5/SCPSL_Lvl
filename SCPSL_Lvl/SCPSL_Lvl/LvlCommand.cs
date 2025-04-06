using CommandSystem;
using Exiled.API.Features;
using System;

namespace SCPSL_Lvl
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class LvlCommand : ICommand
    {
        public string Command { get; } = "lvl";
        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "Shows your current level and how much XP remains to the next level.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (Plugin.Instance.Config.Debug)
                Log.Debug("[LvlCommand] Execute called.");

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
            var lvlConfig = plugin.LevelConfig;
            if (db == null || lvlConfig == null)
            {
                response = "Database or level config not found. Contact admin.";
                return false;
            }

            var data = db.GetPlayerData(player.UserId);
            if (data == null)
            {
                response = "No data found for your user. Try reconnecting.";
                return false;
            }

            int level = lvlConfig.GetLevelFromXp(data.TotalXp);

            if (Plugin.Instance.Config.Debug)
                Log.Debug($"[LvlCommand] Player {player.Nickname} XP = {data.TotalXp}, level = {level}");

            // Find next level
            int? nextLevel = null;
            int xpNeeded = 0;
            foreach (var kvp in lvlConfig.LevelThresholds)
            {
                if (kvp.Key > level)
                {
                    if (nextLevel == null || kvp.Key < nextLevel.Value)
                    {
                        nextLevel = kvp.Key;
                    }
                }
            }

            if (nextLevel.HasValue)
            {
                xpNeeded = lvlConfig.LevelThresholds[nextLevel.Value] - data.TotalXp;
                if (xpNeeded < 0) xpNeeded = 0;
            }

            var tr = plugin.MyTranslation;
            string msg = tr.LvlCommandInfo
                .Replace("{level}", level.ToString())
                .Replace("{currentXp}", data.TotalXp.ToString())
                .Replace("{xpLeft}", xpNeeded.ToString());

            if (!nextLevel.HasValue)
            {
                msg += "\nYou have reached the maximum level!";
            }

            response = msg;
            return true;
        }
    }
}
