namespace PersonalCollectionShelf.App.Services;

internal static class MobileFormFocus
{
    private static readonly BindableProperty ScrollOwnerProperty = BindableProperty.CreateAttached(
        "ScrollOwner",
        typeof(ScrollView),
        typeof(MobileFormFocus),
        default(ScrollView));

    public static void Attach(ContentPage page, ScrollView scrollView, VisualElement? keyboardSpacer = null)
    {
        void AttachInput(InputView input)
        {
            if (!IsDescendantOf(input, scrollView))
            {
                return;
            }

            if (input.GetValue(ScrollOwnerProperty) is not null)
            {
                return;
            }

            input.SetValue(ScrollOwnerProperty, scrollView);
            input.Focused += HandleFocused;
            if (keyboardSpacer is not null)
            {
                input.Focused += (_, _) => ShowKeyboardSpacer(page, keyboardSpacer);
                input.Unfocused += async (_, _) =>
                {
                    await Task.Delay(120);
                    if (!page.GetVisualTreeDescendants().OfType<InputView>().Any(value => value.IsFocused))
                    {
                        keyboardSpacer.HeightRequest = 0;
                    }
                };
            }
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

    private static bool IsDescendantOf(Element element, Element ancestor)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void ShowKeyboardSpacer(ContentPage page, VisualElement keyboardSpacer)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.Android ||
            DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
        {
            return;
        }

        // AdjustResize is not reliable for edge-to-edge modal pages on every Android keyboard.
        // Extra scroll extent lets the last controls move completely above an overlaying IME.
        var pageHeight = page.Height > 0 ? page.Height : 720;
        keyboardSpacer.HeightRequest = Math.Clamp(pageHeight * 0.48, 280, 430);
    }

    private static async void HandleFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not InputView input ||
            input.GetValue(ScrollOwnerProperty) is not ScrollView scrollView)
        {
            return;
        }

        // Move to the field once when it receives focus. Further scrolling must remain under
        // the user's control until another input is selected.
        await Task.Delay(120);
        if (input.IsFocused)
        {
            await scrollView.ScrollToAsync(input, ScrollToPosition.Center, true);
        }
    }
}
