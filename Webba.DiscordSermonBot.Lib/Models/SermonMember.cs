using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class SermonMember
    {
        [Key]
        public string? Id { get; set; }

        public string? RotationId { get; set; }

        public string? CharacterName { get; set; }

        public ulong UserId { get; set; }

        public DateTimeOffset? LastFatihTime { get; set; }

        public DateTimeOffset? NextSermonTime { get; set; }
    }
}
