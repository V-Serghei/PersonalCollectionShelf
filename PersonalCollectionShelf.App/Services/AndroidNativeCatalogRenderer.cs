#if ANDROID
using System.Collections.Concurrent;
using System.Collections.Specialized;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using PersonalCollectionShelf.App.ViewModels;
using AColor = Android.Graphics.Color;
using AView = Android.Views.View;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace PersonalCollectionShelf.App.Services;

/// <summary>
/// Uses a small native Android view hierarchy instead of materializing a full
/// MAUI visual tree for every visible catalog card. The adapter always owns the
/// complete in-memory list; RecyclerView only recycles its lightweight views.
/// </summary>
internal static class AndroidNativeCatalogRenderer
{
    public static NativeCatalogBinding<MediaItemListItemViewModel>? AttachMedia(
        CollectionView collectionView,
        IList<MediaItemListItemViewModel> items,
        bool grid,
        int span,
        Action<MediaItemListItemViewModel> open)
    {
        if (collectionView.Handler?.PlatformView is not RecyclerView recyclerView) return null;
        Prepare(collectionView, recyclerView, grid, span);
        var adapter = new MediaAdapter(recyclerView, grid, Math.Clamp(span, 1, 4), open);
        var binding = new NativeCatalogBinding<MediaItemListItemViewModel>(recyclerView, adapter);
        binding.Update(items);
        recyclerView.SetAdapter(adapter);
        return binding;
    }

    public static NativeCatalogBinding<PersonCardViewModel>? AttachPeople(
        CollectionView collectionView,
        IList<PersonCardViewModel> items,
        bool grid,
        int span,
        Action<PersonCardViewModel> open)
    {
        if (collectionView.Handler?.PlatformView is not RecyclerView recyclerView) return null;
        Prepare(collectionView, recyclerView, grid, span);
        var adapter = new PeopleAdapter(recyclerView, grid, Math.Clamp(span, 1, 4), open);
        var binding = new NativeCatalogBinding<PersonCardViewModel>(recyclerView, adapter);
        binding.Update(items);
        recyclerView.SetAdapter(adapter);
        return binding;
    }

    private static void Prepare(CollectionView collectionView, RecyclerView recyclerView, bool grid, int span)
    {
        // Disconnect MAUI's templated adapter; otherwise it can replace the
        // native adapter when a filter changes the observable collection.
        collectionView.RemoveBinding(ItemsView.ItemsSourceProperty);
        collectionView.ItemsSource = null;
        recyclerView.ClearOnScrollListeners();
        recyclerView.SetOnFlingListener(null);
        recyclerView.SetItemAnimator(null);
        recyclerView.HasFixedSize = true;
        recyclerView.NestedScrollingEnabled = true;
        recyclerView.SetItemViewCacheSize(32);
        recyclerView.GetRecycledViewPool().SetMaxRecycledViews(0, 48);
        recyclerView.SetLayoutManager(grid
            ? new GridLayoutManager(recyclerView.Context, Math.Clamp(span, 1, 4))
            : new LinearLayoutManager(recyclerView.Context));
    }

    internal sealed class NativeCatalogBinding<T> : IDisposable where T : class
    {
        private readonly RecyclerView _recyclerView;
        private readonly NativeAdapter<T> _adapter;

        public NativeCatalogBinding(RecyclerView recyclerView, NativeAdapter<T> adapter)
        {
            _recyclerView = recyclerView;
            _adapter = adapter;
        }

        public bool IsCurrent(CollectionView collectionView) =>
            collectionView.Handler?.PlatformView == _recyclerView;

        public void Update(IList<T> items) => _adapter.SetItems(items);

        public void Dispose()
        {
            _adapter.SetItems(Array.Empty<T>());
            if (_recyclerView.GetAdapter() == _adapter) _recyclerView.SetAdapter(null);
            _adapter.Dispose();
        }
    }

    internal abstract class NativeAdapter<T> : RecyclerView.Adapter where T : class
    {
        private IList<T> _items = Array.Empty<T>();
        private INotifyCollectionChanged? _observable;

        protected NativeAdapter(RecyclerView recyclerView)
        {
            RecyclerView = recyclerView;
            HasStableIds = true;
        }

        protected RecyclerView RecyclerView { get; }
        protected T ItemAt(int position) => _items[position];
        public override int ItemCount => _items.Count;
        public override long GetItemId(int position) => ItemAt(position).GetHashCode();

        public void SetItems(IList<T> items)
        {
            if (ReferenceEquals(_items, items))
            {
                NotifyDataSetChanged();
                return;
            }

            if (_observable is not null) _observable.CollectionChanged -= HandleCollectionChanged;
            _items = items;
            _observable = items as INotifyCollectionChanged;
            if (_observable is not null) _observable.CollectionChanged += HandleCollectionChanged;
            NotifyDataSetChanged();
        }

        private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            RecyclerView.Post(NotifyDataSetChanged);

        protected override void Dispose(bool disposing)
        {
            if (disposing && _observable is not null) _observable.CollectionChanged -= HandleCollectionChanged;
            base.Dispose(disposing);
        }
    }

    private sealed class MediaAdapter(
        RecyclerView recyclerView,
        bool grid,
        int span,
        Action<MediaItemListItemViewModel> open) : NativeAdapter<MediaItemListItemViewModel>(recyclerView)
    {
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) =>
            new MediaHolder(parent, grid, span, open);

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position) =>
            ((MediaHolder)holder).Bind(ItemAt(position));
    }

    private sealed class PeopleAdapter(
        RecyclerView recyclerView,
        bool grid,
        int span,
        Action<PersonCardViewModel> open) : NativeAdapter<PersonCardViewModel>(recyclerView)
    {
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) =>
            new PersonHolder(parent, grid, span, open);

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position) =>
            ((PersonHolder)holder).Bind(ItemAt(position));
    }

    private sealed class MediaHolder : RecyclerView.ViewHolder
    {
        private readonly bool _grid;
        private readonly int _span;
        private readonly Action<MediaItemListItemViewModel> _open;
        private readonly ImageView _image;
        private readonly TextView _placeholder;
        private readonly TextView _title;
        private readonly TextView _subtitle;
        private readonly TextView _trailing;
        private MediaItemListItemViewModel? _item;
        private string _imageKey = string.Empty;

        public MediaHolder(ViewGroup parent, bool grid, int span, Action<MediaItemListItemViewModel> open)
            : base(CreateRoot(parent, grid, out var image, out var placeholder, out var title, out var subtitle, out var trailing))
        {
            _grid = grid;
            _span = span;
            _open = open;
            _image = image;
            _placeholder = placeholder;
            _title = title;
            _subtitle = subtitle;
            _trailing = trailing;
            ItemView.Click += (_, _) => { if (_item is not null) _open(_item); };
        }

        public void Bind(MediaItemListItemViewModel item)
        {
            _item = item;
            _title.Text = item.Title;
            _subtitle.Text = _grid
                ? (_span >= 3 ? $"{item.StatusIcon}  {item.ReleaseYearText}" : $"{item.StatusLabel}  ·  {item.CategoryLine}")
                : $"{item.CategoryLine}  ·  {item.StatusLabel}";
            _trailing.Text = item.HasRating ? $"★ {item.RatingShort}" : item.ReleaseYearText;
            BindImage(_image, _placeholder, item.OriginalCoverUrl, item.Initial, key => _imageKey = key, () => _imageKey);
        }

        private static AView CreateRoot(
            ViewGroup parent,
            bool grid,
            out ImageView image,
            out TextView placeholder,
            out TextView title,
            out TextView subtitle,
            out TextView trailing)
        {
            var context = parent.Context!;
            var root = new LinearLayout(context) { Orientation = grid ? Orientation.Vertical : Orientation.Horizontal, Clickable = true };
            root.SetPadding(Dp(context, grid ? 0 : 8), Dp(context, grid ? 0 : 7), Dp(context, grid ? 0 : 8), Dp(context, grid ? 7 : 7));
            root.Background = Rounded(context, ResourceColor("CardBackground", AColor.Rgb(31, 27, 42)), ResourceColor("Border", AColor.Rgb(65, 58, 79)), 11);
            root.Elevation = Dp(context, 1);
            var rootParams = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            rootParams.SetMargins(Dp(context, 4), Dp(context, 4), Dp(context, 4), Dp(context, 6));
            root.LayoutParameters = rootParams;

            var imageFrame = new FrameLayout(context);
            imageFrame.LayoutParameters = grid
                ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(context, 170))
                : new LinearLayout.LayoutParams(Dp(context, 54), Dp(context, 72));
            imageFrame.Background = Rounded(context, ResourceColor("Muted", AColor.Rgb(42, 37, 53)), AColor.Transparent, 8);
            image = new ImageView(context);
            image.SetScaleType(ImageView.ScaleType.CenterCrop);
            imageFrame.AddView(image, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            placeholder = Text(context, 24, true, ResourceColor("Primary", AColor.Rgb(151, 121, 242)), 1);
            placeholder.Gravity = GravityFlags.Center;
            imageFrame.AddView(placeholder, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            root.AddView(imageFrame);

            var textArea = new LinearLayout(context) { Orientation = Orientation.Vertical };
            textArea.SetPadding(Dp(context, 8), Dp(context, grid ? 7 : 3), Dp(context, 8), 0);
            textArea.LayoutParameters = grid
                ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                : new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
            title = Text(context, grid ? 12 : 14, true, ResourceColor("Foreground", AColor.White), grid ? 2 : 1);
            subtitle = Text(context, grid ? 10 : 11, false, ResourceColor("MutedForeground", AColor.LightGray), grid ? 2 : 1);
            trailing = Text(context, 11, true, AColor.Rgb(245, 190, 80), 1);
            textArea.AddView(title);
            textArea.AddView(subtitle);
            textArea.AddView(trailing);
            root.AddView(textArea);
            return root;
        }
    }

    private sealed class PersonHolder : RecyclerView.ViewHolder
    {
        private readonly bool _grid;
        private readonly Action<PersonCardViewModel> _open;
        private readonly ImageView _image;
        private readonly TextView _placeholder;
        private readonly TextView _name;
        private readonly TextView _details;
        private readonly TextView _rating;
        private PersonCardViewModel? _item;
        private string _imageKey = string.Empty;

        public PersonHolder(ViewGroup parent, bool grid, int span, Action<PersonCardViewModel> open)
            : base(CreateRoot(parent, grid, out var image, out var placeholder, out var name, out var details, out var rating))
        {
            _grid = grid;
            _open = open;
            _image = image;
            _placeholder = placeholder;
            _name = name;
            _details = details;
            _rating = rating;
            ItemView.Click += (_, _) => { if (_item is not null) _open(_item); };
        }

        public void Bind(PersonCardViewModel item)
        {
            _item = item;
            _name.Text = item.Name;
            _details.Text = _grid ? item.RoleSummary : $"{item.RoleSummary}\n{item.TopWorksSummary}";
            _rating.Text = item.HasRating ? $"★ {item.AverageRatingText}  ·  {item.WorkCountText}" : item.WorkCountText;
            BindImage(_image, _placeholder, item.OriginalPhotoPath, item.Initial, key => _imageKey = key, () => _imageKey);
        }

        private static AView CreateRoot(
            ViewGroup parent,
            bool grid,
            out ImageView image,
            out TextView placeholder,
            out TextView name,
            out TextView details,
            out TextView rating)
        {
            var context = parent.Context!;
            var root = new LinearLayout(context) { Orientation = grid ? Orientation.Vertical : Orientation.Horizontal, Clickable = true };
            root.SetPadding(Dp(context, grid ? 0 : 8), Dp(context, grid ? 0 : 7), Dp(context, grid ? 0 : 8), Dp(context, 7));
            root.Background = Rounded(context, ResourceColor("CardBackground", AColor.Rgb(31, 27, 42)), ResourceColor("Border", AColor.Rgb(65, 58, 79)), 11);
            var rootParams = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            rootParams.SetMargins(Dp(context, 4), Dp(context, 4), Dp(context, 4), Dp(context, 6));
            root.LayoutParameters = rootParams;

            var frame = new FrameLayout(context);
            frame.LayoutParameters = grid
                ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(context, 112))
                : new LinearLayout.LayoutParams(Dp(context, 72), Dp(context, 88));
            frame.Background = Rounded(context, ResourceColor("Muted", AColor.Rgb(42, 37, 53)), AColor.Transparent, 8);
            image = new ImageView(context);
            image.SetScaleType(ImageView.ScaleType.CenterCrop);
            frame.AddView(image, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            placeholder = Text(context, 23, true, ResourceColor("Primary", AColor.Rgb(151, 121, 242)), 1);
            placeholder.Gravity = GravityFlags.Center;
            frame.AddView(placeholder, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            root.AddView(frame);

            var textArea = new LinearLayout(context) { Orientation = Orientation.Vertical };
            textArea.SetPadding(Dp(context, 8), Dp(context, 6), Dp(context, 8), 0);
            textArea.LayoutParameters = grid
                ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                : new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
            name = Text(context, grid ? 13 : 15, true, ResourceColor("Foreground", AColor.White), grid ? 2 : 1);
            details = Text(context, grid ? 10 : 11, false, ResourceColor("MutedForeground", AColor.LightGray), grid ? 2 : 2);
            rating = Text(context, 10, true, AColor.Rgb(245, 190, 80), 1);
            textArea.AddView(name);
            textArea.AddView(details);
            textArea.AddView(rating);
            root.AddView(textArea);
            return root;
        }
    }

    private static TextView Text(Android.Content.Context context, float size, bool bold, AColor color, int lines)
    {
        var view = new TextView(context);
        view.SetTextSize(Android.Util.ComplexUnitType.Sp, size);
        view.SetTextColor(color);
        view.SetMaxLines(lines);
        view.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        if (bold) view.SetTypeface(view.Typeface, TypefaceStyle.Bold);
        return view;
    }

    private static void BindImage(
        ImageView image,
        TextView placeholder,
        string source,
        string initial,
        Action<string> setKey,
        Func<string> getKey)
    {
        setKey(source);
        image.SetImageDrawable(null);
        placeholder.Text = initial;
        placeholder.Visibility = ViewStates.Visible;
        if (string.IsNullOrWhiteSpace(source)) return;

        _ = NativeImageLoader.LoadAsync(source).ContinueWith(task =>
        {
            var bitmap = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            if (bitmap is null) return;
            image.Post(() =>
            {
                if (!string.Equals(getKey(), source, StringComparison.Ordinal)) return;
                image.SetImageBitmap(bitmap);
                placeholder.Visibility = ViewStates.Gone;
            });
        }, TaskScheduler.Default);
    }

    private static GradientDrawable Rounded(Android.Content.Context context, AColor fill, AColor stroke, int radius)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(fill);
        drawable.SetCornerRadius(Dp(context, radius));
        if (stroke != AColor.Transparent) drawable.SetStroke(Dp(context, 1), stroke);
        return drawable;
    }

    private static int Dp(Android.Content.Context context, int value) =>
        (int)Math.Round(value * context.Resources!.DisplayMetrics!.Density);

    private static AColor ResourceColor(string key, AColor fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is MauiColor color)
        {
            return AColor.Argb((int)(color.Alpha * 255), (int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255));
        }
        return fallback;
    }

    private static class NativeImageLoader
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly SemaphoreSlim DecodeGate = new(3, 3);
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Pending = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> Cache = new(StringComparer.Ordinal);

        public static Task<Bitmap?> LoadAsync(string source)
        {
            if (Cache.TryGetValue(source, out var reference) && reference.TryGetTarget(out var cached) && !cached.IsRecycled)
                return Task.FromResult<Bitmap?>(cached);
            return Pending.GetOrAdd(source, LoadCoreAsync);
        }

        private static async Task<Bitmap?> LoadCoreAsync(string source)
        {
            await DecodeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Bitmap? bitmap;
                if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                {
                    var bytes = await Client.GetByteArrayAsync(uri).ConfigureAwait(false);
                    bitmap = Decode(bytes);
                }
                else
                {
                    var path = Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.IsFile ? uri.LocalPath : source;
                    bitmap = await Task.Run(() => DecodeFile(path)).ConfigureAwait(false);
                }

                if (bitmap is not null) Cache[source] = new WeakReference<Bitmap>(bitmap);
                return bitmap;
            }
            catch
            {
                return null;
            }
            finally
            {
                Pending.TryRemove(source, out _);
                DecodeGate.Release();
            }
        }

        private static Bitmap? DecodeFile(string path)
        {
            if (!File.Exists(path)) return null;
            var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(path, bounds);
            var options = new BitmapFactory.Options { InSampleSize = Sample(bounds.OutWidth, bounds.OutHeight) };
            return BitmapFactory.DecodeFile(path, options);
        }

        private static Bitmap? Decode(byte[] bytes)
        {
            var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
            var options = new BitmapFactory.Options { InSampleSize = Sample(bounds.OutWidth, bounds.OutHeight) };
            return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        }

        private static int Sample(int width, int height)
        {
            var sample = 1;
            while (width / (sample * 2) >= 480 && height / (sample * 2) >= 480) sample *= 2;
            return sample;
        }
    }
}
#endif
