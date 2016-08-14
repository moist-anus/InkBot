using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using MoistFm.Models;
using NadekoBot.Classes;
using NadekoBot.DataModels;
using NadekoBot.Modules.LastFm.Models;

namespace NadekoBot.Modules.LastFm.Handlers
{
	internal static class LastFmUserHandler
	{
		public static async Task<List<LastFmUser>> GetScrobblers()
		{
			return await Task<List<LastFmUser>>.Run(() =>
			{
				return DbHandler.Instance.GetAllRows<LastFmUser>().ToList();
			}).ConfigureAwait(false);
		}

		public static async Task SaveScrobble(long serverId, long userId, LfmTrack track)
		{
			await Task.Run(async () =>
			{
				var scrobble = await GetLastScrobble(serverId, userId);

				if (scrobble == null)
				{
					scrobble = new LastFmScrobble(serverId, userId, track.Artist.Name, track.Name, DateTime.Now);
				}
				else
				{
					scrobble.Artist = track.Artist.Name;
					scrobble.Track = track.Name;
				}

				DbHandler.Instance.Save(scrobble);
			});
		}

		public static async Task<LastFmScrobble> GetLastScrobble(long serverId, long userId)
		{
			return await Task.Run(() =>
			{
				return DbHandler.Instance.FindAll<LastFmScrobble>(s => s.DiscordServerId == serverId && s.DiscordUserId == userId).OrderByDescending(s => s.Scrobbled).FirstOrDefault();
			}).ConfigureAwait(false);
		}

		public static async Task AssociateUsername(CommandEventArgs e, string lastFmUsername)
		{
			await Task.Run(() =>
			{
				var userId = Convert.ToInt64(e.User.Id);
				var serverId = Convert.ToInt64(e.Server.Id);
				var existingUser = DbHandler.Instance.FindOne<LastFmUser>(t => t.DiscordUserId == userId);

				if (existingUser != null)
				{
					e.Channel.SendMessage("User already has a last.fm username.").ConfigureAwait(false);
					return;
				}

				DbHandler.Instance.Save(new LastFmUser { DiscordUserId = userId, DiscordServerId = serverId, LastFmUsername = lastFmUsername });
				e.Channel.SendMessage($"Set last.fm username to **{lastFmUsername}**").ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		public static async Task DisplayUsername(CommandEventArgs e)
		{
			await Task.Run(() =>
			{
				var serverId = Convert.ToInt64(e.Server.Id);
				var userId = Convert.ToInt64(e.User.Id);
				var lastFmUser = DbHandler.Instance.FindOne<LastFmUser>(t => t.DiscordServerId == serverId && t.DiscordUserId == userId);
				string message = lastFmUser != null ? $"Your last.fm username is **{lastFmUser.LastFmUsername}**" : "You don't have a last.fm username set.";

				e.Channel.SendMessage(message).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		public static async Task<string> GetUsername(long serverId, long userId)
		{
			return await Task<string>.Run(() =>
			{
				var lastFmUser = DbHandler.Instance.FindOne<LastFmUser>(t => t.DiscordServerId == serverId && t.DiscordUserId == userId);
				return lastFmUser != null ? lastFmUser.LastFmUsername : string.Empty;
			});
		}

		public static async Task<string> GetUsername(long userId)
		{
			return await Task<string>.Run(() =>
			{
				var lastFmUser = DbHandler.Instance.FindOne<LastFmUser>(t => t.DiscordUserId == userId);
				return lastFmUser != null ? lastFmUser.LastFmUsername : string.Empty;
			}).ConfigureAwait(false);
		}
	}
}
