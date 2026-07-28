using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PeoplePage : ContentPage
{
    private bool? _usesCompactLayout;

    public PeoplePage() : this(App.Services.GetRequiredService<PeopleViewModel>()) { }

    public PeoplePage(PeopleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MobileFormFocus.Attach(this, PersonEditorScroll);
    }

    public PeopleViewModel ViewModel => (PeopleViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await ((PeopleViewModel)BindingContext).LoadAsync(); }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "PeoplePage.OnAppearing"); }
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
        BackButton.IsVisible = false;
        HeaderGrid.Padding = compact ? new Thickness(14, 10) : new Thickness(24, 12);
        HeaderTitle.FontSize = compact ? 22 : 28;
        HeaderGrid.ColumnDefinitions.Clear();
        if (compact)
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(HeaderTitle, 1);
            Grid.SetColumn(PeopleSearchButton, 2);
            Grid.SetColumn(NewPersonButton, 3);
        }
        else
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(HeaderTitle, 1);
            Grid.SetColumn(PeopleSearchButton, 2);
            Grid.SetColumn(NewPersonButton, 3);
        }

        ConfigureWorkspace(compact);
        ConfigureAssociations(compact);
    }

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible) { await Task.Delay(50); PeopleSearch.Focus(); }
    }

    private void ConfigureWorkspace(bool compact)
    {
        WorkspaceGrid.ColumnDefinitions.Clear();
        WorkspaceGrid.RowDefinitions.Clear();
        WorkspaceGrid.Padding = compact ? new Thickness(14, 6, 14, 16) : new Thickness(24, 8, 24, 24);
        if (compact)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(200));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(PeopleListCard, 0);
            Grid.SetRow(PeopleListCard, 0);
            Grid.SetColumn(PersonEditorScroll, 0);
            Grid.SetRow(PersonEditorScroll, 1);
        }
        else
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(280));
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(PeopleListCard, 0);
            Grid.SetRow(PeopleListCard, 0);
            Grid.SetColumn(PersonEditorScroll, 1);
            Grid.SetRow(PersonEditorScroll, 0);
        }
    }

    private void ConfigureAssociations(bool compact)
    {
        AssociationsGrid.ColumnDefinitions.Clear();
        AssociationsGrid.RowDefinitions.Clear();
        if (compact)
        {
            AssociationsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AssociationsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AssociationsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AssociationsGrid.RowSpacing = 14;
            Grid.SetColumn(ProfessionsCard, 0);
            Grid.SetRow(ProfessionsCard, 0);
            Grid.SetColumn(RelationsCard, 0);
            Grid.SetRow(RelationsCard, 1);
        }
        else
        {
            AssociationsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AssociationsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AssociationsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AssociationsGrid.RowSpacing = 0;
            Grid.SetColumn(ProfessionsCard, 0);
            Grid.SetRow(ProfessionsCard, 0);
            Grid.SetColumn(RelationsCard, 1);
            Grid.SetRow(RelationsCard, 0);
        }
    }
}
