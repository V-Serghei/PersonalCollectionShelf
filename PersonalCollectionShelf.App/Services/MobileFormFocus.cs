namespace PersonalCollectionShelf.App.Services;

internal static class MobileFormFocus
{
    private static readonly BindableProperty ScrollOwnerProperty = BindableProperty.CreateAttached(
        "ScrollOwner",
        typeof(ScrollView),
        typeof(MobileFormFocus),
        default(ScrollView));

    private static readonly BindableProperty ScrollStateProperty = BindableProperty.CreateAttached(
        "ScrollState",
        typeof(FocusScrollState),
        typeof(MobileFormFocus),
        default(FocusScrollState));

    public static void Attach(ContentPage page, ScrollView scrollView, VisualElement? keyboardSpacer = null)
    {
        var state = scrollView.GetValue(ScrollStateProperty) as FocusScrollState;
        if (state is null)
        {
            state = new FocusScrollState();
            scrollView.SetValue(ScrollStateProperty, state);
            scrollView.Scrolled += state.HandleScrolled;
        }

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
            input.TextChanged += HandleTextChanged;
            input.Unfocused += HandleUnfocused;
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
            input.GetValue(ScrollOwnerProperty) is not ScrollView scrollView ||
            scrollView.GetValue(ScrollStateProperty) is not FocusScrollState state)
        {
            return;
        }

        state.BeginFocus(input);
        await ScrollToInputAsync(input, scrollView, state, 120);
    }

    private static async void HandleTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not InputView input ||
            !input.IsFocused ||
            input.GetValue(ScrollOwnerProperty) is not ScrollView scrollView ||
            scrollView.GetValue(ScrollStateProperty) is not FocusScrollState state ||
            !state.RestoreAutoScrollAfterTyping(input))
        {
            return;
        }

        await ScrollToInputAsync(input, scrollView, state, 60);
    }

    private static void HandleUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is InputView input &&
            input.GetValue(ScrollOwnerProperty) is ScrollView scrollView &&
            scrollView.GetValue(ScrollStateProperty) is FocusScrollState state)
        {
            state.EndFocus(input);
        }
    }

    private static async Task ScrollToInputAsync(
        InputView input,
        ScrollView scrollView,
        FocusScrollState state,
        int delayMilliseconds)
    {
        var request = state.CreateScrollRequest(input);
        await Task.Delay(delayMilliseconds);
        if (!input.IsFocused || !state.CanRun(request, input))
        {
            return;
        }

        state.IsProgrammaticScroll = true;
        try
        {
            // An immediate move avoids an animated scroll fighting a drag gesture from the user.
            await scrollView.ScrollToAsync(input, ScrollToPosition.Center, false);
        }
        finally
        {
            state.IsProgrammaticScroll = false;
        }
    }

    private sealed class FocusScrollState
    {
        private int _requestVersion;
        private InputView? _focusedInput;
        private bool _manualScrollCancelled;

        public bool IsProgrammaticScroll { get; set; }

        public void BeginFocus(InputView input)
        {
            _focusedInput = input;
            _manualScrollCancelled = false;
            _requestVersion++;
        }

        public void EndFocus(InputView input)
        {
            if (!ReferenceEquals(_focusedInput, input))
            {
                return;
            }

            _focusedInput = null;
            _manualScrollCancelled = false;
            _requestVersion++;
        }

        public int CreateScrollRequest(InputView input)
        {
            if (!ReferenceEquals(_focusedInput, input))
            {
                _focusedInput = input;
            }

            return ++_requestVersion;
        }

        public bool CanRun(int request, InputView input) =>
            request == _requestVersion &&
            ReferenceEquals(_focusedInput, input) &&
            !_manualScrollCancelled;

        public bool RestoreAutoScrollAfterTyping(InputView input)
        {
            if (!ReferenceEquals(_focusedInput, input) || !_manualScrollCancelled)
            {
                return false;
            }

            _manualScrollCancelled = false;
            return true;
        }

        public void HandleScrolled(object? sender, ScrolledEventArgs e)
        {
            if (IsProgrammaticScroll || _focusedInput is null || !_focusedInput.IsFocused)
            {
                return;
            }

            _manualScrollCancelled = true;
            _requestVersion++;
        }
    }
}
