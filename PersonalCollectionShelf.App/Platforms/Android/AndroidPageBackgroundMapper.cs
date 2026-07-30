#if ANDROID
using Android.Graphics;
using Android.Graphics.Drawables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.Platforms.Android;

internal static class AndroidPageBackgroundMapper
{
    private static readonly object BitmapLock = new();
    private static string? _loadedPath;
    private static Bitmap? _loadedBitmap;

    public static void Apply(PageHandler handler, IContentView view)
    {
        if (view is Page page && AppNavigation.RequiresOpaqueModalBackground(page))
        {
            handler.PlatformView.SetBackgroundColor(page.BackgroundColor.ToPlatform());
            return;
        }

        var path = App.Services.GetService<IAppearanceService>()?.BackgroundImagePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            if (view is VisualElement element)
            {
                handler.PlatformView.SetBackgroundColor(element.BackgroundColor.ToPlatform());
            }
            return;
        }

        var bitmap = GetBitmap(path);
        if (bitmap is null)
        {
            return;
        }

        handler.PlatformView.Background = new AspectFillBitmapDrawable(bitmap);
    }

    private static Bitmap? GetBitmap(string path)
    {
        lock (BitmapLock)
        {
            if (string.Equals(_loadedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _loadedBitmap is { IsRecycled: false })
            {
                return _loadedBitmap;
            }

            var decoded = BitmapFactory.DecodeFile(path);
            if (decoded is null)
            {
                return null;
            }

            _loadedPath = path;
            _loadedBitmap = decoded;
            return decoded;
        }
    }

    private sealed class AspectFillBitmapDrawable(Bitmap bitmap) : Drawable
    {
        private readonly global::Android.Graphics.Paint _paint =
            new(PaintFlags.AntiAlias | PaintFlags.FilterBitmap | PaintFlags.Dither);

        public override void Draw(Canvas canvas)
        {
            var bounds = Bounds;
            if (bounds.Width() <= 0 || bounds.Height() <= 0 || bitmap.IsRecycled)
            {
                return;
            }

            var scale = Math.Max(
                bounds.Width() / (float)bitmap.Width,
                bounds.Height() / (float)bitmap.Height);
            var width = bitmap.Width * scale;
            var height = bitmap.Height * scale;
            var left = bounds.Left + (bounds.Width() - width) / 2f;
            var top = bounds.Top + (bounds.Height() - height) / 2f;

            canvas.DrawBitmap(
                bitmap,
                null,
                new global::Android.Graphics.RectF(left, top, left + width, top + height),
                _paint);
        }

        public override void SetAlpha(int alpha) => _paint.Alpha = alpha;

#pragma warning disable CS0672
        public override void SetColorFilter(ColorFilter? colorFilter) =>
            _paint.SetColorFilter(colorFilter);
#pragma warning restore CS0672

        public override int Opacity => (int)Format.Translucent;
    }
}
#endif
