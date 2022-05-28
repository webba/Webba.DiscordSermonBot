using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class NotificationMessage
    {
        public NotificationMessage(ulong guildId, ulong channelId, ulong user, string message)
        {
            GuildId = guildId;
            ChannelId = channelId;
            User = user;
            Message = message;
        }

        public ulong GuildId { get; set; }

        public ulong ChannelId { get; set; }

        public ulong User { get; set; }

        public string Message { get; set; }
    }
}
