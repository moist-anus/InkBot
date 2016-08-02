using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.DataModels;

namespace NadekoBot.Modules.LastFm.Models
{
	internal class LastFmUser : IDataModel
	{
		public long DiscordUserId { get; set; }

        public long DiscordServerId { get; set; }

		public string LastFmUsername { get; set; }
	}
}
