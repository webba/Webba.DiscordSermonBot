using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Models
{
    public class SermonMemberDAO
    {
        public SermonMemberDAO(string name, ulong user)
        {
            Name = name;
            User = user;
        }

        public string Name { get; set; }

        public ulong User { get; set; }
    }
}
