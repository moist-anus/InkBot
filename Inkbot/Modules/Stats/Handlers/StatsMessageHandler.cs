using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inkbot.Modules.Stats.Models;
using NadekoBot.Classes;

namespace Inkbot.Modules.Stats.Handlers
{
	internal static class StatsMessageHandler
	{
		public static async Task SaveMessage(long serverId, long channelId, long userId, string message)
		{
			await Task.Run(() =>
			{
				var statsMessage = new StatsMessage(serverId, channelId, userId, message);
				DbHandler.Instance.Save(statsMessage);
			});
		}

		public static async Task<int> GetMessageCount(long serverId)
		{
			return await Task.Run(() =>
			{
				return DbHandler.Instance.FindAll<StatsMessage>(m => m.ServerId == serverId).Count;
			});
		}
	}
}
