using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NadekoBot.Modules.Splatoon.Models
{
    internal class MapSchedule
    {
        public MapSchedule(JToken schedule)
        {
            IsFestival = false;
            Begin = (DateTime)schedule[BeginElement];
            End = (DateTime)schedule[EndElement];
            RankedMode = (string)schedule[RankedModeElement];

            RegularStages.AddRange(schedule[StagesElement][RegularElement].Select(s => (string)s[StageNameElement]));
            RankedStages.AddRange(schedule[StagesElement][RankedElement].Select(s => (string)s[StageNameElement]));
        }

        public bool IsFestival { get; set; }

        public DateTime Begin { get; set; }

        public string BeginEmoji { get { return $":clock{(Begin.Hour > 12 ? Begin.Hour - 12 : Begin.Hour)}:"; } }

        public DateTime End { get; set; }

        public string EndEmoji { get { return $":clock{(End.Hour > 12 ? End.Hour - 12 : End.Hour)}:"; } }

        public string RankedMode { get; set; }

        public string RankedModeEmoji
        {
            get
            {
                switch (RankedMode)
                {
                    case "Rainmaker":
                        return ":cloud_rain:";
                    case "Tower Control":
                        return ":tokyo_tower:";
                    case "Splat Zones":
                    default:
                        return ":boom:";
                }
            }
        }

        public string TurfWarEmoji { get; } = ":u6e80:";

        public List<string> RegularStages { get; set; } = new List<string>();

        public List<string> RankedStages { get; set; } = new List<string>();

        private string BeginElement { get { return "begin"; } }

        private string EndElement { get { return "end"; } }

        private string RankedModeElement { get { return "ranked_modeEN"; } }

        private string StagesElement { get { return "stages"; } }

        private string RegularElement { get { return "regular"; } }

        private string RankedElement { get { return "ranked"; } }

        private string StageNameElement { get { return "nameEN"; } }
    }
}
