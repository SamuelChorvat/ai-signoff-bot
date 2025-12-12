using AISignoffBot.Enums;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffWorkflow
{
    Task Process(string issueKey, JiraTriggerType triggerType, string? actorAccountId, CancellationToken ct = default);
}