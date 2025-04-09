using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using Exiled.API.Features;

namespace SCPSL_Lvl
{
    public class TaskDefinition
    {
        [YamlMember(Alias = "Id")]
        public string Id { get; set; }

        [YamlMember(Alias = "Description")]
        public string Description { get; set; }

        [YamlMember(Alias = "Enabled")]
        public bool Enabled { get; set; } = true;

        [YamlMember(Alias = "XpReward")]
        public int XpReward { get; set; }

        [YamlMember(Alias = "MustBeDoneInOneRound")]
        public bool MustBeDoneInOneRound { get; set; } = false;
    }

    public class TasksConfig
    {
        [YamlMember(Alias = "PossibleTasks")]
        public List<TaskDefinition> PossibleTasks { get; set; } = new List<TaskDefinition>
        {
            new TaskDefinition
            {
                Id = "Kill5DclassOneRound",
                Description = "Kill 5 D-Class in one round",
                Enabled = true,
                XpReward = 200,
                MustBeDoneInOneRound = true,
            },
            new TaskDefinition
            {
                Id = "KillScpMicrohid",
                Description = "Kill an SCP using the MicroHID",
                Enabled = true,
                XpReward = 300,
                MustBeDoneInOneRound = true,
            },
            new TaskDefinition
            {
                Id = "TieChaos",
                Description = "Tie a Chaos Insurgent",
                Enabled = true,
                XpReward = 150,
                MustBeDoneInOneRound = true,
            },
            new TaskDefinition
            {
                Id = "TieGuard",
                Description = "Tie a Guard",
                Enabled = true,
                XpReward = 100,
                MustBeDoneInOneRound = true,
            },
            new TaskDefinition
            {
                Id = "DieFallDamage",
                Description = "Die from fall damage in one round",
                Enabled = true,
                XpReward = 50,
                MustBeDoneInOneRound = true,
            }
        };

        public static TasksConfig LoadOrCreate(string path)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[TasksConfig] Loading or creating: {path}");

            if (!File.Exists(path))
            {
                var def = new TasksConfig();
                def.Save(path);
                return def;
            }

            var text = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var config = deserializer.Deserialize<TasksConfig>(text) ?? new TasksConfig();
            return config;
        }

        public void Save(string path)
        {
            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug($"[TasksConfig] Saving to file: {path}");

            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(this);
            File.WriteAllText(path, yaml);

            if (Plugin.Instance.ManualConfig.Debug)
                Log.Debug("[TasksConfig] Saved.");
        }
    }
}
