using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Services;
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

        var apiVersion = builder.Configuration["Api:Version"] ?? "unknown";
        var coreAssembly = typeof(IGestionaDocumentService).Assembly;

        var rawDllVersion =
            coreAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? coreAssembly.GetName().Version?.ToString()
            ?? "unknown";

        var rawDllVersionParts = rawDllVersion.Split('+', 2);
        var dllVersion = rawDllVersionParts[0];
        var dllCommit = rawDllVersionParts.Length > 1 ? rawDllVersionParts[1] : "n/a";

        builder.Services.AddControllers();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IGestionaApiClient, GestionaApiClient>();
        builder.Services.AddScoped<IGestionaDocumentService, GestionaDocumentService>();
        builder.Services.Configure<GestionaOptions>(
            builder.Configuration.GetSection(GestionaOptions.SectionName));

        try
        {
            Log.Information(
                "{Method} starting application. ApiVersion={ApiVersion}, DllVersion={DllVersion}, DllCommit={DllCommit}",
                nameof(Main),
                apiVersion,
                dllVersion,
                dllCommit);

            var app = builder.Build();

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
