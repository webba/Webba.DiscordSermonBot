using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class SermonMember
    {
        public string CharacterName { get; set; }

        public ulong User { get; set; }

        public string? LastFaith { get; set; }

        public DateTimeOffset? LastFatihTime { get; set; }
    }
}
