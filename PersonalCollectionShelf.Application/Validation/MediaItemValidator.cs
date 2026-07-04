using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Validation;

public static class MediaItemValidator
{
    public static ValidationResult Validate(CreateMediaItemRequest request)
    {
        var result = new ValidationResult();
        ValidateShared(request.UserId, request.Title, request.Rating, request.ProgressCurrent, request.ProgressTotal, request.ReleaseYear, result);
        return result;
    }

    public static ValidationResult Validate(UpdateMediaItemRequest request)
    {
        var result = new ValidationResult();

        if (request.Id == Guid.Empty)
        {
            result.Add("Validation.IdRequired", nameof(request.Id));
        }

        ValidateShared(request.UserId, request.Title, request.Rating, request.ProgressCurrent, request.ProgressTotal, request.ReleaseYear, result);
        return result;
    }

    private static void ValidateShared(
        string userId,
        string title,
        int? rating,
        int progressCurrent,
        int? progressTotal,
        int? releaseYear,
        ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            result.Add("Validation.UserRequired", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            result.Add("Validation.TitleRequired", nameof(title));
        }

        if (rating is < 1 or > 10)
        {
            result.Add("Validation.RatingRange", nameof(rating));
        }

        if (progressCurrent < 0)
        {
            result.Add("Validation.ProgressCurrentInvalid", nameof(progressCurrent));
        }

        if (progressTotal is < 0)
        {
            result.Add("Validation.ProgressTotalInvalid", nameof(progressTotal));
        }

        if (progressTotal.HasValue && progressCurrent > progressTotal.Value)
        {
            result.Add("Validation.ProgressCurrentExceedsTotal", nameof(progressCurrent));
        }

        if (releaseYear is < 1800 or > 2200)
        {
            result.Add("Validation.ReleaseYearRange", nameof(releaseYear));
        }
    }
}
