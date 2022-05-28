using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Services
{
    public class DiscordServiceOptions
    {
        public const string PublicKeyEnv = "DISCORD_BOT_PUBKEY";
        public const string TestGuildEnv = "DISCORD_BOT_TEST_GUILD_ID";

        public DiscordServiceOptions(string publicKey, string testGuild)
        {
            PublicKey = publicKey;
            TestGuild = ulong.Parse(testGuild);
        }

        public string PublicKey { get; set; }
        public ulong TestGuild { get; set; }
    }
}
