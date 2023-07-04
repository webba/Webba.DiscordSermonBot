using Azure.Messaging.ServiceBus;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webba.DiscordSermonBot.Lib.Data;
using Webba.DiscordSermonBot.Lib.Models;

namespace Webba.DiscordSermonBot.Lib.Services
{
    public class SermonRotationService
    {
        private readonly SermonBotDbContext _context;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ILogger<SermonRotationService> _logger;

        public const string NotifyQueue = "sermon-bot-notify";

        public SermonRotationService(SermonBotDbContext context, ServiceBusClient serviceBusClient, ILoggerFactory loggerFactory)
        {
            _context = context;
            _serviceBusClient = serviceBusClient;
            _logger = loggerFactory.CreateLogger<SermonRotationService>();
        }

        private async Task<SermonRotation?> GetSermonRotationByGuildAndChannelId(ulong guildId, ulong channelId) {
            return await _context.Rotations.Include(r => r.Members).FirstOrDefaultAsync(r => r.GuildId == guildId && r.ChannelId == channelId);
        } 

        private static DateTimeOffset GetMemberSlot(SermonRotation rotation, SermonMember member)
        {
            DateTimeOffset next = DateTimeOffset.UtcNow.AddSeconds(10);
            if (rotation.LastSermonTime.HasValue && rotation.LastSermonTime.Value.AddMinutes(30) > next)
            {
                next = rotation.LastSermonTime.Value.AddMinutes(30);
            }

            if (member.LastFatihTime.HasValue && member.LastFatihTime.Value.AddHours(3) > next)
            {
                next = member.LastFatihTime.Value.AddHours(3);
            }

            foreach (var oldMember in rotation.Members.OrderBy(m => m.NextSermonTime))
            {
                if (oldMember.NextSermonTime.HasValue && next > oldMember.NextSermonTime.Value.AddMinutes(-30))
                {
                    next = oldMember.NextSermonTime.Value.AddMinutes(30);
                }
            }
            return next;
        }

        private async Task AddMembersToRotation(SermonRotation rotation, IList<SermonMemberDAO> memberDAOs)
        {
            foreach (var member in memberDAOs)
            {
                if (!rotation.Members.Any(s => member.Name == s.CharacterName))
                {
                    var newMember = new SermonMember()
                    {
                        Id = Guid.NewGuid().ToString(),
                        CharacterName = member.Name,
                        UserId = member.User

                    };
                    await _context.Members.AddAsync(newMember);
                    await _context.SaveChangesAsync();
                    rotation.Members.Add(newMember);
                }
            }
            await ReorganiseRotation(rotation);
        }

        private async Task ReorganiseRotation(SermonRotation rotation)
        {
            // Find process order
            var existingMembers = rotation.Members.Where(m => m.NextSermonTime.HasValue).OrderBy(m => m.NextSermonTime).ToList();
            var newMembers = rotation.Members.Where(m => !m.NextSermonTime.HasValue).ToList();

            // Clear all timers
            rotation.Members.ToList().ForEach(m => m.NextSermonTime = null);

            foreach (var member in Enumerable.Concat(existingMembers, newMembers))
            {
                member.NextSermonTime = GetMemberSlot(rotation, member);
                _context.Members.Update(member);
            }

            var message = await ScheduleNextMessage(rotation);

            if(message != null)
            {
                rotation.ScheduledMessageId = message.Id;
                rotation.ScheduledMessageTime = message.MessageTime;
                rotation.ScheduledMessageCharacter = message.CharacterName;
            }

            _context.Rotations.Update(rotation);
            await _context.SaveChangesAsync();
        }

        public static DateTimeOffset GetNextSermonTime(SermonRotation rotation)
        {
            DateTimeOffset dt = DateTimeOffset.UtcNow;

            if (rotation.LastSermonTime != null)
            {
                if (dt < rotation.LastSermonTime.Value.AddMinutes(30))
                {
                    dt = rotation.LastSermonTime.Value.AddMinutes(30);
                }

                if (rotation.Members != null && rotation.Members.Any())
                {
                    if (rotation.Members.First().LastFatihTime != null)
                    {
                        if (dt < rotation.Members.First().LastFatihTime!.Value.AddHours(3))
                        {
                            dt = rotation.Members.First().LastFatihTime!.Value.AddHours(3);
                        }
                    }
                }
            }

            return dt;
        }

        private static string GetSermonMemberQueueString(SermonMember member)
        {
            if (member.NextSermonTime.HasValue)
            {
                return $"{member.CharacterName} (<t:{member.NextSermonTime.Value.ToUnixTimeSeconds()}:R>)";

            }
            return $"{member.CharacterName}";
        }

        public static string GetSermonQueueString(SermonRotation rotation)
        {
            if (rotation.Members != null && rotation.Members.Any())
            {
                return $"The current order is: {string.Join(", ", rotation.Members.OrderBy(m => m.NextSermonTime).Select(GetSermonMemberQueueString))}";
            }
            else
            {
                return "No sermons";
            }
        }

        private async Task CancelScheduledMessage(SermonRotation rotation)
        {
            if (rotation.ScheduledMessageId != null && rotation.ScheduledMessageTime.HasValue && rotation.ScheduledMessageTime.Value > DateTimeOffset.UtcNow)
            {
                try
                {
                    var sender = _serviceBusClient.CreateSender(NotifyQueue);
                    await sender.CancelScheduledMessageAsync(rotation.ScheduledMessageId.Value);
                } 
                catch (Exception ex)
                {
                    _logger.LogDebug("Cancel Exception: {Message}", ex.Message);
                }
            }
        }

        private static bool IsMessageSame(SermonRotation rotation, SermonMember member)
        {
            return (rotation.ScheduledMessageTime.HasValue &&
                member.NextSermonTime.HasValue &&
                rotation.ScheduledMessageTime.Value == member.NextSermonTime.Value) &&
                (rotation.ScheduledMessageCharacter == member.CharacterName);
        }

        private async Task<ScheduledMessageResponse?> ScheduleNextMessage(SermonRotation rotation)
        {

            var member = rotation.Members.Where(m => m.NextSermonTime.HasValue).MinBy(m => m.NextSermonTime);

            if (member != null && member.NextSermonTime.HasValue)
            {
                if (!IsMessageSame(rotation, member))
                {

                    await CancelScheduledMessage(rotation);
                    var message = new NotificationMessage(rotation.GuildId, rotation.ChannelId, member.UserId,
                        $"Its {member.CharacterName}'s sermon time! {GetSermonQueueString(rotation)}");

                    var serviceMessage = new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(message));

                    var time = member.NextSermonTime.Value;

                    var sender = _serviceBusClient.CreateSender(NotifyQueue);
                    var id = await sender.ScheduleMessageAsync(serviceMessage, time);

                    return new(id, member.CharacterName, time);
                }
            }
            else
            {
                await CancelScheduledMessage(rotation);
            }
            return null;
        }

        public async Task<string> AddSermonMember(ulong guildId, ulong channelId, ulong userId, string memberText)
        {
            var members = memberText.Split(',').Select(s => new SermonMemberDAO(s.Trim(), userId)).ToList();
            if (members.Any())
            {
                var item = await GetSermonRotationByGuildAndChannelId(guildId, channelId);

                if (item == null)
                {
                    SermonRotation rotation = new()
                    {
                        Id = Guid.NewGuid().ToString(),
                        GuildId = guildId,
                        ChannelId = channelId
                    };
                    await _context.Rotations.AddAsync(rotation);
                    await _context.SaveChangesAsync();

                    await AddMembersToRotation(rotation, members);
                    return $"Added those! {GetSermonQueueString(rotation)}";
                }
                else
                {
                    await AddMembersToRotation(item, members);
                    return $"Added those! {GetSermonQueueString(item)}";
                }
            }
            return "No members to add";
        }

        public async Task<string> RemoveSermonMember(ulong guildId, ulong channelId, string member)
        {
            if (!String.IsNullOrEmpty(member))
            {
                var rotation = await GetSermonRotationByGuildAndChannelId(guildId, channelId);

                if (rotation != null)
                {
                    var memberDb = rotation.Members.FirstOrDefault(m => m.CharacterName == m.CharacterName);
                    if (memberDb != null)
                    {
                        rotation.Members.Remove(memberDb);
                        _context.Members.Remove(memberDb);

                        await ReorganiseRotation(rotation);
                        return $"Removed {member}! {GetSermonQueueString(rotation)}";
                    }
                }
                return "No members in rotation";
            }
            return "Invalid member";
        }

        public async Task<string> StopSermon(ulong guildId, ulong channelId)
        {
            var rotation = await GetSermonRotationByGuildAndChannelId(guildId, channelId);
            if (rotation != null)
            {
                await CancelScheduledMessage(rotation);
                _context.Rotations.Remove(rotation);
                await _context.SaveChangesAsync();
                return "Stopped sermon rotation";
            }
            return "No sermon rotation to stop";
        }

        public async Task<string> ListSermon(ulong guildId, ulong channelId)
        {
            var rotation = await GetSermonRotationByGuildAndChannelId(guildId, channelId);
            if (rotation != null)
            {
                return GetSermonQueueString(rotation);
            }
            return "No sermon rotation running";
        }

        private static DateTimeOffset ParseTimeFromTick(string tick,  DateTimeOffset dateTime)
        {
            var faithSplit = tick.Split("]");

            DateTimeOffset sermonTime = dateTime;

            if (faithSplit.Length == 2)
            {
                var faithTime = DateTimeOffset.ParseExact(faithSplit[0].Replace("]", ""), "hh:mm:ss", CultureInfo.InvariantCulture);

                if (faithTime.Minute > dateTime.Minute)
                {
                    sermonTime = sermonTime.AddHours(-1);
                }
                sermonTime = sermonTime.AddMinutes(faithTime.Minute - dateTime.Minute);


                if (faithTime.Second > dateTime.Second)
                {
                    sermonTime = sermonTime.AddMinutes(-1);
                }
                sermonTime = sermonTime.AddSeconds(faithTime.Second - dateTime.Second);
            }

            return sermonTime;
        }

        public async Task<string> ProcessFaithTick(ulong guildId, ulong channelId, ulong userId, string faithTick, DateTimeOffset messageTime)
        {
            var rotation = await GetSermonRotationByGuildAndChannelId(guildId, channelId);
            if (rotation != null)
            {
                var member = rotation.Members.Where(m => m.NextSermonTime.HasValue & m.UserId == userId).MinBy(m => m.NextSermonTime);
                if(member != null)
                {
                    DateTimeOffset sermonTime = ParseTimeFromTick(faithTick, messageTime);

                    member.LastFatihTime = sermonTime;
                    rotation.LastSermonTime = sermonTime;
                    member.NextSermonTime = null;

                    await ReorganiseRotation(rotation);
                    return $"Sermon from {member.CharacterName}! {GetSermonQueueString(rotation)}";
                }
                return "Member not found";
            }
            return "No sermon rotation running";
        }
    }
}
