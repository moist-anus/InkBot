using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using NadekoBot.Modules.Administration.Commands;
using NadekoBot.Modules.Music;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using log4net;

namespace NadekoBot
{
	public class NadekoStats
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(NadekoStats));

		public static NadekoStats Instance { get; } = new NadekoStats();

		public string BotVersion => $"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version}";

		private int commandsRan = 0;

		private string statsCache = "";

		private readonly Stopwatch statsStopwatch = new Stopwatch();

		public int ServerCount { get; private set; } = 0;

		public int TextChannelsCount { get; private set; } = 0;

		public int VoiceChannelsCount { get; private set; } = 0;

		private readonly Timer commandLogTimer = new Timer() { Interval = 10000 };

		private readonly Timer carbonStatusTimer = new Timer() { Interval = 3600000 };

		private static ulong messageCounter = 0;

		public static ulong MessageCounter => messageCounter;

		static NadekoStats() { }

		private NadekoStats()
		{
			var commandService = NadekoBot.Client.GetService<CommandService>();

			statsStopwatch.Start();

			commandService.CommandExecuted += StatsCollector_RanCommand;
			commandService.CommandFinished += CommandService_CommandFinished;
			commandService.CommandErrored += CommandService_CommandFinished;

			Task.Run(StartCollecting);

			commandLogTimer.Start();

			ServerCount = NadekoBot.Client.Servers.Count();
			var channels = NadekoBot.Client.Servers.SelectMany(s => s.AllChannels);
			var channelsArray = channels as Channel[] ?? channels.ToArray();
			TextChannelsCount = channelsArray.Count(c => c.Type == ChannelType.Text);
			VoiceChannelsCount = channelsArray.Count() - TextChannelsCount;

			NadekoBot.Client.MessageReceived += (s, e) => messageCounter++;

			NadekoBot.Client.JoinedServer += (s, e) =>
			{
				try
				{
					ServerCount++;
					TextChannelsCount += e.Server.TextChannels.Count();
					VoiceChannelsCount += e.Server.VoiceChannels.Count();
				}
				catch { }
			};

			NadekoBot.Client.LeftServer += (s, e) =>
			{
				try
				{
					ServerCount--;
					TextChannelsCount -= e.Server.TextChannels.Count();
					VoiceChannelsCount -= e.Server.VoiceChannels.Count();
				}
				catch { }
			};

			NadekoBot.Client.ChannelCreated += (s, e) =>
			{
				try
				{
					if (e.Channel.IsPrivate) return;

					if (e.Channel.Type == ChannelType.Text)
					{
						TextChannelsCount++;
					}
					else if (e.Channel.Type == ChannelType.Voice)
					{
						VoiceChannelsCount++;
					}
				}
				catch { }
			};

			NadekoBot.Client.ChannelDestroyed += (s, e) =>
			{
				try
				{
					if (e.Channel.IsPrivate) return;

					if (e.Channel.Type == ChannelType.Text)
					{
						TextChannelsCount--;
					}
					else if (e.Channel.Type == ChannelType.Voice)
					{
						VoiceChannelsCount--;
					}
				}
				catch { }
			};
		}

		public TimeSpan GetUptime() => DateTime.Now - Process.GetCurrentProcess().StartTime;

		public string GetUptimeString()
		{
			var time = GetUptime();
			return time.Days + " days, " + time.Hours + " hours, and " + time.Minutes + " minutes.";
		}

		public Task LoadStats() =>
				Task.Run(() =>
				{
					var songs = MusicModule.MusicPlayers.Count(mp => mp.Value.CurrentSong != null);
					var sb = new System.Text.StringBuilder();
					sb.AppendLine("`Author: Kwoth` `Library: Discord.Net`");
					sb.AppendLine($"`Bot Version: {BotVersion}`");
					sb.AppendLine($"`Bot id: {NadekoBot.Client.CurrentUser.Id}`");
					sb.Append("`Owners' Ids:` ");
					sb.AppendLine("`" + String.Join(", ", NadekoBot.Creds.OwnerIds) + "`");
					sb.AppendLine($"`Uptime: {GetUptimeString()}`");
					sb.Append($"`Servers: {ServerCount}");
					sb.Append($" | TextChannels: {TextChannelsCount}");
					sb.AppendLine($" | VoiceChannels: {VoiceChannelsCount}`");
					sb.AppendLine($"`Commands Ran this session: {commandsRan}`");
					sb.AppendLine($"`Message queue size: {NadekoBot.Client.MessageQueue.Count}`");
					sb.Append($"`Greeted {ServerGreetCommand.Greeted} times.`");
					sb.AppendLine($" `| Playing {songs} songs, .SnPl(songs) {MusicModule.MusicPlayers.Sum(kvp => kvp.Value.Playlist.Count)} queued.`");
					sb.AppendLine($"`Messages: {messageCounter} ({messageCounter / (double)GetUptime().TotalSeconds:F2}/sec)`  `Heap: {Heap(false)}`");
					statsCache = sb.ToString();
				});

		public async Task ShowStats() =>
		await Task.Run(() =>
		{
			var songs = MusicModule.MusicPlayers.Count(mp => mp.Value.CurrentSong != null);
			log.Debug($"Bot Version: {BotVersion}");
			log.Debug($"Bot id: {NadekoBot.Client.CurrentUser.Id}");
			log.Debug("Owners' Ids:");
			log.Debug(String.Join(", ", NadekoBot.Creds.OwnerIds));
			log.Debug($"Uptime: {GetUptimeString()}");
			log.Debug($"Servers: {ServerCount}");
			log.Debug($"Text Channels: {TextChannelsCount}");
			log.Debug($"Voice Channels: {VoiceChannelsCount}");
			log.Debug($"Commands run this session: {commandsRan}");
			log.Debug($"Message queue size: {NadekoBot.Client.MessageQueue.Count}");
			log.Debug($"Playing {songs} songs, .SnPl(songs) {MusicModule.MusicPlayers.Sum(kvp => kvp.Value.Playlist.Count)} queued.");
			log.Debug($"Messages: {messageCounter} ({messageCounter / (double)GetUptime().TotalSeconds:F2}/sec) Heap: {Heap(false)}");
			log.Info(string.Empty);
		});

		public string Heap(bool pass = true) => Math.Round((double)GC.GetTotalMemory(pass) / 1.MiB(), 2).ToString();

		public async Task<string> GetStats()
		{
			if (statsStopwatch.Elapsed.Seconds < 4 && !string.IsNullOrWhiteSpace(statsCache)) return statsCache;
			await LoadStats().ConfigureAwait(false);
			statsStopwatch.Restart();
			return statsCache;
		}

		private async Task StartCollecting()
		{
			var statsSw = new Stopwatch();
			while (true)
			{
				await Task.Delay(new TimeSpan(0, 30, 0)).ConfigureAwait(false);
				statsSw.Start();

				try
				{
					var onlineUsers = await Task.Run(() => NadekoBot.Client.Servers.Sum(x => x.Users.Count())).ConfigureAwait(false);
					var realOnlineUsers = await Task.Run(() => NadekoBot.Client.Servers
																															.Sum(x => x.Users.Count(u => u.Status == UserStatus.Online)))
																															.ConfigureAwait(false);

					var connectedServers = NadekoBot.Client.Servers.Count();

					Classes.DbHandler.Instance.Connection.Insert(new DataModels.Stats
					{
						OnlineUsers = onlineUsers,
						RealOnlineUsers = realOnlineUsers,
						Uptime = GetUptime(),
						ConnectedServers = connectedServers,
						DateAdded = DateTime.Now
					});

					statsSw.Stop();
					log.Warn($"Stats collection finished in {statsSw.Elapsed.TotalSeconds}s");
					statsSw.Reset();
				}
				catch
				{
					log.Error("DB Exception in stats collecting.");
					break;
				}
			}
		}

		private static ConcurrentDictionary<ulong, DateTime> commandTracker = new ConcurrentDictionary<ulong, DateTime>();

		private void CommandService_CommandFinished(object sender, CommandEventArgs e)
		{

			DateTime dt;
			if (!commandTracker.TryGetValue(e.Message.Id, out dt)) return;

			try
			{
				if (e is CommandErrorEventArgs)
				{
					var er = e as CommandErrorEventArgs;

					if (er.ErrorType == CommandErrorType.Exception)
					{
						File.AppendAllText("errors.txt", $@"Command: {er.Command} {er.Exception} -------------------------------------");
						log.Error($"({e.Server?.Name ?? "Private"}) #{e.Channel.Name} @{e.User.Name} {e.Command.Text} error {(DateTime.UtcNow - dt).TotalSeconds}s");
					}
				}
				else
				{
					log.Warn($"({e.Server?.Name ?? "Private"}) #{e.Channel.Name} @{e.User.Name} {e.Command.Text} {(DateTime.UtcNow - dt).TotalSeconds}s");
				}
			}
			catch { }
		}

		private async void StatsCollector_RanCommand(object sender, CommandEventArgs e)
		{
			commandTracker.TryAdd(e.Message.Id, DateTime.UtcNow);
			//log.Warn($"({e.Server?.Name ?? "Private"}) #{e.Channel.Name} @{e.User.Name} {e.Command.Text} started");

			await Task.Run(() =>
			{
				try
				{
					commandsRan++;

					Classes.DbHandler.Instance.Connection.Insert(new DataModels.Command
					{
						ServerId = (long)(e.Server?.Id ?? 0),
						ServerName = e.Server?.Name ?? "--Direct Message--",
						ChannelId = (long)e.Channel.Id,
						ChannelName = e.Channel.IsPrivate ? "--Direct Message" : e.Channel.Name,
						UserId = (long)e.User.Id,
						UserName = e.User.Name,
						CommandName = e.Command.Text,
						DateAdded = DateTime.Now
					});
				}
				catch (Exception ex)
				{
					log.Error($"Error in database write. {ex.Message}");
				}
			}).ConfigureAwait(false);
		}
	}
}
