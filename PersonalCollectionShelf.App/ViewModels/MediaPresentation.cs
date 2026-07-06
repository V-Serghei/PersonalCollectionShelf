using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

internal static class MediaPresentation
{
    public static Color GetMediaTypeColor(MediaType mediaType)
    {
        return mediaType switch
        {
            MediaType.Movie => Color.FromArgb("#E07C54"),
            MediaType.Series => Color.FromArgb("#5BA4F0"),
            MediaType.Book => Color.FromArgb("#7CCC8A"),
            MediaType.Manga => Color.FromArgb("#F07CB8"),
            MediaType.Comic => Color.FromArgb("#F0C040"),
            MediaType.Game => Color.FromArgb("#C47CF0"),
            MediaType.Anime => Color.FromArgb("#F07CB8"),
            _ => Color.FromArgb("#9D7FF4")
        };
    }

    public static Color GetStatusForegroundColor(MediaStatus status)
    {
        return status switch
        {
            MediaStatus.Completed => Color.FromArgb("#4ADE80"),
            MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading => Color.FromArgb("#60A5FA"),
            MediaStatus.Planned or MediaStatus.OnHold => Color.FromArgb("#FBBF24"),
            MediaStatus.Dropped => Color.FromArgb("#F87171"),
            _ => Color.FromArgb("#9D7FF4")
        };
    }

    public static Color GetStatusBackgroundColor(MediaStatus status)
    {
        return status switch
        {
            MediaStatus.Completed => Color.FromArgb("#1A4ADE80"),
            MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading => Color.FromArgb("#1A60A5FA"),
            MediaStatus.Planned or MediaStatus.OnHold => Color.FromArgb("#1AFBBF24"),
            MediaStatus.Dropped => Color.FromArgb("#1AF87171"),
            _ => Color.FromArgb("#1A9D7FF4")
        };
    }

    public static bool HasProgressBar(MediaStatus status)
    {
        return status is MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading;
    }

    public static double GetProgressPercent(int current, int? total, MediaStatus status)
    {
        if (status == MediaStatus.Completed)
        {
            return 1d;
        }

        if (total is null or <= 0)
        {
            return 0d;
        }

        return Math.Clamp((double)current / total.Value, 0d, 1d);
    }
}
