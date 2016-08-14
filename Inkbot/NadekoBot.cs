using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Modules;
using NadekoBot.Classes.Help.Commands;
using NadekoBot.Classes.JSONModels;
using NadekoBot.Modules.Administration;
using NadekoBot.Modules.ClashOfClans;
using NadekoBot.Modules.Conversations;
using NadekoBot.Modules.CustomReactions;
using NadekoBot.Modules.Gambling;
using NadekoBot.Modules.Games;
using NadekoBot.Modules.Games.Commands;
using NadekoBot.Modules.Help;
using NadekoBot.Modules.Music;
using NadekoBot.Modules.NSFW;
using NadekoBot.Modules.Permissions;
using NadekoBot.Modules.Permissions.Classes;
using NadekoBot.Modules.Pokemon;
using NadekoBot.Modules.Searches;
using NadekoBot.Modules.Translator;
using NadekoBot.Modules.Trello;
using NadekoBot.Modules.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Modules.LastFm;
using NadekoBot.Modules.Splatoon;
using Inkbot.Modules.Stats.Handlers;
using log4net;

namespace NadekoBot
{
	public class NadekoBot
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(NadekoBot));

		public static DiscordClient Client { get; private set; }

		public static Credentials Creds { get; set; }

		public static Configuration Config { get; set; }

		public static LocalizedStrings Locale { get; set; } = new LocalizedStrings();

		public static string BotMention { get; set; } = "";

		public static bool Ready { get; set; } = false;

		public static Action OnReady { get; set; } = delegate { };

		private static List<Channel> OwnerPrivateChannels { get; set; }

		private static void Main()
		{
			Console.OutputEncoding = Encoding.Unicode;
			log.Warn($"Inkbot started.");

			try
			{
				File.WriteAllText("data/config_example.json", JsonConvert.SerializeObject(new Configuration(), Formatting.Indented));

				if (!File.Exists("data/config.json"))
				{
					File.Copy("data/config_example.json", "data/config.json");
				}

				File.WriteAllText("credentials_example.json", JsonConvert.SerializeObject(new Credentials(), Formatting.Indented));
			}
			catch
			{
				log.Error("Failed writing credentials_example.json or data/config_example.json.");
			}

			try
			{
				Config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText("data/config.json"));
				Config.Quotes = JsonConvert.DeserializeObject<List<Quote>>(File.ReadAllText("data/quotes.json"));
				Config.PokemonTypes = JsonConvert.DeserializeObject<List<PokemonType>>(File.ReadAllText("data/PokemonTypes.json"));
			}
			catch (Exception ex)
			{
				log.Error("Failed to load credentials.");
				log.Error(ex);
				Console.ReadKey();

				return;
			}

			try
			{
				Creds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText("credentials.json"));
			}
			catch (Exception ex)
			{
				log.Error($"Failed to load credentials from credentials.json. {ex.Message}");
				Console.ReadKey();

				return;
			}

			if (string.IsNullOrWhiteSpace(Creds.Token))
			{
				Console.WriteLine("Token blank. Please enter your bot's token:");
				Creds.Token = Console.ReadLine();
			}

			log.Debug(string.IsNullOrWhiteSpace(Creds.GoogleAPIKey)
					? "No google api key found. You will not be able to use music and links won't be shortened."
					: "Google API key found.");

			log.Debug(string.IsNullOrWhiteSpace(Creds.TrelloAppKey)
					? "No trello appkey found. You will not be able to use trello commands."
					: "Trello app key found..");

			log.Debug(Config.ForwardMessages != true
					? "Not forwarding messages."
					: "Forwarding private messages to owner.");

			log.Debug(string.IsNullOrWhiteSpace(Creds.SoundCloudClientID)
					? "No soundcloud Client ID found. Soundcloud streaming is disabled."
					: "SoundCloud streaming enabled.");

			//log.Info(string.IsNullOrWhiteSpace(Creds.OsuAPIKey)
			//		? "No osu! api key found. Song & top score lookups will not work. User lookups still available."
			//		: "osu! API key provided.");

			log.Info(string.Empty);

			BotMention = $"<@{Creds.BotId}>";

			Client = new DiscordClient(new DiscordConfigBuilder()
			{
				MessageCacheSize = 10,
				ConnectionTimeout = 180000,
				LogLevel = LogSeverity.Warning,
				LogHandler = (s, e) => log.Warn($"Severity: {e.Severity} Message: {e.Message} ExceptionMessage: {e.Exception?.Message ?? "-"}"),
			});

			var commandService = new CommandService(new CommandServiceConfigBuilder
			{
				AllowMentionPrefix = false,
				CustomPrefixHandler = m => 0,
				HelpMode = HelpMode.Disabled,
				ErrorHandler = async (s, e) =>
				{
					if (e.ErrorType != CommandErrorType.BadPermissions) return;
					if (string.IsNullOrWhiteSpace(e.Exception?.Message)) return;

					try
					{
						await e.Channel.SendMessage(e.Exception.Message).ConfigureAwait(false);
					}
					catch { }
				}
			});

			Client.MessageReceived += Client_MessageReceived;
			Client.AddService<CommandService>(commandService);
			var modules = Client.AddService<ModuleService>(new ModuleService());

			Client.AddService<AudioService>(new AudioService(new AudioServiceConfigBuilder()
			{
				Channels = 2,
				EnableEncryption = false,
				Bitrate = 128,
			}));

			modules.Add(new HelpModule(), "Help", ModuleFilter.None);
			modules.Add(new AdministrationModule(), "Administration", ModuleFilter.None);
			modules.Add(new UtilityModule(), "Utility", ModuleFilter.None);
			modules.Add(new PermissionModule(), "Permissions", ModuleFilter.None);
			modules.Add(new Conversations(), "Conversations", ModuleFilter.None);
			//modules.Add(new GamblingModule(), "Gambling", ModuleFilter.None);
			modules.Add(new GamesModule(), "Games", ModuleFilter.None);
			modules.Add(new MusicModule(), "Music", ModuleFilter.None);
			modules.Add(new SearchesModule(), "Searches", ModuleFilter.None);
			modules.Add(new NSFWModule(), "NSFW", ModuleFilter.None);
			//modules.Add(new ClashOfClansModule(), "ClashOfClans", ModuleFilter.None);
			modules.Add(new PokemonModule(), "Pokegame", ModuleFilter.None);
			modules.Add(new TranslatorModule(), "Translator", ModuleFilter.None);
			modules.Add(new CustomReactionsModule(), "Customreactions", ModuleFilter.None);
			modules.Add(new SplatoonModule(), "Splatoon", ModuleFilter.None);
			if (!string.IsNullOrWhiteSpace(Creds.TrelloAppKey)) modules.Add(new TrelloModule(), "Trello", ModuleFilter.None);
			if (!string.IsNullOrEmpty(Creds.LastFmApiKey)) modules.Add(new LastFmModule(), "LastFm", ModuleFilter.None);

			Client.ExecuteAndWait(async () =>
			{
				try
				{
					await Client.Connect(Creds.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					log.Error($"Token is wrong. Don't set a token if you don't have an official BOT account.");
					log.Error(ex);
					Console.ReadKey();

					return;
				}

				await Task.Delay(1000).ConfigureAwait(false);
				await NadekoStats.Instance.ShowStats().ConfigureAwait(false);

				OwnerPrivateChannels = new List<Channel>(Creds.OwnerIds.Length);

				foreach (var id in Creds.OwnerIds)
				{
					try
					{
						OwnerPrivateChannels.Add(await Client.CreatePrivateChannel(id).ConfigureAwait(false));
					}
					catch
					{
						log.Warn($"Could not create private channel with owner {id} listed in credentials.json");
					}
				}

				Client.ClientAPI.SendingRequest += (s, e) =>
							{
								var request = e.Request as Discord.API.Client.Rest.SendMessageRequest;
								if (request == null) return;

								request.Content = request.Content?.Replace("@everyone", "@everyοne").Replace("@here", "@һere") ?? "_error_";

								if (string.IsNullOrWhiteSpace(request.Content)) e.Cancel = true;
							};

				PermissionsHandler.Initialize();
				NadekoBot.Ready = true;
				NadekoBot.OnReady();
			});

			log.Info($"Exiting...");
			Console.ReadKey();
		}

		public static bool IsOwner(ulong id) => Creds.OwnerIds.Contains(id);

		public static async Task SendMessageToOwner(string message)
		{
			if (Config.ForwardMessages && OwnerPrivateChannels.Any())
			{
				if (Config.ForwardToAllOwners)
				{
					OwnerPrivateChannels.ForEach(async c => { try { await c.SendMessage(message).ConfigureAwait(false); } catch { } });
				}
				else
				{
					var c = OwnerPrivateChannels.FirstOrDefault();

					if (c != null) await c.SendMessage(message).ConfigureAwait(false);
				}
			}
		}

		private static bool repliedRecently = false;

		private static async void Client_MessageReceived(object sender, MessageEventArgs e)
		{
			if (e.Server != null && e.Channel != null && e.User != null)
			{
				long serverId = Convert.ToInt64(e.Server.Id);
				long channelId = Convert.ToInt64(e.Channel.Id);
				long userId = Convert.ToInt64(e.User.Id);

				if (userId != Convert.ToInt64(Client.CurrentUser.Id))
				{
					await StatsMessageHandler.SaveMessage(serverId, channelId, userId, e.Message.Text);

					int messageCount = await Task.Run(() =>
					{
						return StatsMessageHandler.GetMessageCount(serverId);
					});

					log.Info($"({e.Server.Name}) #{e.Channel.Name} @{e.User.Name} {e.Message.Text} ({messageCount})");
				}
			}

			try
			{
				if (e.Server != null || e.User.Id == Client.CurrentUser.Id) return;
				if (PollCommand.ActivePolls.SelectMany(kvp => kvp.Key.Users.Select(u => u.Id)).Contains(e.User.Id)) return;
				if (ConfigHandler.IsBlackListed(e)) return;

				if (Config.ForwardMessages && !NadekoBot.Creds.OwnerIds.Contains(e.User.Id) && OwnerPrivateChannels.Any())
				{
					await SendMessageToOwner(e.User + ": ```\n" + e.Message.Text + "\n```").ConfigureAwait(false);
				}

				if (repliedRecently) return;
				repliedRecently = true;

				if (e.Message.RawText != NadekoBot.Config.CommandPrefixes.Help + "h")
				{
					await e.Channel.SendMessage(HelpCommand.DMHelpString).ConfigureAwait(false);
				}

				await Task.Delay(2000).ConfigureAwait(false);
				repliedRecently = false;
			}
			catch { }
		}
	}
}
