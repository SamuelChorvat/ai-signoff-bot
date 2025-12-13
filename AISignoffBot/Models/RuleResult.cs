namespace AISignoffBot.Models;

public record RuleResult(IReadOnlyList<RuleFailure> Failures)
{
    public bool Passed => Failures.Count == 0;
}
