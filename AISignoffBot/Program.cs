using AISignoffBot.Models;
using AISignoffBot.Services;
using AISignoffBot.Services.Interfaces;
using AISignoffBot.Services.V1;

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
        
        // Options
        builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));

        // Services
        builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
        builder.Services.AddScoped<ISignoffWorkflow, SignoffWorkflow>();

        builder.Services.AddHttpClient<IJiraClient, JiraClient>();
        
        builder.Services.AddScoped<IAcProvider, SimpleAcProvider>();
        builder.Services.AddScoped<ISignoffEvaluator, RandomSignoffEvaluator>();
        builder.Services.AddScoped<ISignoffReporter, MarkdownSignoffReporter>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}