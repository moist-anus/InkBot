﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Discord.Commands;
using Lastfm.Services;
using NadekoBot.Classes;
using NadekoBot.Extensions;
using NadekoBot.Modules.LastFm.Handlers;

namespace NadekoBot.Modules.LastFm.Commands
{
    class LastFmCommands : DiscordCommand
    {
        public LastFmCommands(DiscordModule module) : base(module)
        { }

        private string Username { get; set; } = string.Empty;
        private Session Session { get; set; } = new Session(ApiKey, ApiSecret);
        private User LastFmUser { get; set; } = null;
        private Discord.User DiscordUser { get; set; } = null;
        private int ResultLimit { get; set; } = 10;
        private Timer Timer { get; set; } = null;
        private static string ApiKey { get; set; } = NadekoBot.Creds.LastFmApiKey;
        private static string ApiSecret { get; set; } = NadekoBot.Creds.LastFmApiSecret;

        internal override void Init(CommandGroupBuilder cgb)
        {
            cgb.CreateCommand(Prefix + "autoscrobble")
                .Alias("asc")
                .Description("Starts the auto scrobble display.")
                .Parameter("interval", ParameterType.Optional)
                .Do(async e =>
                {
                    await Task.Run(() =>
                    {
                        string intervalArgument = e.GetArg("interval")?.Trim();
                        int interval;

                        if (string.IsNullOrEmpty(intervalArgument))
                        {
                            interval = 5;
                        }

                        string intervalParameter = e.GetArg("interval")?.Trim();
                        

                        if (int.TryParse(intervalParameter, out interval))
                        {
                            if (interval <= 0)
                            {
                                e.Channel.SendMessage($"{interval} is an invalid interval.").ConfigureAwait(false);
                                return;
                            }

                            Timer = new Timer(interval * 60 * 1000);
                            Timer.Elapsed += (source, te) => ScrobbleToChannel(source, te, e);
                            Timer.Enabled = true;

                            e.Channel.SendMessage($"Auto scrobble display started every {interval} minute{(interval > 1 ? "s" : string.Empty)}.").ConfigureAwait(false);
                        }
                        else if (string.Equals(intervalParameter, "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            string message = Timer != null ? "Auto scrobble display stopped." : "Auto scrobble display isn't started.";

                            if (Timer != null)
                            {
                                Timer.Enabled = false;
                            }

                            e.Channel.SendMessage(message).ConfigureAwait(false);
                        }
                    });
                });

            cgb.CreateCommand(Prefix + "nowplaying")
                .Alias(Prefix + "np")
                .Description("Displays the current scrobbled track.")
                .Do(async e => await RunForValidUser(e, m =>
                {
                    var nowPlaying = LastFmUser.GetNowPlaying();
                    m.AppendLine(nowPlaying == null ? "You're not scrobbling anything." : CreateTrackMessage(nowPlaying));
                }));

            cgb.CreateCommand(Prefix + "toptracks")
                .Alias(Prefix + "tt")
                .Description("Displays the overall top tracks.")
                .Parameter("limit", ParameterType.Optional)
                .Parameter("user", ParameterType.Optional)
                .Do(async e => await RunForValidUser(e, async m =>
                {
                    await SetUserAndResultLimitParameters(e, () =>
                    {
                        LastFmUser.GetTopTracks(Period.Overall).Take(ResultLimit).ForEach(t => CreateTrackMessage(t, m));
                    });
                }));

            cgb.CreateCommand(Prefix + "topartists")
                .Alias(Prefix + "ta")
                .Description("Displays the overall top artists.")
                .Parameter("limit", ParameterType.Optional)
                .Parameter("user", ParameterType.Optional)
                .Do(async e => await RunForValidUser(e, async m =>
                {
                    await SetUserAndResultLimitParameters(e, ()  =>
                    {
                        LastFmUser.GetTopArtists(Period.Overall).Take(ResultLimit).ForEach(a => CreateArtistMessage(a, m));
                    });
                }));

            cgb.CreateCommand(Prefix + "tracksweek")
                .Description("Displays the top weekly tracks.")
                .Parameter("limit", ParameterType.Optional)
                .Parameter("user", ParameterType.Optional)
                .Do(async e => await RunForValidUser(e, async m =>
                {
                    await SetUserAndResultLimitParameters(e, () =>
                    {
                        LastFmUser.GetWeeklyTrackChart().Take(ResultLimit).ForEach(c => CreateWeeklyTrackMessage(c, m));
                    });
                }));

            cgb.CreateCommand(Prefix + "artistsweek")
                .Description("Displays the top weekly artists.")
                .Parameter("limit", ParameterType.Optional)
                .Parameter("user", ParameterType.Optional)
                .Do(async e => await RunForValidUser(e, async m =>
                {
                    await SetUserAndResultLimitParameters(e, () =>
                    {
                        LastFmUser.GetWeeklyArtistChart().Take(ResultLimit).ForEach(a => CreateWeeklyArtistMessage(a, m));
                    });
                }));

            cgb.CreateCommand(Prefix + "recent")
                .Description("Displays recent scrobbles.")
                .Parameter("limit", ParameterType.Optional)
                .Parameter("user", ParameterType.Optional)
                .Do(async e => await RunForValidUser(e, async m =>
                {
                    await SetUserAndResultLimitParameters(e, () =>
                    {
                        LastFmUser.GetRecentTracks(ResultLimit).ForEach(t => CreateTrackMessage(t, m));
                    });
                }));

            cgb.CreateCommand(Prefix + "similar")
                .Alias(Prefix + "sim")
                .Description("Displays similar artists.")
                .Parameter("artist", ParameterType.Required)
                .Do(async e => await RunForValidUser(e, m =>
                {
                    new Artist(e.GetArg("artist")?.Trim(), Session).GetSimilar().Take(10).ForEach(s => m.AppendLine($"{s.Name}"));
                }));

            cgb.CreateCommand(Prefix + "setusername")
                .Alias(Prefix + "su")
                .Description("Sets a last.fm username.")
                .Parameter("lastFmUsername", ParameterType.Required)
                .Do(async e =>
                {
                    var lastFmUsername = e.GetArg("lastFmUsername")?.Trim();
                    await LastFmUserHandler.AssociateUsername(e, lastFmUsername);
                });

            cgb.CreateCommand(Prefix + "getusername")
                .Alias(Prefix + "gu")
                .Description("Displays the set last.fm username.")
                .Parameter("user", ParameterType.Optional)
                .Do(async e =>
                {
                    var lookupUser = e.GetArg("user")?.Trim();

                    if (string.IsNullOrEmpty(lookupUser))
                    {
                        await LastFmUserHandler.DisplayUsername(e);
                    }
                    else
                    {
                        var users = e.Server.FindUsers(lookupUser, true);
                        string message;

                        if (users.Count() == 1)
                        {
                            var username = await LastFmUserHandler.GetUsername(Convert.ToInt64(users.First().Id));
                            message = $"{lookupUser}'s last.fm account is {username}.";
                        }
                        else
                        {
                            message = $"No last.fm account information found for {lookupUser}.";
                        }

                        await e.Channel.SendMessage(message).ConfigureAwait(false);
                    }
                });
        }

        private static async void ScrobbleToChannel(object source, ElapsedEventArgs te, CommandEventArgs e)
        {
            var message = new StringBuilder();

            LastFmUserHandler.GetScrobblers().Result.ForEach(s =>
            {
                var track = new User(s.LastFmUsername, new Session(ApiKey, ApiSecret)).GetNowPlaying();

                if (track != null)
                {
                    message.AppendLine($"{e.Server.GetUser(Convert.ToUInt64(s.DiscordUserId)).Name} is playing: {CreateTrackMessage(track)}");
                }
            });

            if (!string.IsNullOrEmpty(message.ToString()))
            {
                await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
            }
        }

        private async Task SetUserAndResultLimitParameters(CommandEventArgs e, Action action)
        {
            var userArgument = e.GetArg("user")?.Trim();
            var limitArgument = e.GetArg("limit")?.Trim();

            if (string.IsNullOrEmpty(userArgument))
            {
                DiscordUser = e.User;
            }
            else
            {
                var user = e.Server.FindUsers(userArgument, true).FirstOrDefault();

                if (user != null)
                {
                    DiscordUser = user;
                }
                else
                {
                    await e.Channel.SendMessage($"No last.fm user information found for {userArgument}.");
                    return;
                }
            }

            int resultLimit;

            if (!string.IsNullOrEmpty(limitArgument))
            {
                ResultLimit = int.TryParse(limitArgument, out resultLimit) ? resultLimit : 10;
            }

            action();
        }

        private async Task RunForValidUser(CommandEventArgs e, Action<StringBuilder> action)
        {
            bool isValidUser = await Task<bool>.Run(async () =>
            {
                if (e.User == null)
                {
                    return false;
                }

                Username = await LastFmUserHandler.GetUsername(Convert.ToInt64(e.User.Id));

                if (string.IsNullOrEmpty(Username))
                {
                    LastFmUser = null;
                    await e.Channel.SendMessage("Last.fm username not set.").ConfigureAwait(false);
                    return false;
                }
                else
                {
                    LastFmUser = new User(Username, Session);
                    return true;
                }
            }).ConfigureAwait(false);

            if (isValidUser)
            {
                var message = new StringBuilder();
                action(message);
                await e.Channel.SendMessage(message.ToString()).ConfigureAwait(false);
            }
        }

        private static string CreateTrackMessage(Track track)
        {
            return $":musical_note: {track.Artist} - {track.Title} :musical_note:";
        }

        private static void CreateTrackMessage(Track track, StringBuilder message)
        {
            message.AppendLine(CreateTrackMessage(track));
        }

        private static void CreateTrackMessage(TopTrack topTrack, StringBuilder message)
        {
            message.AppendLine($"{topTrack.Item.Artist} - {topTrack.Item.Title} : {topTrack.Weight} plays");
        }

        private static void CreateWeeklyArtistMessage(WeeklyArtistChartItem item, StringBuilder message)
        {
            message.AppendLine($"{item.Artist} : {item.Playcount} plays");
        }

        private static void CreateWeeklyTrackMessage(WeeklyTrackChartItem item, StringBuilder message)
        {
            message.AppendLine($"{item.Track.Artist} - {item.Track.Title} : {item.Playcount} plays");
        }

        private static void CreateArtistMessage(TopArtist topArtist, StringBuilder message)
        {
            message.AppendLine($"{topArtist.Item.Name} : {topArtist.Weight} plays");
        }
    }
}
