using GestionaGatewayAPI.Configuration;
using GestionaGatewayAPI.Services;
using Serilog;

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

        builder.Services.AddControllers();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IGestionaApiClient, GestionaApiClient>();
        builder.Services.Configure<GestionaOptions>(
            builder.Configuration.GetSection(GestionaOptions.SectionName));

        try
        {
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
