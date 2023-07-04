using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class SermonRotation
    {
        [Key]
        public string? Id { get; set; }

        public ulong GuildId { get; set; }

        public ulong ChannelId { get; set; }

        public long? ScheduledMessageId { get; set; }

        public DateTimeOffset? ScheduledMessageTime { get; set; }

        public string? ScheduledMessageCharacter { get; set; }

        public DateTimeOffset? LastSermonTime { get; set; }

        public ICollection<SermonMember> Members { get; set; } = new List<SermonMember>();
    }
}
