namespace ShieldCommand.Core.Models;

public sealed record InstalledPackage(
    string PackageName,
    string? VersionName = null,
    string? VersionCode = null);
