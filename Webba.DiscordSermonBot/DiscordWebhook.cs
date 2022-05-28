using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Webba.DiscordSermonBot.Lib.Services;
using Webba.DiscordSermonBot.Lib.Models;

namespace Webba.DiscordSermonBot
{
    public class DiscordWebhook
    {
        private readonly DiscordService service;

        public DiscordWebhook(DiscordService service)
        {
            this.service = service;
        }

        [FunctionName(nameof(InteractionWebhook))]
        public async Task<IActionResult> InteractionWebhook(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Recieved Interaction Post");

            string timestamp = req.Headers["X-Signature-Timestamp"];
            string signature = req.Headers["X-Signature-Ed25519"];

            MemoryStream stream = new MemoryStream();
            await req.Body.CopyToAsync(stream);
            byte[] body = stream.ToArray();

            DiscordWebhookResponse resp = await service.HandleGlobalCommand(signature, timestamp, body);

            if(resp == null || resp.Authorized == false)
            {
                return new UnauthorizedResult();
            }
            else
            {
                return new ContentResult() { Content = resp.Response, ContentType = System.Net.Mime.MediaTypeNames.Application.Json, StatusCode = StatusCodes.Status200OK };
            }
        }

        public async Task<IActionResult> SetupGlobalCommand(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Recieved Interaction Post");

            await service.SetupGlobalCommandAsync();

            return new OkObjectResult("done");
        }
    }
}
