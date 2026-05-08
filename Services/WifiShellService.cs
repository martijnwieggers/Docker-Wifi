using System.Diagnostics;
using Docker_Wifi.Exceptions;
using Docker_Wifi.Helpers;
using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public interface IWifiShellService
{
    Task<ShellCommandResult> ExecuteCommandAsync(
        string command,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

public sealed class WifiShellService : IWifiShellService
{
    private readonly ILogger<WifiShellService> _logger;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public WifiShellService(ILogger<WifiShellService> logger)
    {
        _logger = logger;
    }

    public async Task<ShellCommandResult> ExecuteCommandAsync(
        string command,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        var argList = arguments.ToList();
        _logger.LogDebug("Executing command: {Command} {Arguments}", command, string.Join(" ", argList));

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in argList)
            startInfo.ArgumentList.Add(arg);

        try
        {
            using var process = new Process { StartInfo = startInfo };

            var stdOutBuilder = new System.Text.StringBuilder();
            var stdErrBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdOutBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill process after timeout");
                }

                throw new ShellCommandException(
                    $"Command timed out after {effectiveTimeout.TotalSeconds} seconds",
                    -1,
                    "Timeout");
            }

            stopwatch.Stop();

            var result = new ShellCommandResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdOutBuilder.ToString(),
                StdErr = stdErrBuilder.ToString(),
                Duration = stopwatch.Elapsed
            };

            if (result.Success)
            {
                _logger.LogDebug(
                    "Command completed successfully in {Duration}ms",
                    result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Command failed with exit code {ExitCode} in {Duration}ms. StdErr: {StdErr}",
                    result.ExitCode,
                    result.Duration.TotalMilliseconds,
                    result.StdErr);
            }

            return result;
        }
        catch (Exception ex) when (ex is not ShellCommandException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to execute command: {Command}", command);
            throw new ShellCommandException($"Failed to execute command: {ex.Message}", -1, ex.Message);
        }
    }
}
