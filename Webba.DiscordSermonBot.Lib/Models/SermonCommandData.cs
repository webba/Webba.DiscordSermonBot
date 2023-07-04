using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public enum SermonCommandType
    {
        Add, 
        Remove,
        List,
        Faith,
        Stop
    }

    public class SermonCommandData
    {
        public ulong InteractionId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
        public SermonCommandType CommandType { get; set; }
        public string? Text { get; set; }
        public DateTimeOffset? Time { get; set; }

        public string? Signature { get; set; }
        public string? Timestamp { get; set; }
        public byte[]? Body { get; set; }
    }
}
