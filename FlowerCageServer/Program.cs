
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using FlowerCageServer.Extensions;
using FlowerCageServer.Middleware;
using FlowerCageServer.Data;

namespace FlowerCageServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        // Apply Server.Urls from configuration (appsettings.json > "Server": { "Urls": "http://0.0.0.0:5101" })
        var serverUrls = builder.Configuration["Server:Urls"];
        if (!string.IsNullOrWhiteSpace(serverUrls))
        {
            builder.WebHost.UseUrls(serverUrls.Split(';'));
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddOpenApiDocumentation();

        var app = builder.Build();

        // Log startup configuration
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var env = app.Environment;
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "(none)";
        var gptEnabled = builder.Configuration["ExternalServices:Gpt:Enabled"] ?? "false";
        var gptModel = builder.Configuration["ExternalServices:Gpt:Model"] ?? "(none)";
        logger.LogInformation("=== Flower Cage Server Starting ===");
        logger.LogInformation("  Environment : {Env}", env.EnvironmentName);
        logger.LogInformation("  Server URLs : {Urls}", serverUrls ?? "(default)");
        logger.LogInformation("  Database    : {Conn}", connStr);
        logger.LogInformation("  GPT Enabled : {Enabled}, Model: {Model}", gptEnabled, gptModel);

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FlowerCageDbContext>();
            if (dbContext.Database.GetMigrations().Any())
            {
                dbContext.Database.Migrate();
            }
            else
            {
                if (env.IsDevelopment() && dbContext.Database.CanConnect())
                {
                    try
                    {
                        _ = dbContext.Users.Any();
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42P01")
                    {
                        dbContext.Database.EnsureDeleted();
                    }
                }

                dbContext.Database.EnsureCreated();
            }
            logger.LogInformation("  Database    : Ready");
        }

        // Middleware pipeline
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Only redirect HTTPS in production
        if (!env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthorization();
        app.MapGet("/", () => Results.Ok(new
        {
            Name = "Flower Cage Server",
            Status = "Running",
            Api = "/api/users",
            OpenApi = "/openapi/v1.json"
        }));
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
        app.MapOpenApi();
        app.MapControllers();

        logger.LogInformation("=== Flower Cage Server Ready ===");
        app.Run();
    }
}
