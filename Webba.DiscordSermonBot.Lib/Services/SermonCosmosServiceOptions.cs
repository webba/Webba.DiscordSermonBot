using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webba.DiscordSermonBot.Lib.Services
{
    public class SermonCosmosServiceOptions
    {
        public const string DatabaseNameEnv = "COSMOS_DATABASE";
        public const string ContainerNameEnv = "COSMOS_SERMON_CONTAINER";
        public const string ServiceBusQueueEnv = "SERVICE_BUS_QUEUE";

        public SermonCosmosServiceOptions(string databaseName, string containerName, string serviceBusQueue)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
            ServiceBusQueue = serviceBusQueue;
        }

        public string DatabaseName { get; set; }
        public string ContainerName { get; set; }
        public string ServiceBusQueue { get; set; }
    }
}
