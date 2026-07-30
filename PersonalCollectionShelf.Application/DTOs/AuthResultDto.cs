namespace PersonalCollectionShelf.Application.DTOs;

public sealed class AuthResultDto
{
    public bool Succeeded { get; init; }

    public string? UserId { get; init; }

    public string? Email { get; init; }

    public string? ErrorKey { get; init; }

    public static AuthResultDto Success(string userId, string? email)
    {
        return new AuthResultDto { Succeeded = true, UserId = userId, Email = email };
    }

    public static AuthResultDto Failure(string errorKey)
    {
        return new AuthResultDto { Succeeded = false, ErrorKey = errorKey };
    }
}
