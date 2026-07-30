using System.Diagnostics;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace PersonalCollectionShelf.App.Services;

public static class CrashReporter
{
    private static int _isShowingAlert;

    public static string LogDirectory => Path.Combine(FileSystem.AppDataDirectory, "logs");

    public static void InstallGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Log(exception, "AppDomain unhandled exception");
            }
            else
            {
                LogMessage("AppDomain unhandled exception", args.ExceptionObject?.ToString() ?? "Unknown exception");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log(args.Exception, "TaskScheduler unobserved task exception");
            args.SetObserved();
        };

#if WINDOWS
        Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, args) =>
        {
            _ = ReportAsync(args.Exception, "WinUI unhandled exception");
            args.Handled = true;
        };
#endif
    }

    public static string Log(Exception exception, string context)
    {
        return LogMessage(context, exception.ToString());
    }

    public static string LogMessage(string context, string message)
    {
        Directory.CreateDirectory(LogDirectory);
        var logPath = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
        var entry = new StringBuilder()
            .AppendLine("============================================================")
            .AppendLine($"{DateTime.Now:O}")
            .AppendLine(context)
            .AppendLine(message)
            .ToString();

        File.AppendAllText(logPath, entry);
        Debug.WriteLine(entry);
        Console.Error.WriteLine(entry);
        return logPath;
    }

    public static async Task ReportAsync(Exception exception, string context)
    {
        var logPath = Log(exception, context);

        if (Interlocked.Exchange(ref _isShowingAlert, 1) == 1)
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Shell.Current?.CurrentPage
                    ?? Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;

                if (page is not null)
                {
                    await page.DisplayAlertAsync(
                        "Application error",
                        $"Something failed. Details were written to:\n{logPath}\n\n{exception.GetType().Name}: {exception.Message}",
                        "OK");
                }
            });
        }
        catch (Exception alertException)
        {
            Log(alertException, "Failed to show error alert");
        }
        finally
        {
            Interlocked.Exchange(ref _isShowingAlert, 0);
        }
    }
}
