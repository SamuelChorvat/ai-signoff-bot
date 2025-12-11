using AISignoffBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace AISignoffBot.Controllers;


[ApiController]
[Route("jira/[controller]")]
public class WebhookController(
    IJiraWebhookService webhookService,
    ILogger<WebhookController> logger)
    : ControllerBase
{
    // Jira uses HEAD to validate webhook URL
    [HttpHead]
    public IActionResult Head() => Ok();

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Received empty webhook payload.");
            return Ok();
        }

        await webhookService.HandleWebhook(body);
        return Ok();
    }
}