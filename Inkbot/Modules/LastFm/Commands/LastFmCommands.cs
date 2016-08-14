using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Discord.Commands;
using log4net;
using MoistFm;
using MoistFm.Models;
using NadekoBot.Classes;
using NadekoBot.Extensions;
using NadekoBot.Modules.LastFm.Handlers;
using NadekoBot.Modules.LastFm.Models;
using Newtonsoft.Json.Linq;

namespace NadekoBot.Modules.LastFm.Commands
{
	class LastFmCommands : DiscordCommand
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(LastFmCommands));

		public LastFmCommands(DiscordModule module) : base(module)
		{ }

		private Discord.User DiscordUser { get; set; } = null;
		private Timer Timer { get; set; } = null;
		private static string ApiKey { get; set; } = NadekoBot.Creds.LastFmApiKey;
		private static LfmService Service { get; set; } = new LfmService(ApiKey);

		private async Task RunForValidUser(CommandEventArgs e, Action<LfmUser, string, StringBuilder> action)
		{
			var userParameter = e.GetArg("user");
			string lastFmUsername = string.Empty;
			Discord.User discordUser = null;
			bool isValidUser = await Task<bool>.Run(async () =>
			{
				if (e.User == null)
				{
					return false;
				}

				discordUser = string.IsNullOrEmpty(userParameter) ? e.User : e.Server.Users.Where(u => u.Name == userParameter || u.Nickname == userParameter).FirstOrDefault();
				lastFmUsername = await LastFmUserHandler.GetUsername(Convert.ToInt64(discordUser.Id));

				if (string.IsNullOrEmpty(lastFmUsername))
				{
					await e.Channel.SendMessage("Last.fm username not set.").ConfigureAwait(false);
					return false;
				}

				return true;
			}).ConfigureAwait(false);

			if (isValidUser)
			{
				var message = new StringBuilder();
				var user = new LfmUser(lastFmUsername, Service);
				var displayName = string.IsNullOrEmpty(discordUser.Nickname) ? discordUser.Name : discordUser.Nickname;
				
				action(user, displayName, message);

				if (!string.IsNullOrEmpty(message.ToString())) await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
			}
		}

		private string DownloadImage(Uri uri)
		{
			string imageDirectory = "lastfm";
			Directory.CreateDirectory(imageDirectory);
			var imagePath = Path.Combine(imageDirectory, Path.GetFileName(uri.AbsolutePath));

			using (var webClient = new WebClient())
			{
				webClient.DownloadFile(uri, imagePath);
			}

			return imagePath;
		}

		internal override void Init(CommandGroupBuilder cgb)
		{
			cgb.CreateCommand(Prefix + "setusername")
				.Parameter("user", ParameterType.Required)
				.Do(async e =>
				{
					var lastFmUsername = e.GetArg("user").Trim();
					await LastFmUserHandler.AssociateUsername(e, lastFmUsername);
				});

			cgb.CreateCommand(Prefix + "getusername")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					m.AppendLine($"**{n}**'s last.fm username is **{u.Name}**");
				}));

			cgb.CreateCommand(Prefix + "userimage")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetInfo();
					var url = u.Images.Where(i => string.Equals(i.Size, "large", StringComparison.OrdinalIgnoreCase)).FirstOrDefault().Url;

					if (string.IsNullOrEmpty(url))
					{
						e.Channel.SendMessage($"No image found for **{n}**");
						return;
					}

					var imageUri = new Uri(u.Images.Where(i => i.Size == "large").FirstOrDefault().Url);
					var imagePath = DownloadImage(imageUri);

					e.Channel.SendFile(imagePath).ConfigureAwait(false);
				}));

			cgb.CreateCommand(Prefix + "recent")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetRecentTracks();
					m.AppendLine($"Recent tracks for **{n}**");
					u.RecentTracks.Take(5).ForEach(t => m.AppendLine($"{t.Artist.Name} - {t.Name}"));
				}));

			cgb.CreateCommand(Prefix + "artistsweek")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetWeeklyArtistChart();
					m.AppendLine($"Top weekly artists for **{n}**");
					u.WeeklyArtistChart.Take(5).ForEach(a => m.AppendLine($"{a.Name} ({a.Playcount} plays)"));
				}));

			cgb.CreateCommand(Prefix + "tracksweek")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetWeeklyTrackChart();
					m.AppendLine($"Top weekly tracks for **{n}**");
					u.WeeklyTrackChart.Take(5).ForEach(t => m.AppendLine($"{t.Artist.Name} - {t.Name} ({t.Stats.Playcount} plays)"));
				}));

			cgb.CreateCommand(Prefix + "similar")
				.Parameter("artist", ParameterType.Required)
				.Do(async e =>
				{
					var artistParameter = e.GetArg("artist").Trim();

					if (string.IsNullOrEmpty(artistParameter))
					{
						await e.Channel.SendMessage($"Artist parameter missing for bio command.");
						return;
					}

					var artist = new LfmArtist(artistParameter, Service);

					await Task.Run(() =>
					{
						artist.GetSimilar();
					});

					var message = new StringBuilder();

					message.AppendLine($"**Similar** to **{artist.Name}**");

					foreach (var similarArtist in artist.Similar.Take(5))
					{
						message.AppendLine($"**{similarArtist.Name}** - {similarArtist.Url}");
					}

					await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
				});

			cgb.CreateCommand(Prefix + "bio")
				.Parameter("artist", ParameterType.Required)
				.Do(async e =>
				{
					var artistParameter = e.GetArg("artist").Trim();

					if (string.IsNullOrEmpty(artistParameter))
					{
						await e.Channel.SendMessage($"Artist parameter missing for bio command.");
						return;
					}

					var artist = new LfmArtist(artistParameter, Service);
					artist.GetInfo();

					var message = new StringBuilder();

					message.AppendLine($"**Biography** for **{artist.Name}**");
					message.AppendLine($"Published on **{artist.Bio.Published.ToString("MMM-dd-yyyy")}**");
					message.AppendLine($"{artist.Bio.Summary}");
					message.AppendLine($"**{artist.Stats.Playcount}** plays from **{artist.Stats.Listeners}** listeners");

					await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
					message.Clear();

					artist.GetTopAlbums();
					message.AppendLine($"**Top albums**");

					foreach (var album in artist.TopAlbums.Take(5))
					{
						message.AppendLine($"{album.Name}");
					}

					await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
					message.Clear();

					artist.GetTopTracks();

					message.AppendLine($"**Top tracks**");

					foreach (var track in artist.TopTracks.Take(5))
					{
						message.AppendLine($"{track.Name}");
					}

					await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
					message.Clear();

					artist.GetTopTags();
					message.AppendLine($"**Top tags**");

					foreach (var tag in artist.TopTags.Take(5))
					{
						message.AppendLine($"{tag.Name}");
					}

					await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);

					var imageUri = new Uri(artist.Images.Where(i => i.Size == "large").FirstOrDefault().Url);
					var imagePath = DownloadImage(imageUri);

					await e.Channel.SendFile(imagePath).ConfigureAwait(false);
				});

			cgb.CreateCommand(Prefix + "userinfo")
				.Description("Displays last.fm user information.")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetInfo();

					m.AppendLine($"Last.fm user info for **{n}**");

					if (!string.IsNullOrEmpty(u.RealName)) m.AppendLine($"**Real name:** {u.RealName}");
					if (!string.IsNullOrEmpty(u.Url)) m.AppendLine($"**Url:** {u.Url}");
					if (!string.IsNullOrEmpty(u.Country)) m.AppendLine($"**Country:** {u.Country}");
					if (u.Age != 0) m.AppendLine($"**Age:** {u.Age}");
					if (!string.IsNullOrEmpty(u.Gender)) m.AppendLine($"**Gender:** {u.Gender}");
					if (u.Playcount != 0) m.AppendLine($"**Playcount:** {u.Playcount}");
					if (u.Playlists != 0) m.AppendLine($"**Playlists:** {u.Playlists}");
					if (u.Registered.Date != default(DateTime)) m.AppendLine($"**Registered:** {u.Registered.Date.ToString("MMM-dd-yyyy")}");

					u.GetFriends();
					m.AppendLine($"**Friends**: {u.Friends.Count()}");

					u.GetLovedTracks();
					m.AppendLine($"**Loved tracks:** {u.LovedTracks.Count()}");

					e.Channel.SendMessage(m.ToString()).ConfigureAwait(false);
					m.Clear();

					m.AppendLine($"**Top artists:**");
					u.GetTopArtists();
					u.TopArtists.Take(5).ForEach(a => m.AppendLine($"{a.Name} ({a.Playcount} plays)"));

					e.Channel.SendMessage(m.ToString()).ConfigureAwait(false);
					m.Clear();

					m.AppendLine($"**Top tracks:**");
					u.GetTopTracks();
					u.TopTracks.Take(5).ForEach(t => m.AppendLine($"{t.Artist.Name} - {t.Name} ({t.Stats.Playcount} plays)"));

					e.Channel.SendMessage(m.ToString()).ConfigureAwait(false);
					m.Clear();

					m.AppendLine($"**Top tags:**");
					u.GetTopTags();
					u.TopTags.Take(5).ForEach(t => m.AppendLine($"{t.Name}"));

					e.Channel.SendMessage(m.ToString()).ConfigureAwait(false);
					m.Clear();

					m.AppendLine($"**Recent tracks:**");
					u.GetRecentTracks();
					u.RecentTracks.Take(5).ForEach(t => m.AppendLine($"{t.Artist.Name} - {t.Name}"));

					e.Channel.SendMessage(m.ToString()).ConfigureAwait(false);
					m.Clear();

					var imageUri = new Uri(u.Images.Where(i => i.Size == "large").FirstOrDefault().Url);
					var imagePath = DownloadImage(imageUri);

					e.Channel.SendFile(imagePath).ConfigureAwait(false);
				}));

			cgb.CreateCommand(Prefix + "nowplaying")
				.Parameter("user", ParameterType.Optional)
				.Do(async e => await RunForValidUser(e, (u, n, m) =>
				{
					u.GetNowPlaying();

					if (string.IsNullOrEmpty(u.NowPlaying.Artist.Name) || string.IsNullOrEmpty(u.NowPlaying.Name))
					{
						m.AppendLine($"No current scrobbles found for **{n}**");
						return;
					}

					u.NowPlaying.GetInfo();
					m.AppendLine(CreateTrackMessage(e.User, u.NowPlaying));
				}));

			cgb.CreateCommand(Prefix + "autoscrobble")
					.Alias("asc")
					.Description("Starts the auto scrobble display.")
					.Parameter("pollRate", ParameterType.Optional)
					.Do(async e =>
					{
						await Task.Run(() =>
									{
										string pollRateParameter = e.GetArg("pollRate")?.Trim();

										if (string.Equals(pollRateParameter, "stop", StringComparison.OrdinalIgnoreCase))
										{
											if (Timer != null)
											{
												Timer.Enabled = false;
											}

											string message = Timer != null ? "**Auto scrobble** stopped." : "**Auto scrobble** display isn't started.";
											e.Channel.SendMessage(message).ConfigureAwait(false);
											return;
										}

										double pollRate;

										if (string.IsNullOrEmpty(pollRateParameter))
										{
											pollRate = .5;
										}
										else if (double.TryParse(pollRateParameter, out pollRate) && pollRate <= 0)
										{
											e.Channel.SendMessage($"**{pollRate}** is an invalid poll rate.").ConfigureAwait(false);
											return;
										}

										if (Timer != null && Timer.Enabled)
										{
											double currentPollRate = Timer.Interval / 60 / 1000;

											if (string.IsNullOrEmpty(pollRateParameter) || pollRate == currentPollRate)
											{
												e.Channel.SendMessage($"**Auto scrobble** already started (**{currentPollRate}** minute poll rate).").ConfigureAwait(false);
												return;
											}

											Timer.Interval = pollRate * 60 * 1000;
											e.Channel.SendMessage($"**Auto scrobble** poll rated changed to **{pollRate}** minute{(pollRate == 1 ? string.Empty : "s")}").ConfigureAwait(false);
											return;
										}

										Timer = new Timer(pollRate * 60 * 1000);
										Timer.Elapsed += (source, te) => ScrobbleToChannel(source, te, e);
										Timer.Enabled = true;

										e.Channel.SendMessage($"**Auto scrobble** started (polling every **{pollRate}** minute{(pollRate == 1 ? string.Empty : "s")}).").ConfigureAwait(false);
									});
					});
		}

		private static string CreateTrackMessage(Discord.User user, LfmTrack track)
		{
			var message = new StringBuilder();
			message.Append($"**{(string.IsNullOrEmpty(user.Nickname) ? user.Name : user.Nickname)}** is playing **{track.Artist.Name}** - **{track.Name}**");

			if (track.Duration != 0) message.Append($" ({track.Length.ToString(@"m\:ss")})");
			if (!string.IsNullOrEmpty(track.Album.Name)) message.Append($" from **{track.Album.Name}**");

			return message.ToString();
		}

		private static async void ScrobbleToChannel(object source, ElapsedEventArgs te, CommandEventArgs e)
		{
			var serverId = Convert.ToInt64(e.Server.Id);
			var scrobblers = await LastFmUserHandler.GetScrobblers();

			foreach (var scrobbler in scrobblers)
			{
				var user = new LfmUser(scrobbler.LastFmUsername, new LfmService(ApiKey));
				var userId = scrobbler.DiscordUserId;
				user.GetNowPlaying();

				if (!string.IsNullOrEmpty(user.NowPlaying.Name))
				{
					user.NowPlaying.GetInfo();
					var lastScrobble = await LastFmUserHandler.GetLastScrobble(serverId, userId);
					bool isLastArtist = string.Equals(user.NowPlaying.Artist.Name, lastScrobble.Artist, StringComparison.OrdinalIgnoreCase);
					bool isLastTitle = string.Equals(user.NowPlaying.Name, lastScrobble.Track, StringComparison.OrdinalIgnoreCase);
					bool isLastTrack = isLastArtist && isLastTitle;

					if (!isLastTrack)
					{
						var discordUser = e.Channel.Users.Where(u => u.Id == Convert.ToUInt64(userId)).FirstOrDefault();
						await LastFmUserHandler.SaveScrobble(serverId, userId, user.NowPlaying);

						log.Debug($"({e.Server.Name}) #{e.Channel.Name} @{discordUser.Name} {e.Command.Text} display");
						await e.Channel.SendMessage(CreateTrackMessage(discordUser, user.NowPlaying)).ConfigureAwait(false);
					}
				}
			}
		}

		private static async void SendScrobbleMessage(LastFmUser lastFmUser, CommandEventArgs e)
		{
			var user = new LfmUser(lastFmUser.LastFmUsername, new LfmService(ApiKey));
			var userId = lastFmUser.DiscordUserId;
			user.GetNowPlaying();

			if (!string.IsNullOrEmpty(user.NowPlaying.Name))
			{
				user.NowPlaying.GetInfo();
				var serverId = Convert.ToInt64(e.Server.Id);
				var lastScrobble = await LastFmUserHandler.GetLastScrobble(serverId, userId);
				bool isLastArtist = string.Equals(user.NowPlaying.Artist.Name, lastScrobble.Artist, StringComparison.OrdinalIgnoreCase);
				bool isLastTitle = string.Equals(user.NowPlaying.Name, lastScrobble.Track, StringComparison.OrdinalIgnoreCase);
				bool isLastTrack = isLastArtist && isLastTitle;

				if (!isLastTrack)
				{
					var discordUser = e.Channel.Users.Where(u => u.Id == Convert.ToUInt64(userId)).FirstOrDefault();
					await LastFmUserHandler.SaveScrobble(serverId, userId, user.NowPlaying);

					log.Debug($"({e.Server.Name}) #{e.Channel.Name} @{discordUser.Name} {e.Command.Text} display");
					await e.Channel.SendMessage(CreateTrackMessage(discordUser, user.NowPlaying)).ConfigureAwait(false);
				}
			}
		}
	}
}
