using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webba.DiscordSermonBot.Lib.Models;

namespace Webba.DiscordSermonBot.Lib.Services
{
    public class SermonCosmosService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly SermonCosmosServiceOptions _options;

        public SermonCosmosService(CosmosClient cosmosClient, ServiceBusClient serviceBusClient, SermonCosmosServiceOptions options)
        {
            _cosmosClient = cosmosClient;
            _serviceBusClient = serviceBusClient;
            _options = options;
        }

        private async Task<Container> GetContainerAsync()
        {
            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_options.DatabaseName);
            return await database.Database.CreateContainerIfNotExistsAsync(_options.ContainerName, "/GuildId");
        }

        public static (string?, IList<SermonMember>) AddMembersToRotation(IList<SermonMember>? sermonMembers, IList<SermonMemberDAO> memberDAOs)
        {
            List<SermonMember> mainList = sermonMembers?.ToList() ?? new List<SermonMember>();
            if (mainList.Any(s => memberDAOs.Any(m => m.Name == s.CharacterName)))
            {
                List<SermonMember> members = memberDAOs.Select(memberDAO => new SermonMember()
                {
                    CharacterName = memberDAO.Name,
                    User = memberDAO.User
                }).ToList();

                for (int i = 0; i < mainList.Count; i++)
                {
                    if (mainList[i].LastFatihTime != null)
                    {
                        if (mainList[i].LastFatihTime!.Value.AddHours(3) > DateTimeOffset.UtcNow.AddMinutes((i + 1) * 30))
                        {
                            if (members.Any())
                            {
                                SermonMember member = members.First();
                                mainList.Insert(i, member);
                                members.Remove(member);
                            }
                        }
                    }
                }

                if (members.Any())
                {
                    mainList.AddRange(members);
                }
                return (null, mainList);
            }
            else
            {
                return ("Member already in roatation", mainList);
            }
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

        public static string GetSermonQueueString(SermonRotation rotation)
        {
            if (rotation.Members != null && rotation.Members.Any())
            {
                return $"The current order is: {string.Join(", ", rotation.Members.Select(m => m.CharacterName))}";
            }
            else
            {
                return "No sermons";
            }
        }

        public async Task CancelScheduledMessage(SermonRotation rotation)
        {
            if (rotation.ScheduledMessageId != null)
            {
                var sender = _serviceBusClient.CreateSender(_options.ServiceBusQueue);
                await sender.CancelScheduledMessageAsync(rotation.ScheduledMessageId!.Value);
            }
        }

        public async Task<long?> ScheduleNextMessage(SermonRotation rotation)
        {
            await CancelScheduledMessage(rotation);

            if (rotation.Members != null && rotation.Members.Any())
            {
                var member = rotation.Members.First();

                var message = new NotificationMessage(rotation.GuildId, rotation.ChannelId, member.User,
                    $"Its your sermon time! {GetSermonQueueString(rotation)}");

                var serviceMessage = new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(message));

                var time = GetNextSermonTime(rotation);

                var sender = _serviceBusClient.CreateSender(_options.ServiceBusQueue);
                var id = await sender.ScheduleMessageAsync(serviceMessage, time);

                return id;
            }
            else
            {
                return null;
            }
        }

        public async Task<string?> AddSermonMember(ulong guildId, ulong channelId, IList<SermonMemberDAO> members)
        {
            if (members.Any())
            {
                var container = await GetContainerAsync();
                try
                {
                    var item = await container.ReadItemAsync<SermonRotation>(channelId.ToString(), new PartitionKey(guildId));

                    (string? error, item.Resource.Members) = AddMembersToRotation(item.Resource.Members, members);

                    if (error != null)
                    {
                        return error;
                    }

                    item.Resource.ScheduledMessageId = await ScheduleNextMessage(item.Resource);

                    await container.ReplaceItemAsync(item.Resource, item.Resource.Id, new PartitionKey(guildId));

                    return $"Added those! {GetSermonQueueString(item.Resource)}";
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    SermonRotation rotation = new SermonRotation
                    {
                        Id = channelId.ToString(),
                        GuildId = guildId,
                        ChannelId = channelId,
                        Members = members.Select(memberDAO => new SermonMember()
                        {
                            CharacterName = memberDAO.Name,
                            User = memberDAO.User
                        }).ToList()
                    };
                    rotation.ScheduledMessageId = await ScheduleNextMessage(rotation);
                    await container.CreateItemAsync(rotation, new PartitionKey(guildId));
                    return $"Added those! {GetSermonQueueString(rotation)}";
                }
            }
            return null;
        }

        public async Task<string?> RemoveSermonMember(ulong guildId, ulong channelId, string member)
        {
            var container = await GetContainerAsync();
            try
            {
                var item = await container.ReadItemAsync<SermonRotation>(channelId.ToString(), new PartitionKey(guildId));

                if (item.Resource.Members != null && item.Resource.Members.Any())
                {
                    var single = item.Resource.Members.FirstOrDefault(m => m.CharacterName == member);
                    if (single != null)
                    {
                        item.Resource.Members.Remove(single);
                    }
                }

                if (item.Resource.Members != null && item.Resource.Members.Any())
                {
                    item.Resource.ScheduledMessageId = await ScheduleNextMessage(item.Resource);
                    await container.ReplaceItemAsync(item.Resource, item.Resource.Id, new PartitionKey(guildId));
                }
                else
                {
                    await CancelScheduledMessage(item.Resource);
                    await container.DeleteItemAsync<SermonRotation>(item.Resource.Id, new PartitionKey(guildId));
                }

                return $"Removed {member}! {GetSermonQueueString(item.Resource)}";
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "No members in rotation";
            }
        }

        public async Task<string?> StopSermon(ulong guildId, ulong channelId)
        {
            var container = await GetContainerAsync();
            try
            {
                var item = await container.ReadItemAsync<SermonRotation>(channelId.ToString(), new PartitionKey(guildId));

                await CancelScheduledMessage(item.Resource);

                await container.DeleteItemAsync<SermonRotation>(item.Resource.Id, new PartitionKey(guildId));

                return "Sermon stopped";
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "No sermon to stop";
            }
        }

        public async Task<string?> ListSermon(ulong guildId, ulong channelId)
        {
            var container = await GetContainerAsync();
            try
            {
                var item = await container.ReadItemAsync<SermonRotation>(channelId.ToString(), new PartitionKey(guildId));

                return GetSermonQueueString(item.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "No sermon to stop";
            }
        }

        public async Task<string?> ProcessFaithTick(ulong guildId, ulong channelId, ulong userId, string faithTick, DateTimeOffset messageTime)
        {
            var container = await GetContainerAsync();
            try
            {
                var item = await container.ReadItemAsync<SermonRotation>(channelId.ToString(), new PartitionKey(guildId));

                if (item.Resource.Members != null && item.Resource.Members.Any())
                {
                    var character = item.Resource.Members.FirstOrDefault(m => m.User == userId);
                    var faithSplit = faithTick.Split("]");

                    DateTimeOffset sermonTime = messageTime;

                    if (faithSplit.Length == 2)
                    {
                        var faithTime = DateTimeOffset.ParseExact(faithSplit[0].Replace("]", ""), "hh:mm:ss", CultureInfo.InvariantCulture);

                        if (faithTime.Minute > messageTime.Minute)
                        {
                            sermonTime = sermonTime.AddHours(-1);
                        }
                        sermonTime = sermonTime.AddMinutes(faithTime.Minute - messageTime.Minute);


                        if (faithTime.Second > messageTime.Second)
                        {
                            sermonTime = sermonTime.AddMinutes(-1);
                        }
                        sermonTime = sermonTime.AddSeconds(faithTime.Second - messageTime.Second);
                    }

                    if (character != null)
                    {
                        item.Resource.Members.Remove(character);

                        character.LastFatihTime = sermonTime;
                        character.LastFaith = faithTick;

                        item.Resource.Members.Add(character);

                        return $"Sermon from {character.CharacterName}: \"{faithTick}\"! {GetSermonQueueString(item.Resource)}";
                    }

                    item.Resource.LastSermonTime = sermonTime;
                    item.Resource.ScheduledMessageId = await ScheduleNextMessage(item.Resource);
                    await container.ReplaceItemAsync(item.Resource, item.Resource.Id, new PartitionKey(guildId));
                }
                return $"Sermon from unknown: \"{faithTick}\"! {GetSermonQueueString(item.Resource)}";
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "No sermon to stop";
            }
        }
    }
}
