using Discord;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webba.DiscordSermonBot.Lib.Models;

namespace Webba.DiscordSermonBot.Lib.Services
{
    public class DiscordService
    {
        public const string SermonCommandName = "sermon";
        public const string SermonCommandDescription = "Sermon rotation bot commands";
        public const string FaithCommandName = "faith";
        public const string FaithCommandDescription = "Sermon rotation faith tick";

        private readonly DiscordRestClient _discordRestClient;
        private readonly DiscordServiceOptions _options;
        private readonly SermonCosmosService _sermon;

        public DiscordService(DiscordRestClient discordRestClient, DiscordServiceOptions options, SermonCosmosService sermon)
        {
            _discordRestClient = discordRestClient;
            _options = options;
            _sermon = sermon;
        }

        public async Task SetupGlobalCommandAsync()
        {

            var builder = new SlashCommandBuilder()
            .WithName(SermonCommandName)
            .WithDescription(SermonCommandDescription)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("stop")
                .WithDescription("Ends the sermon rotations in this channel")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("list")
                .WithDescription("Shows the sermon rotation")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("add")
                .WithDescription("Add character(s) to the rotation")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("character")
                    .WithDescription("Character name(s), seperate multiple with spaces")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("user")
                    .WithDescription("Discord user to add someone else's character to the rotation")
                    .WithType(ApplicationCommandOptionType.User)
                    .WithRequired(false)
                ))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("remove")
                .WithDescription("Remove character(s) from the rotation")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("character")
                    .WithDescription("Character name(s), seperate multiple with spaces")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                ));

            var fbuilder = new SlashCommandBuilder()
                .WithName(FaithCommandName)
                .WithDescription(SermonCommandDescription)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("faith-tick")
                    .WithDescription("Paste your faith tick")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true));

            var commands = new ApplicationCommandProperties[] { builder.Build(), fbuilder.Build() };

            //await _discordRestClient.BulkOverwriteGuildCommands(commands, _options.TestGuild);
            await _discordRestClient.BulkOverwriteGlobalCommands(commands);
        }

        public async  Task<DiscordWebhookResponse> HandleGlobalCommand(string sig, string timestamp, byte[] body)
        {
            if(_discordRestClient.IsValidHttpInteraction(_options.PublicKey, sig, timestamp, body))
            {
                var interaction = await _discordRestClient.ParseHttpInteractionAsync(_options.PublicKey, sig, timestamp, body);

                if(interaction != null)
                {
                    if(interaction.Type == InteractionType.Ping)
                    {
                        var responseString = ((RestPingInteraction)interaction).AcknowledgePing();
                        return new() { Authorized = true, Response = responseString };
                    } 
                    else if(interaction.Type == InteractionType.ApplicationCommand)
                    {
                        var appInter = (RestSlashCommand)interaction;
                        var responseString = ""; 
                        if(appInter.Data != null && appInter.GuildId != null && appInter.ChannelId != null)
                        {
                            var data = appInter.Data;
                            if (data.Name == "sermon")
                            {
                                if (data.Options.Any())
                                {
                                    var sData = data.Options.First();
                                    if (sData.Name == "add")
                                    {
                                        if (sData.Options.Any(o => o.Name == "character"))
                                        {
                                            var text = sData.Options.First(o => o.Name == "character").Value.ToString();
                                            var userId = appInter.User.Id;

                                            var userOption = sData.Options.FirstOrDefault(o => o.Name == "user");
                                            if (userOption != null)
                                            {
                                                userId = ((RestGuildUser)userOption.Value).Id;
                                            }

                                            if (text != null) {
                                                var members = text.Split(" ").Select(s => new SermonMemberDAO(s, userId)).ToList();

                                                var str = await _sermon.AddSermonMember(appInter.GuildId.Value, appInter.ChannelId.Value, members);

                                                responseString = interaction.Respond(str);
                                            }
                                        }
                                    }
                                    else if (sData.Name == "remove")
                                    {
                                        responseString = interaction.Respond("remove test");
                                    }
                                    else if (sData.Name == "stop")
                                    {
                                        responseString = interaction.Respond("stop test");
                                    }
                                    else if (sData.Name == "list")
                                    {
                                        responseString = interaction.Respond("list test");
                                    }
                                }
                            }
                            else if (data.Name == "faith")
                            {
                                responseString = interaction.Respond("faith test");
                            }
                        }

                        if(responseString == "")
                        {
                            responseString = appInter.Respond("Something went wrong");
                        }

                        return new() { Authorized = true, Response= responseString };
                    }
                }
            }
            return new() { Authorized = false };
        }

        public async Task SendSermonNotification(ulong guildId, ulong channelId, ulong userId, string message)
        {
            var g = await _discordRestClient.GetGuildAsync(guildId);
            var channel = await g.GetTextChannelAsync(channelId);

            await channel.SendMessageAsync($"<@{userId}> {message}");
        }
    }
}
