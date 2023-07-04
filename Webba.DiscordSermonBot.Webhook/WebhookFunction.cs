using System.Net;
using System.Net.Mime;
using Azure.Messaging.ServiceBus;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Webba.DiscordSermonBot.Lib.Models;
using Webba.DiscordSermonBot.Lib.Services;

namespace Webba.DiscordSermonBot.Webhook
{
    public class WebhookFunction
    {
        private readonly ILogger _logger;
        private readonly DiscordService _service;
        private readonly ServiceBusClient _busClient;

        public WebhookFunction(ILoggerFactory loggerFactory, DiscordService service, ServiceBusClient busClient)
        {
            _logger = loggerFactory.CreateLogger<WebhookFunction>();
            _service = service;
            _busClient = busClient;
        }

        [Function(nameof(InteractionWebhook))]
        public async Task<HttpResponseData> InteractionWebhook([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Recieved Interaction Post");

            string timestamp = req.Headers.GetValues("X-Signature-Timestamp").First();
            string signature = req.Headers.GetValues("X-Signature-Ed25519").First();

            MemoryStream stream = new MemoryStream();
            await req.Body.CopyToAsync(stream);
            byte[] body = stream.ToArray();

            DiscordWebhookResponse resp = await _service.HandleGlobalCommand(signature, timestamp, body);

            if (resp == null || resp.Authorized == false || resp.Response == null)
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }
            else
            {
                if (resp.CommandData != null)
                {
                    var sender = _busClient.CreateSender(DiscordService.BusQueueName);

                    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(resp.CommandData)));
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", MediaTypeNames.Application.Json);
                await response.WriteStringAsync(resp.Response);
                return response;
            }

        }
    }
}
