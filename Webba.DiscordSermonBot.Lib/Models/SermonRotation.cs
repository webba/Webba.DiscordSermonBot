using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class SermonRotation
    {
        public string Id { get; set; }

        public ulong GuildId { get; set; }

        public ulong ChannelId { get; set; }

        public long? ScheduledMessageId { get; set; }

        public DateTimeOffset? LastSermonTime { get; set; }

        public IList<SermonMember>? Members { get; set; }
    }
}
