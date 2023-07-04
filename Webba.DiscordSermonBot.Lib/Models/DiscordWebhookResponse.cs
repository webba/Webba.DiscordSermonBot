using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class DiscordWebhookResponse
    {
        public bool Authorized { get; set; }

        public string? Response { get; set; }

        public SermonCommandData? CommandData { get; set; }
    }
}
