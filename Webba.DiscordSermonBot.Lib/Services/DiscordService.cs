using Discord;
using Discord.Rest;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
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
        public const string FaithTickOptionName = "faith-tick";
        public const string SermonAddOptionName = "add";
        public const string SermonRemoveOptionName = "remove";
        public const string SermonListOptionName = "list";
        public const string SermonStopOptionName = "stop";
        public const string SermonCharacterOptionName = "character";
        public const string SermonUserOptionName = "user";

        public const string BusQueueName = "bot-slash-commands";

        private readonly DiscordRestClient _discordRestClient;
        private readonly DiscordServiceOptions _options;
        public DiscordService(DiscordRestClient discordRestClient, IOptions<DiscordServiceOptions> options)
        {
            _options = options.Value;
            _discordRestClient = discordRestClient;
            _discordRestClient.LoginAsync(TokenType.Bot, _options.DiscordBotToken);
        }

        public DiscordService(DiscordRestClient discordRestClient, DiscordServiceOptions options)
        {
            _discordRestClient = discordRestClient;
            _options = options;
        }

        public async Task SetupGlobalCommandAsync()
        {

            var builder = new SlashCommandBuilder()
            .WithName(SermonCommandName)
            .WithDescription(SermonCommandDescription)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(SermonStopOptionName)
                .WithDescription("Ends the sermon rotations in this channel")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(SermonListOptionName)
                .WithDescription("Shows the sermon rotation")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(SermonAddOptionName)
                .WithDescription("Add character(s) to the rotation")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(SermonCharacterOptionName)
                    .WithDescription("Character name(s), seperate multiple with spaces")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true))
                )
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("remove")
                .WithDescription("Remove character(s) from the rotation")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(SermonCharacterOptionName)
                    .WithDescription("Character name(s), seperate multiple with spaces")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                ));

            var fbuilder = new SlashCommandBuilder()
                .WithName(FaithCommandName)
                .WithDescription(SermonCommandDescription)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(FaithTickOptionName)
                    .WithDescription("Paste your faith tick")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true));

            var commands = new ApplicationCommandProperties[] { builder.Build(), fbuilder.Build() };

            await _discordRestClient.BulkOverwriteGlobalCommands(commands);
        }

        private static DiscordWebhookResponse HandlePing(RestInteraction interaction)
        {
            var responseString = ((RestPingInteraction)interaction).AcknowledgePing();
            return new() { Authorized = true, Response = responseString };
        }

        private static SermonCommandType? GetSermonCommandType(string name)
        {
            if (name == SermonAddOptionName)
            {
                return SermonCommandType.Add;
            }
            else if (name == SermonRemoveOptionName)
            {
                return SermonCommandType.Remove;
            }
            else if (name == SermonStopOptionName)
            {
                return SermonCommandType.Stop;
            }
            else if (name == SermonListOptionName)
            {
                return SermonCommandType.List;
            }
            else if (name == FaithCommandName)
            {
                return SermonCommandType.Faith;
            }
            else
            {
                return null;
            }
        }

        public static SermonCommandData? ProcessCommand(RestSlashCommand slashCommand, string sig, string timestamp, byte[] body)
        {
            var interactionId = slashCommand.Id;
            var userId = slashCommand.User.Id;
            var guildId = slashCommand.GuildId;
            var channelId  = slashCommand.ChannelId;
            if (slashCommand.Data.Name == SermonCommandName && slashCommand.Data.Options.Any() && guildId != null && channelId != null)
            {
                var subCommand = slashCommand.Data.Options.First();
                var type = GetSermonCommandType(subCommand.Name);
                var text = subCommand.Options.FirstOrDefault(o => o.Name == SermonCharacterOptionName)?.Value?.ToString();

                if(type != null) {
                    return new()
                    {
                        InteractionId = interactionId,
                        GuildId = guildId.Value,
                        ChannelId = channelId.Value,
                        UserId = userId,
                        CommandType = type.Value,
                        Text = text,
                        Time = slashCommand.CreatedAt,
                        Signature = sig,
                        Timestamp = timestamp,
                        Body = body
                    }; 
                }
            }
            else if (slashCommand.Data.Name == FaithCommandName && guildId != null && channelId != null)
            {
                return new()
                {
                    InteractionId = interactionId,
                    GuildId = guildId.Value,
                    ChannelId = channelId.Value,
                    UserId = userId,
                    CommandType = SermonCommandType.Faith,
                    Text =  slashCommand.Data.Options.First(o => o.Name == FaithTickOptionName).Value.ToString(),
                    Time = slashCommand.CreatedAt,
                    Signature = sig,
                    Timestamp = timestamp,
                    Body = body
                };
            }
            return null;
        }

        private static DiscordWebhookResponse HandleApplicationCommand(RestInteraction interaction, string sig, string timestamp, byte[] body)
        {
            var slashCommand = (RestSlashCommand)interaction;
            if (slashCommand.GuildId == null || slashCommand.ChannelId == null)
            {
                return new() { Authorized = true, Response = slashCommand.Respond("This command can only be used in a server channel.") };
            }

            if (slashCommand.Data != null)
            {
                var command = ProcessCommand(slashCommand, sig, timestamp, body);
                if (command != null)
                {
                    return new() { Authorized = true, Response = slashCommand.Defer(), CommandData = command };
                }
                else
                {
                    return new()
                    {
                        Authorized = true,
                        Response = slashCommand.Respond("Invalid Command")
                    };
                }
            }
            return new() { Authorized = true, Response = slashCommand.Respond("Something went wrong") };
        }

        public async  Task<DiscordWebhookResponse> HandleGlobalCommand(string sig, string timestamp, byte[] body)
        {
            if(_discordRestClient.IsValidHttpInteraction(_options.PublicKey, sig, timestamp, body))
            {
                var interaction = await _discordRestClient.ParseHttpInteractionAsync(_options.PublicKey, sig, timestamp, body);
                
                if(interaction != null)
                {
                    if (interaction.Type == InteractionType.Ping)
                    {
                        return HandlePing(interaction);
                    }
                    else if (interaction.Type == InteractionType.ApplicationCommand)
                    {
                        return HandleApplicationCommand(interaction, sig, timestamp, body);
                    }
                }
            }
            return new() { Authorized = false };
        }

        public async Task RespondToInteraction(string sig, string timestamp, byte[] body, string message)
        {
            if (_discordRestClient.IsValidHttpInteraction(_options.PublicKey, sig, timestamp, body))
            {
                var interaction = await _discordRestClient.ParseHttpInteractionAsync(_options.PublicKey, sig, timestamp, body);

                if (interaction != null)
                {
                    if (interaction.Type == InteractionType.ApplicationCommand)
                    {
                        await interaction.ModifyOriginalResponseAsync(prop =>
                        {
                            prop.Content = message;
                        });
                    }
                }
            }
        }
        public async Task SendSermonNotification(NotificationMessage message)
        {
            await SendSermonNotification(message.GuildId, message.ChannelId, message.User, message.Message);
        }

        public async Task SendSermonNotification(ulong guildId, ulong channelId, ulong userId, string message)
        {
            var g = await _discordRestClient.GetGuildAsync(guildId);
            if(g != null)
            {
                var channel = await g.GetTextChannelAsync(channelId);

                if (channel != null)
                {
                    await channel.SendMessageAsync($"<@{userId}> {message}");
                }
            }
        }
    }
}
