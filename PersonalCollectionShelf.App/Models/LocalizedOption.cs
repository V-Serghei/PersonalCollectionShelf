using System.ComponentModel;

namespace PersonalCollectionShelf.App.Models;

public abstract record DisplayOption(string DisplayName) : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => DisplayName;
}

public sealed record LocalizedOption<T>(T Value, string DisplayName) : DisplayOption(DisplayName)
{
    public override string ToString() => DisplayName;
}
