using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Splatoon.Models
{
    internal class MapSchedules
    {
        public MapSchedules()
        { }

        public List<MapSchedule> Schedules { get; set; } = new List<MapSchedule>();

        public string CommandResponse
        {
            get
            {
                var message = new StringBuilder();

                Schedules.Take(2).ToList().ForEach(s =>
                {
                    message.AppendLine($"{s.BeginEmoji} to {s.EndEmoji} est");
                    message.AppendLine();
                    message.AppendLine($"**{s.RegularStages[0]}** {s.TurfWarEmoji} **{s.RegularStages[1]}**");
                    message.AppendLine($"**{s.RankedStages[0]}** {s.RankedModeEmoji} **{s.RankedStages[1]}**");

                    if (Schedules.IndexOf(s) == 0)
                    {
                        message.AppendLine();
                    }
                });

                return message.ToString();
            }
        }

        public void Add(MapSchedule schedule)
        {
            Schedules.Add(schedule);
        }
    }
}
