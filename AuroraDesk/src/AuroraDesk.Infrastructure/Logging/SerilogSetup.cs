using AuroraDesk.Shared.Helpers;
using Serilog;

namespace AuroraDesk.Infrastructure.Logging;

public static class SerilogSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    public static ILogger CreateLogger()
    {
        var logPath = Path.Combine(PathHelper.GetLogPath(), "aurora-.log");

        return new LoggerConfiguration()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }
}
