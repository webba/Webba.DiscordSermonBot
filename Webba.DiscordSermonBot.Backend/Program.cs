using Discord.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Webba.DiscordSermonBot.Lib.Data;
using Webba.DiscordSermonBot.Lib.Services;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddConfiguration(new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build());
    });

builder.ConfigureServices((host, s) =>
{
    s.AddOptions<DiscordServiceOptions>()
        .Configure<IConfiguration>((settings, configuration) =>
        {
            configuration.GetSection(DiscordServiceOptions.SectionName).Bind(settings);
        });

    s.AddSingleton<DiscordRestConfig>(cfg => new()
    {
        APIOnRestInteractionCreation = true
    });
    s.AddSingleton<DiscordRestClient>();

    s.AddDbContext<SermonBotDbContext>(options => options.UseSqlServer(
        host.Configuration.GetConnectionString("SQLConn") ?? "", op => op.MigrationsAssembly("Webba.DiscordSermonBot.Backend")
    ));

    s.AddAzureClients(bb =>
    {
        bb.AddServiceBusClient(host.Configuration.GetConnectionString("SlashCommandBus"));
    });

    s.AddTransient<SermonRotationService>();
    s.AddTransient<DiscordService>();

});

var host = builder.Build();

host.Run();