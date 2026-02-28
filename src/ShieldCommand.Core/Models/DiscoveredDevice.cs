namespace ShieldCommand.Core.Models;

public sealed record DiscoveredDevice(string IpAddress, string? DisplayName = null);
