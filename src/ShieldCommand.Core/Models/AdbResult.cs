namespace ShieldCommand.Core.Models;

public record AdbResult(bool Success, string Output, string Error = "");
