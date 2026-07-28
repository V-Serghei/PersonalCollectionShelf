using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class EditMediaItemPage : ContentPage, IQueryAttributable
{
    private bool _hasQuery;
    private bool? _usesCompactLayout;
    private Guid? _navigationTarget;

    public EditMediaItemPage()
        : this(App.Services.GetRequiredService<EditMediaItemViewModel>())
    {
    }

    public EditMediaItemPage(EditMediaItemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MobileFormFocus.Attach(this, FormScrollView, KeyboardScrollSpacer);
        MobileFormFocus.Attach(this, PersonPickerScroll, PersonPickerKeyboardSpacer);
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            _hasQuery = true;

            if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var mediaItemId))
            {
                await ViewModel.LoadForEditAsync(mediaItemId);
                return;
            }

            await ViewModel.LoadForCreateAsync();
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "EditMediaItemPage.ApplyQueryAttributes");
        }
    }

    public void SetNavigationTarget(Guid? mediaItemId)
    {
        _hasQuery = true;
        _navigationTarget = mediaItemId;
    }

    public Task LoadNavigationTargetAsync() =>
        _navigationTarget.HasValue
            ? ViewModel.LoadForEditAsync(_navigationTarget.Value)
            : ViewModel.LoadForCreateAsync();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasQuery)
        {
            try
            {
                await ViewModel.LoadForCreateAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "EditMediaItemPage.OnAppearing");
            }
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        // Some Android devices report the allocated width in physical pixels here.
        // The idiom check keeps the form single-column on phones regardless of density.
        var usesCompactLayout = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || width < 700;
        if (_usesCompactLayout == usesCompactLayout)
        {
            return;
        }

        _usesCompactLayout = usesCompactLayout;
        ApplyResponsiveLayout(usesCompactLayout);
    }

    private void ApplyResponsiveLayout(bool usesCompactLayout)
    {
        PageLayout.ColumnDefinitions.Clear();
        PageLayout.RowDefinitions.Clear();
        PageLayout.Padding = usesCompactLayout ? new Thickness(10, 8, 10, 16) : new Thickness(24);

        if (usesCompactLayout)
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }
        else
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition(1040));
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        Grid.SetColumn(FormScrollView, usesCompactLayout ? 0 : 1);
        FormCard.Padding = usesCompactLayout ? new Thickness(16, 18) : new Thickness(30, 26);
        ConfigureStackingGrid(
            MainFormGrid,
            IdentityColumn,
            TrackingColumn,
            usesCompactLayout,
            24,
            32);
        ConfigureStackingGrid(
            BookDetailsGrid,
            BookPeopleColumn,
            BookMetadataColumn,
            usesCompactLayout,
            20,
            32);
        ConfigureStackingGrid(
            BookRelationsGrid,
            BookCollectionColumn,
            BookRelatedItemsColumn,
            usesCompactLayout,
            20,
            32);
        ConfigureStackingGrid(
            MovieRelationsGrid,
            MovieCollectionColumn,
            MovieRelatedItemsColumn,
            usesCompactLayout,
            20,
            32);
        ConfigureMovieDetailsGrid(usesCompactLayout);

        FormActionsGrid.ColumnDefinitions.Clear();
        FormActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        FormActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(
            usesCompactLayout ? GridLength.Star : new GridLength(180)));

        Grid.SetColumnSpan(PersonPickerOverlay, usesCompactLayout ? 1 : 3);
        PersonPickerOverlay.Padding = usesCompactLayout ? new Thickness(12) : new Thickness(24);
        PersonPickerCard.Padding = usesCompactLayout ? new Thickness(18) : new Thickness(24);
        PersonPickerCard.WidthRequest = usesCompactLayout ? -1 : 500;
        PersonPickerCard.HorizontalOptions = usesCompactLayout ? LayoutOptions.Fill : LayoutOptions.Center;
    }

    private static void ConfigureStackingGrid(
        Grid grid,
        View first,
        View second,
        bool usesCompactLayout,
        double compactSpacing,
        double wideSpacing)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnSpacing = usesCompactLayout ? 0 : wideSpacing;
        grid.RowSpacing = usesCompactLayout ? compactSpacing : 0;

        if (usesCompactLayout)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 0);
            Grid.SetRow(second, 1);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 1);
            Grid.SetRow(second, 0);
        }
    }

    private void ConfigureMovieDetailsGrid(bool usesCompactLayout)
    {
        View[] fields =
        [
            MovieRuntimeField,
            MovieCountryField,
            MovieOriginalLanguageField,
            MovieLanguageField,
            MovieAgeRatingField
        ];

        MovieDetailsGrid.ColumnDefinitions.Clear();
        MovieDetailsGrid.RowDefinitions.Clear();
        var columnCount = usesCompactLayout ? 1 : 2;
        for (var column = 0; column < columnCount; column++)
        {
            MovieDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var rowCount = (int)Math.Ceiling(fields.Length / (double)columnCount);
        for (var row = 0; row < rowCount; row++)
        {
            MovieDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        MovieDetailsGrid.ColumnSpacing = usesCompactLayout ? 0 : 14;
        foreach (var (field, index) in fields.Select((field, index) => (field, index)))
        {
            Grid.SetColumn(field, index % columnCount);
            Grid.SetRow(field, index / columnCount);
        }
    }

    public EditMediaItemViewModel ViewModel => (EditMediaItemViewModel)BindingContext;
}
