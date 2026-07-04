namespace PersonalCollectionShelf.Application.Validation;

public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public IReadOnlyList<ValidationError> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public void Add(string code, string propertyName)
    {
        _errors.Add(new ValidationError(code, propertyName));
    }
}
