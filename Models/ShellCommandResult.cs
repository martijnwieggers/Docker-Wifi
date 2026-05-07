namespace Docker_Wifi.Models;

public sealed class ShellCommandResult
{
    public int ExitCode { get; init; }
    public required string StdOut { get; init; }
    public required string StdErr { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success => ExitCode == 0;
}
