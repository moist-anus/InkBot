using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.DataModels;

namespace Inkbot.Modules.Stats.Models
{
	internal class StatsMessage : IDataModel
	{
		public StatsMessage(long serverId, long channelId, long userId, string message)
			: this()
		{
			ServerId = serverId;
			ChannelId = channelId;
			UserId = userId;
			Message = message;
		}

		public StatsMessage()
		{ }

		public long ServerId { get; set; }

		public long ChannelId { get; set; }

		public long UserId { get; set; }

		public string Message { get; set; }
	}
}