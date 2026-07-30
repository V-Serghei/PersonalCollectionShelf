namespace PersonalCollectionShelf.App.Services;

public interface IBackgroundOperationNotifier
{
    void Start(string title, string message);

    void Report(int percent, string message);

    void Complete(string message);

    void Fail(string message);

    void Stop();
}

public sealed class BackgroundOperationNotifier : IBackgroundOperationNotifier
{
    public void Start(string title, string message)
    {
#if ANDROID
        AndroidBackgroundOperationService.Start(title, message);
#endif
    }

    public void Report(int percent, string message)
    {
#if ANDROID
        AndroidBackgroundOperationService.Report(percent, message);
#endif
    }

    public void Complete(string message)
    {
#if ANDROID
        AndroidBackgroundOperationService.Complete(message, succeeded: true);
#endif
    }

    public void Fail(string message)
    {
#if ANDROID
        AndroidBackgroundOperationService.Complete(message, succeeded: false);
#endif
    }

    public void Stop()
    {
#if ANDROID
        AndroidBackgroundOperationService.Stop();
#endif
    }
}
