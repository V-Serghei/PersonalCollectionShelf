using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.Validation;

public static class MediaItemValidator
{
    public static ValidationResult Validate(CreateMediaItemRequest request)
    {
        var result = new ValidationResult();
        ValidateShared(request.UserId, request.Title, request.Rating, request.ProgressCurrent, request.ProgressTotal, request.ReleaseYear, result);
        ValidateBook(request.MediaType, request.BookDetails, result);
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
        ValidateBook(request.MediaType, request.BookDetails, result);
        return result;
    }

    private static void ValidateShared(
        string userId,
        string title,
        decimal? rating,
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

        if (rating is < 0 or > 10 || (rating.HasValue && decimal.Round(rating.Value, 1) != rating.Value))
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

    private static void ValidateBook(MediaType mediaType, BookDetailsInput? book, ValidationResult result)
    {
        if (book is null)
        {
            return;
        }

        if (mediaType != MediaType.Book)
        {
            result.Add("Validation.BookDetailsType", nameof(book));
        }

        ValidateYear(book.EditionYear, nameof(book.EditionYear), result);
        ValidateYear(book.OriginalPublicationYear, nameof(book.OriginalPublicationYear), result);
        ValidateYear(book.TranslationYear, nameof(book.TranslationYear), result);

        if (book.PageCount is < 1)
        {
            result.Add("Validation.PageCountInvalid", nameof(book.PageCount));
        }

        if (book.EditionNumber is < 1)
        {
            result.Add("Validation.EditionNumberInvalid", nameof(book.EditionNumber));
        }

        if (!string.IsNullOrWhiteSpace(book.Isbn10) && NormalizeIsbn(book.Isbn10).Length != 10)
        {
            result.Add("Validation.Isbn10Invalid", nameof(book.Isbn10));
        }

        if (!string.IsNullOrWhiteSpace(book.Isbn13) && NormalizeIsbn(book.Isbn13).Length != 13)
        {
            result.Add("Validation.Isbn13Invalid", nameof(book.Isbn13));
        }
    }

    private static void ValidateYear(int? year, string fieldName, ValidationResult result)
    {
        if (year is < 1 or > 2200)
        {
            result.Add("Validation.BookYearRange", fieldName);
        }
    }

    private static string NormalizeIsbn(string value) => value.Replace("-", string.Empty).Replace(" ", string.Empty);
}
