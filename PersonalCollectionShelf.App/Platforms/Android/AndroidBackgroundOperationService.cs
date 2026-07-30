using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System.Runtime.Versioning;

namespace PersonalCollectionShelf.App.Services;

[Service(
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class AndroidBackgroundOperationService : Service
{
    private const string ChannelId = "cloud-operations";
    private const int OngoingNotificationId = 4101;
    private const int CompletionNotificationId = 4102;
    private const string ActionStart = "pcs.cloud.START";
    private const string ActionReport = "pcs.cloud.REPORT";
    private const string ActionComplete = "pcs.cloud.COMPLETE";
    private const string ActionStop = "pcs.cloud.STOP";
    private const string ExtraTitle = "title";
    private const string ExtraMessage = "message";
    private const string ExtraProgress = "progress";
    private const string ExtraSucceeded = "succeeded";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        var action = intent?.Action ?? ActionStart;
        if (action == ActionStop)
        {
            StopForegroundCompat();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var title = intent?.GetStringExtra(ExtraTitle) ?? "Personal Collection Shelf";
        var message = intent?.GetStringExtra(ExtraMessage) ?? string.Empty;
        if (action == ActionComplete)
        {
            var succeeded = intent?.GetBooleanExtra(ExtraSucceeded, false) ?? false;
            StartForeground(
                OngoingNotificationId,
                BuildNotification(title, message, progress: 100, ongoing: true, succeeded));
            StopForegroundCompat();
            NotificationManagerCompat.From(this)?.Notify(
                CompletionNotificationId,
                BuildNotification(title, message, progress: 100, ongoing: false, succeeded));
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var progress = intent?.GetIntExtra(ExtraProgress, -1) ?? -1;
        var notification = BuildNotification(title, message, progress, ongoing: true, succeeded: true);
        StartForeground(OngoingNotificationId, notification);
        // The foreground service keeps the active process alive when the task is
        // swiped away. If Android force-kills the process, do not show a fake
        // restarted progress notification without an operation to resume.
        return StartCommandResult.NotSticky;
    }

    public static void Start(string title, string message) =>
        Send(ActionStart, title, message, -1);

    public static void Report(int percent, string message) =>
        Send(ActionReport, "Personal Collection Shelf", message, Math.Clamp(percent, 0, 100));

    public static void Complete(string message, bool succeeded)
    {
        var intent = CreateIntent(ActionComplete, "Personal Collection Shelf", message, 100);
        intent.PutExtra(ExtraSucceeded, succeeded);
        ContextCompat.StartForegroundService(Platform.AppContext, intent);
    }

    public static void Stop() =>
        Platform.AppContext.StopService(CreateIntent(ActionStop, string.Empty, string.Empty, 0));

    private static void Send(string action, string title, string message, int progress) =>
        ContextCompat.StartForegroundService(
            Platform.AppContext,
            CreateIntent(action, title, message, progress));

    private static Intent CreateIntent(string action, string title, string message, int progress)
    {
        var intent = new Intent(Platform.AppContext, typeof(AndroidBackgroundOperationService));
        intent.SetAction(action);
        intent.PutExtra(ExtraTitle, title);
        intent.PutExtra(ExtraMessage, message);
        intent.PutExtra(ExtraProgress, progress);
        return intent;
    }

    private Notification BuildNotification(
        string title,
        string message,
        int progress,
        bool ongoing,
        bool succeeded)
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pendingIntent = launchIntent is null
            ? null
            : PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var builder = new NotificationCompat.Builder(this, ChannelId)!;
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentTitle(title);
        builder.SetContentText(message);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(message));
        builder.SetOnlyAlertOnce(ongoing);
        builder.SetOngoing(ongoing);
        builder.SetAutoCancel(!ongoing);
        if (pendingIntent is not null) builder.SetContentIntent(pendingIntent);
        builder.SetPriority(NotificationCompat.PriorityLow);

        if (ongoing)
        {
            builder.SetProgress(100, Math.Max(progress, 0), progress < 0);
        }
        else
        {
            builder.SetPriority(succeeded ? NotificationCompat.PriorityDefault : NotificationCompat.PriorityHigh);
        }

        return builder.Build() ?? throw new InvalidOperationException("Unable to create background operation notification.");
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        CreateNotificationChannelApi26();
    }

    [SupportedOSPlatform("android26.0")]
    private void CreateNotificationChannelApi26()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Cloud operations",
            NotificationImportance.Low)
        {
            Description = "Synchronization and Google Drive backup progress"
        });
    }

    private void StopForegroundCompat()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(24))
        {
            StopForeground(StopForegroundFlags.Remove);
        }
        else
        {
#pragma warning disable CS0618
            StopForeground(true);
#pragma warning restore CS0618
        }
    }
}
