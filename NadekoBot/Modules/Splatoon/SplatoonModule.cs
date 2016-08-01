using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Modules;
using NadekoBot.Extensions;
using NadekoBot.Modules.Permissions.Classes;
using NadekoBot.Modules.Splatoon.Commands;

namespace NadekoBot.Modules.Splatoon
{
    internal class SplatoonModule : DiscordModule
    {
        public SplatoonModule()
        {
            commands.Add(new SplatoonCommands(this));
        }

        public override string Prefix => NadekoBot.Config.CommandPrefixes.Splatoon;

        public override void Install(ModuleManager manager)
        {
            manager.CreateCommands("", cgb =>
            {
                cgb.AddCheck(PermissionChecker.Instance);
                commands.ForEach(c => c.Init(cgb));
            });
        }
    }
}
