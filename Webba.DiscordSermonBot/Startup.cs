using Azure.Messaging.ServiceBus;
using Discord.Rest;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webba.DiscordSermonBot.Lib.Services;

[assembly: FunctionsStartup(typeof(Webba.DiscordSermonBot.Startup))]
namespace Webba.DiscordSermonBot
{
    public class Startup: FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            builder.ConfigurationBuilder
                .AddUserSecrets<Startup>()
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = builder.GetContext().Configuration;

            builder.Services
               .AddSingleton<DiscordRestClient>(c => {
                   DiscordRestClient discordRestClient = new();
                   discordRestClient.LoginAsync(Discord.TokenType.Bot, config["DISCORD_BOT_TOKEN"]).Wait();
                   return discordRestClient;
               });

            builder.Services.AddSingleton(c => new CosmosClient(config["COSMOS_CONNECTION_STRING"]));
            builder.Services.AddSingleton(c => new ServiceBusClient(config["SERVICE_BUS_CONNECTION_STRING"]));

            builder.Services.AddSingleton<DiscordServiceOptions>(dc => 
                new DiscordServiceOptions(config[DiscordServiceOptions.PublicKeyEnv], config[DiscordServiceOptions.TestGuildEnv])
            );

            builder.Services.AddSingleton<DiscordService>();

            builder.Services.AddSingleton<SermonCosmosServiceOptions>(dc =>
                new SermonCosmosServiceOptions(
                    config[SermonCosmosServiceOptions.DatabaseNameEnv], 
                    config[SermonCosmosServiceOptions.ContainerNameEnv],
                    config[SermonCosmosServiceOptions.ServiceBusQueueEnv]
                )
            );

            builder.Services.AddSingleton<SermonCosmosService>();
        }
    }
}
