using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class SignInPage : ContentPage
{
    private readonly TaskCompletionSource _completion = new();

    public SignInPage()
        : this(App.Services.GetRequiredService<SignInViewModel>())
    {
    }

    public SignInPage(SignInViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public Task Completion => _completion.Task;

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _completion.TrySetResult();
    }
}
