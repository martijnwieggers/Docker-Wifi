using System.Text;

namespace Docker_Wifi.Helpers;

public static class CommandLineHelper
{
    /// <summary>
    /// Safely escapes a command line argument for shell execution.
    /// Prevents shell injection by properly quoting arguments.
    /// </summary>
    public static string EscapeArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (OperatingSystem.IsWindows())
        {
            return EscapeWindowsArgument(argument);
        }

        return EscapeLinuxArgument(argument);
    }

    private static string EscapeWindowsArgument(string argument)
    {
        var sb = new StringBuilder();
        sb.Append('"');

        for (int i = 0; i < argument.Length; i++)
        {
            char c = argument[i];

            if (c == '"')
            {
                sb.Append('\\').Append('"');
            }
            else if (c == '\\')
            {
                int numBackslashes = 1;
                while (i + 1 < argument.Length && argument[i + 1] == '\\')
                {
                    numBackslashes++;
                    i++;
                }

                if (i + 1 < argument.Length && argument[i + 1] == '"')
                {
                    sb.Append('\\', numBackslashes * 2);
                }
                else
                {
                    sb.Append('\\', numBackslashes);
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static string EscapeLinuxArgument(string argument)
    {
        // For Linux, wrap in single quotes and escape single quotes
        return $"'{argument.Replace("'", "'\\''")}'";
    }

    /// <summary>
    /// Sanitizes a string for logging, removing sensitive information.
    /// </summary>
    public static string SanitizeForLogging(string input, bool isSensitive = false)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (isSensitive)
        {
            return "***REDACTED***";
        }

        return input;
    }
}
