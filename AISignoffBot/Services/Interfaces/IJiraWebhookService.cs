namespace AISignoffBot.Services.Interfaces;

public interface IJiraWebhookService
{
    Task HandleWebhook(string payload);
}