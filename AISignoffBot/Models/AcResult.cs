using AISignoffBot.Enums;

namespace AISignoffBot.Models;

public record AcResult(string Criterion, AcStatus Status, string Notes);