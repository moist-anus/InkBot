using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.DataModels;

namespace NadekoBot.Modules.LastFm.Models
{
    internal class LastFmScrobble : IDataModel
    {
        public LastFmScrobble(long serverId, long userId, string artist, string track, DateTime scrobbled)
        {
            DiscordServerId = serverId;
            DiscordUserId = userId;
            Artist = artist;
            Track = track;
            Scrobbled = scrobbled;
        }

        public LastFmScrobble()
        { }

        public long DiscordServerId { get; set; }

        public long DiscordUserId { get; set; }

        public string Artist { get; set; }

        public string Track { get; set; }

        public DateTime Scrobbled { get; set; }
    }
}
