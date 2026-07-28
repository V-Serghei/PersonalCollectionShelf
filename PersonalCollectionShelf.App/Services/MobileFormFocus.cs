namespace PersonalCollectionShelf.App.Services;

internal static class MobileFormFocus
{
    private static readonly BindableProperty ScrollOwnerProperty = BindableProperty.CreateAttached(
        "ScrollOwner",
        typeof(ScrollView),
        typeof(MobileFormFocus),
        default(ScrollView));

    public static void Attach(ContentPage page, ScrollView scrollView)
    {
        void AttachInput(InputView input)
        {
            if (input.GetValue(ScrollOwnerProperty) is not null)
            {
                return;
            }

            input.SetValue(ScrollOwnerProperty, scrollView);
            input.Focused += HandleFocused;
        }

        page.Loaded += (_, _) =>
        {
            foreach (var input in page.GetVisualTreeDescendants().OfType<InputView>())
            {
                AttachInput(input);
            }
        };
        page.DescendantAdded += (_, args) =>
        {
            if (args.Element is InputView input)
            {
                AttachInput(input);
            }
        };
    }

    private static async void HandleFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not InputView input ||
            input.GetValue(ScrollOwnerProperty) is not ScrollView scrollView)
        {
            return;
        }

        // Android keyboards resize the activity asynchronously. Repeating the scroll after
        // both animation stages keeps fields added by dynamic templates above the IME.
        foreach (var delay in new[] { 120, 260, 420 })
        {
            await Task.Delay(delay);
            if (!input.IsFocused)
            {
                return;
            }

            await scrollView.ScrollToAsync(input, ScrollToPosition.Center, true);
        }
    }
}
