namespace ShieldCommand.Core.Models;

public class DeviceSuggestion
{
    public required string IpAddress { get; init; }
    public string? DisplayName { get; init; }
    public string Source { get; init; } = "Manual";

    public override string ToString() => IpAddress;
}
