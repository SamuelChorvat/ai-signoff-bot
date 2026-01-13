using AISignoffBot.Models;
using AISignoffBot.Services;
using AISignoffBot.Services.Interfaces;
using AISignoffBot.Services.Rules;

namespace AISignoffBot;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHealthChecks();
        
        // Options
        builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));
        builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
        builder.Services.Configure<CommentFooterOptions>(builder.Configuration.GetSection("CommentFooter"));

        // Services
        builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
        builder.Services.AddScoped<ISignoffWorkflow, SignoffWorkflow>();
        builder.Services.AddScoped<IEvidenceProvider, JiraEvidenceProvider>();

        builder.Services.AddHttpClient<IJiraClient, JiraClient>();

        builder.Services.AddSingleton<IBuildInfoProvider, BuildInfoProvider>();
        builder.Services.AddSingleton<ICommentFormatter, CommentFormatter>();

        builder.Services.AddScoped<IAcProvider, SimpleAcProvider>();
        builder.Services.AddScoped<IAiEvidenceAnalyzer, VisionAiEvidenceAnalyzer>();
        builder.Services.AddHttpClient<IAiClient, OpenAiClient>();
        builder.Services.AddScoped<ISignoffReporter, MarkdownSignoffReporter>();
        builder.Services.AddScoped<ISignoffRule, LocalUrlEvidenceRule>();
        builder.Services.AddScoped<ISignoffRule, BrowserUrlBarRule>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapHealthChecks("/health")
            .WithTags("Health")
            .WithOpenApi();

        app.MapControllers();

        app.Run();
    }
}
