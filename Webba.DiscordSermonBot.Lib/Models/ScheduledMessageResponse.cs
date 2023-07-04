using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class ScheduledMessageResponse
    {
        public ScheduledMessageResponse(long id, string? characterName, DateTimeOffset? messageTime)
        {
            Id = id;
            CharacterName = characterName;
            MessageTime = messageTime;
        }

        public long Id { get; set; }

        public string? CharacterName { get; set; }

        public DateTimeOffset? MessageTime { get; set; }
    }
}
