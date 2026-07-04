namespace PersonalCollectionShelf.Application.Validation;

public sealed record ValidationError(string Code, string PropertyName);
