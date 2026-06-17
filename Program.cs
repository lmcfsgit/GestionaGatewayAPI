using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Middleware;
using Serilog;
using System.Reflection;

namespace GestionaGatewayAPI;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "log-.txt");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:WriteTo:1:Args:path"] = logsPath
        });

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Host.UseSerilog();

        var apiAssembly = typeof(Program).Assembly;
        var coreAssembly = typeof(IGestionaDocumentService).Assembly;

        var rawApiVersion =
            apiAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? apiAssembly.GetName().Version?.ToString()
            ?? "unknown";
        var rawDllVersion =
            coreAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? coreAssembly.GetName().Version?.ToString()
            ?? "unknown";

        var rawApiVersionParts = rawApiVersion.Split('+', 2);
        var apiVersion = rawApiVersionParts[0];
        var apiCommit = rawApiVersionParts.Length > 1 ? rawApiVersionParts[1] : "n/a";


        var rawDllVersionParts = rawDllVersion.Split('+', 2);
        var dllVersion = rawDllVersionParts[0];
        var dllCommit = rawDllVersionParts.Length > 1 ? rawDllVersionParts[1] : "n/a";

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IGestionaApiClient, GestionaApiClient>();
        builder.Services.AddScoped<IGestionaProcessService, GestionaProcessService>();
        builder.Services.AddScoped<IGestionaDocumentService, GestionaDocumentService>();
        builder.Services.AddScoped<IGestionaThirdService, GestionaThirdService>();
        builder.Services.Configure<GestionaOptions>(
            builder.Configuration.GetSection(GestionaOptions.SectionName));

        try
        {
            Log.Information(
                "{Method} starting GestionaGatewayAPI. ApiVersion={ApiVersion}, ApiCommit={ApiCommit}, DllVersion={DllVersion}, DllCommit={DllCommit}",
                nameof(Main),
                apiVersion,
                apiCommit,
                dllVersion,
                dllCommit);

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<ClientRequestLoggingMiddleware>();
            app.UseSerilogRequestLogging();

            // app.UseHttpsRedirection();

            app.MapControllers();

            app.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
