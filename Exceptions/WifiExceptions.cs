namespace Docker_Wifi.Exceptions;

public class WifiException : Exception
{
    public WifiException(string message) : base(message) { }
    public WifiException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class WifiScanException : WifiException
{
    public WifiScanException(string message) : base(message) { }
    public WifiScanException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class WifiConnectionException : WifiException
{
    public WifiConnectionException(string message) : base(message) { }
    public WifiConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ShellCommandException : Exception
{
    public int ExitCode { get; }
    public string StdErr { get; }

    public ShellCommandException(string message, int exitCode, string stdErr) : base(message)
    {
        ExitCode = exitCode;
        StdErr = stdErr;
    }
}
