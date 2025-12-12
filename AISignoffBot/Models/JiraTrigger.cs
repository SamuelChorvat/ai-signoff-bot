using AISignoffBot.Enums;

namespace AISignoffBot.Models;

public record JiraTrigger(string IssueKey, JiraTriggerType Type, string? ActorAccountId = null);