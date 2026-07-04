namespace PersonalCollectionShelf.Application.Validation;

public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
