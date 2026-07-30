using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class CategoryManagementPage : ContentPage
{
    private bool? _usesCompactLayout;

    public CategoryManagementPage() : this(App.Services.GetRequiredService<CategoryManagementViewModel>()) { }

    public CategoryManagementPage(CategoryManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MobileFormFocus.Attach(this, CategoryEditorScroll);
    }

    public CategoryManagementViewModel ViewModel => (CategoryManagementViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await ((CategoryManagementViewModel)BindingContext).LoadAsync(); }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "CategoryManagementPage.OnAppearing"); }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0)
        {
            return;
        }

        var compact = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || width < 700;
        if (_usesCompactLayout == compact)
        {
            return;
        }

        _usesCompactLayout = compact;
        BackButton.IsVisible = true;
        HeaderGrid.Padding = compact ? new Thickness(14, 10) : new Thickness(24, 12);
        HeaderTitle.FontSize = compact ? 22 : 28;
        HeaderGrid.ColumnDefinitions.Clear();
        if (compact)
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(BackButton, 0);
            Grid.SetColumn(HeaderTitle, 1);
            Grid.SetColumn(NewCategoryButton, 2);
        }
        else
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(HeaderTitle, 1);
            Grid.SetColumn(NewCategoryButton, 2);
        }

        ConfigureWorkspace(compact);
        ConfigureIdentity(compact);
    }

    private void ConfigureWorkspace(bool compact)
    {
        WorkspaceGrid.ColumnDefinitions.Clear();
        WorkspaceGrid.RowDefinitions.Clear();
        WorkspaceGrid.Padding = compact ? new Thickness(14, 6, 14, 16) : new Thickness(24, 8, 24, 24);
        if (compact)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(190));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(CategoryListCard, 0);
            Grid.SetRow(CategoryListCard, 0);
            Grid.SetColumn(CategoryEditorScroll, 0);
            Grid.SetRow(CategoryEditorScroll, 1);
        }
        else
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(280));
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(CategoryListCard, 0);
            Grid.SetRow(CategoryListCard, 0);
            Grid.SetColumn(CategoryEditorScroll, 1);
            Grid.SetRow(CategoryEditorScroll, 0);
        }
    }

    private void ConfigureIdentity(bool compact)
    {
        CategoryIdentityGrid.ColumnDefinitions.Clear();
        CategoryIdentityGrid.RowDefinitions.Clear();
        if (compact)
        {
            CategoryIdentityGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            CategoryIdentityGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            CategoryIdentityGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            CategoryIdentityGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(CategoryNameField, 0);
            Grid.SetRow(CategoryNameField, 0);
            Grid.SetColumn(CategoryBaseTypeField, 0);
            Grid.SetRow(CategoryBaseTypeField, 1);
            Grid.SetColumn(SystemCategoryNotice, 0);
            Grid.SetColumnSpan(SystemCategoryNotice, 1);
            Grid.SetRow(SystemCategoryNotice, 2);
        }
        else
        {
            CategoryIdentityGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            CategoryIdentityGrid.ColumnDefinitions.Add(new ColumnDefinition(260));
            CategoryIdentityGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            CategoryIdentityGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(CategoryNameField, 0);
            Grid.SetRow(CategoryNameField, 0);
            Grid.SetColumn(CategoryBaseTypeField, 1);
            Grid.SetRow(CategoryBaseTypeField, 0);
            Grid.SetColumn(SystemCategoryNotice, 0);
            Grid.SetColumnSpan(SystemCategoryNotice, 2);
            Grid.SetRow(SystemCategoryNotice, 1);
        }
    }
}
