using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class CategoryManagementViewModel : BaseViewModel
{
    private readonly ICategoryManagementService _categoryService;
    private readonly IAuthService _authService;
    private string _userId = "local-user";

    [ObservableProperty] public partial MediaCategoryDetailsDto? SelectedCategory { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial LocalizedOption<MediaType?>? SelectedBaseType { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSystemCategory { get; set; }

    public CategoryManagementViewModel(
        ICategoryManagementService categoryService,
        IAuthService authService,
        ILocalizationService localizationService) : base(localizationService)
    {
        _categoryService = categoryService;
        _authService = authService;
        InitializeOptions();
    }

    public ObservableCollection<MediaCategoryDetailsDto> Categories { get; } = [];
    public ObservableCollection<CategoryFieldEditor> Fields { get; } = [];
    public ObservableCollection<LocalizedOption<MediaType?>> BaseTypes { get; } = [];
    public ObservableCollection<LocalizedOption<string>> FieldTypes { get; } = [];

    public bool CanEdit => !IsSystemCategory;
    public bool CanDelete => SelectedCategory is { IsSystem: false };
    public string PageTitle => T("Categories.Title");
    public string BackText => T("Common.Back");
    public string NewText => T("Categories.New");
    public string SaveText => T("Common.Save");
    public string DeleteText => T("Common.Delete");
    public string NameLabel => T("Categories.Name");
    public string BaseTypeLabel => T("Categories.BaseType");
    public string FieldsTitle => T("Categories.Fields");
    public string AddFieldText => T("Categories.AddField");
    public string KeyLabel => T("Categories.Field.Key");
    public string LabelLabel => T("Categories.Field.Label");
    public string TypeLabel => T("Categories.Field.Type");
    public string RequiredLabel => T("Categories.Field.Required");
    public string ReadOnlyText => T("Categories.SystemReadOnly");

    partial void OnSelectedCategoryChanged(MediaCategoryDetailsDto? value)
    {
        if (value is null) return;
        Name = value.Name;
        IsSystemCategory = value.IsSystem;
        SelectedBaseType = BaseTypes.FirstOrDefault(option => option.Value == value.BaseMediaType);
        Fields.Clear();
        foreach (var field in value.Fields)
        {
            Fields.Add(new CategoryFieldEditor(field, FieldTypes));
        }
        StatusMessage = value.IsSystem ? ReadOnlyText : string.Empty;
        OnPropertyChanged(nameof(CanDelete));
    }

    partial void OnIsSystemCategoryChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    [RelayCommand]
    public async Task LoadAsync()
    {
        _userId = await _authService.GetCurrentUserIdAsync() ?? "local-user";
        var selectedId = SelectedCategory?.Id;
        Replace(Categories, await _categoryService.GetAllAsync(_userId));
        SelectedCategory = Categories.FirstOrDefault(value => value.Id == selectedId) ?? Categories.FirstOrDefault();
    }

    [RelayCommand]
    private void NewCategory()
    {
        SelectedCategory = null;
        IsSystemCategory = false;
        Name = string.Empty;
        SelectedBaseType = BaseTypes.FirstOrDefault();
        Fields.Clear();
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private void AddField() => Fields.Add(new CategoryFieldEditor(null, FieldTypes));

    [RelayCommand]
    private void RemoveField(CategoryFieldEditor field) => Fields.Remove(field);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        try
        {
            var saved = await _categoryService.SaveAsync(new SaveMediaCategoryRequest
            {
                Id = SelectedCategory?.Id,
                UserId = _userId,
                Name = Name,
                BaseMediaType = SelectedBaseType?.Value,
                Fields = Fields.Select(field => field.ToDto()).ToList()
            });
            await LoadAsync();
            SelectedCategory = Categories.First(value => value.Id == saved.Id);
            StatusMessage = T("Categories.Saved");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            await CrashReporter.ReportAsync(exception, "CategoryManagementViewModel.SaveAsync");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!CanDelete || SelectedCategory is null) return;
        await _categoryService.DeleteAsync(_userId, SelectedCategory.Id);
        await LoadAsync();
        StatusMessage = T("Categories.Deleted");
    }

    [RelayCommand]
    private Task GoBackAsync() => AppNavigation.CloseAsync();

    private void InitializeOptions()
    {
        BaseTypes.Clear();
        BaseTypes.Add(new LocalizedOption<MediaType?>(null, T("Categories.BaseType.None")));
        foreach (var type in Enum.GetValues<MediaType>())
        {
            BaseTypes.Add(new LocalizedOption<MediaType?>(type, T($"MediaType.{type}")));
        }
        FieldTypes.Clear();
        foreach (var type in new[] { "text", "multiline", "number", "date", "boolean", "choice" })
        {
            FieldTypes.Add(new LocalizedOption<string>(type, T($"Categories.FieldType.{type}")));
        }
    }

    protected override void RefreshLocalizedProperties()
    {
        var baseType = SelectedBaseType?.Value;
        InitializeOptions();
        SelectedBaseType = BaseTypes.FirstOrDefault(value => value.Value == baseType);
        foreach (var field in Fields) field.RefreshOptions(FieldTypes);
        base.RefreshLocalizedProperties();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}

public partial class CategoryFieldEditor : ObservableObject
{
    [ObservableProperty] public partial string Key { get; set; } = string.Empty;
    [ObservableProperty] public partial string Label { get; set; } = string.Empty;
    [ObservableProperty] public partial LocalizedOption<string>? SelectedType { get; set; }
    [ObservableProperty] public partial bool IsRequired { get; set; }

    public ObservableCollection<LocalizedOption<string>> Types { get; } = [];

    public CategoryFieldEditor(CategoryFieldDefinitionDto? field, IEnumerable<LocalizedOption<string>> options)
    {
        Key = field?.Key ?? string.Empty;
        Label = field?.Label ?? string.Empty;
        IsRequired = field?.IsRequired ?? false;
        RefreshOptions(options, field?.FieldType ?? "text");
    }

    public void RefreshOptions(IEnumerable<LocalizedOption<string>> options, string? selected = null)
    {
        var selectedValue = selected ?? SelectedType?.Value ?? "text";
        Types.Clear();
        foreach (var option in options) Types.Add(option);
        SelectedType = Types.FirstOrDefault(value => value.Value == selectedValue) ?? Types.FirstOrDefault();
    }

    public CategoryFieldDefinitionDto ToDto() => new()
    {
        Key = Key,
        Label = Label,
        FieldType = SelectedType?.Value ?? "text",
        IsRequired = IsRequired
    };
}
