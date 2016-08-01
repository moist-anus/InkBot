using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using NadekoBot.Classes;
using NadekoBot.Extensions;
using NadekoBot.Modules.Splatoon.Models;
using Newtonsoft.Json.Linq;

namespace NadekoBot.Modules.Splatoon.Commands
{
    class SplatoonCommands : DiscordCommand
    {
        public SplatoonCommands(DiscordModule module)
			: base(module)
		{ }

        private string JsonUrl { get { return "http://splatapi.ovh/schedule_na.json"; } }

        private string ScheduleElement { get { return "schedule"; } }

        private MapSchedules MapSchedules { get; set; } = new MapSchedules();

        internal override void Init(CommandGroupBuilder cgb)
        {
            cgb.CreateCommand(Module.Prefix + "rotation")
                .Description("Displays the current and upcoming Splatoon maps.")
                .Do(async e =>
                {
                    using (var webClient = new WebClient())
                    {
                        JObject.Parse(webClient.DownloadString(JsonUrl))[ScheduleElement].ForEach(s => MapSchedules.Add(new MapSchedule(s)));
                        await e.Channel.SendMessage(MapSchedules.CommandResponse).ConfigureAwait(false);
                    }
                });
        }
    }
}
