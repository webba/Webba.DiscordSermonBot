using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Webba.DiscordSermonBot.Lib.Models;
using Webba.DiscordSermonBot.Lib.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Webba.DiscordSermonBot.Backend
{
    public class BusHandler
    {
        private readonly ILogger _logger;
        private readonly SermonRotationService _cosmosService;
        private readonly DiscordService _discordService;

        public BusHandler(ILoggerFactory loggerFactory, SermonRotationService cosmosService, DiscordService discordService)
        {
            _logger = loggerFactory.CreateLogger<BusHandler>();
            _cosmosService = cosmosService;
            _discordService = discordService;
        }

        [Function(nameof(ProcessCommand))]
        public async Task ProcessCommand([ServiceBusTrigger("bot-slash-commands", Connection = "SlashCommandBus")] SermonCommandData command)
        {
            _logger.LogInformation($"ServiceBus Triggered Slash Command Handler: {command}");

            if(command != null)
            {
                string? response = null;
                if(command.CommandType == SermonCommandType.Add)
                {
                    response = await _cosmosService.AddSermonMember(command.GuildId, command.ChannelId, command.UserId, command.Text ?? "");
                } 
                else if(command.CommandType == SermonCommandType.Remove)
                {
                    response = await _cosmosService.RemoveSermonMember(command.GuildId, command.ChannelId, command.Text ?? "");

                }
                else if(command.CommandType == SermonCommandType.Stop)
                {
                    response = await _cosmosService.StopSermon(command.GuildId, command.ChannelId);
                }
                else if(command.CommandType == SermonCommandType.List)
                {
                    response = await _cosmosService.ListSermon(command.GuildId, command.ChannelId);

                }
                else if(command.CommandType == SermonCommandType.Faith)
                {
                    response = await _cosmosService.ProcessFaithTick(command.GuildId, command.ChannelId, command.UserId, command.Text ?? "", command.Time ?? DateTimeOffset.UtcNow);
                }

                if(response != null && command.Signature != null && command.Timestamp != null && command.Body != null)
                {
                    await _discordService.RespondToInteraction(command.Signature, command.Timestamp, command.Body, response);
                }
            }
        }

        [Function(nameof(ProcessNotify))]
        public async Task ProcessNotify([ServiceBusTrigger("sermon-bot-notify", Connection = "SlashCommandBus")] NotificationMessage message)
        {
            _logger.LogInformation($"ServiceBus Triggered Notify Handler: {message}");
            if (message != null)
            {
                await _discordService.SendSermonNotification(message);
            }
        }
    }
}
