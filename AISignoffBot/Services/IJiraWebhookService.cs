namespace AISignoffBot.Services;

public interface IJiraWebhookService
{
    Task HandleWebhook(string payload);
}