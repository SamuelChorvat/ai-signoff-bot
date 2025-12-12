namespace AISignoffBot.Models;

public record SignoffResult(bool Passed, IReadOnlyList<AcResult> CriteriaResults);