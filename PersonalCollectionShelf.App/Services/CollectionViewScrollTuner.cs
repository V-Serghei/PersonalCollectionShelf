namespace PersonalCollectionShelf.App.Services;

internal static class CollectionViewScrollTuner
{
    private const int CachedViewCount = 160;
    private const int ExtraViewportCount = 10;

    public static void EnableFreeScrolling(CollectionView collectionView)
    {
#if ANDROID
        if (collectionView.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView)
        {
            // SnapHelper installs an OnFlingListener. Removing it restores normal
            // continuous RecyclerView motion instead of stopping at item boundaries.
            recyclerView.SetOnFlingListener(null);
            recyclerView.NestedScrollingEnabled = true;
            recyclerView.HasFixedSize = true;
            recyclerView.SetItemViewCacheSize(CachedViewCount);
            recyclerView.SetItemAnimator(null);

            var currentLayoutManager = recyclerView.GetLayoutManager();
            if (currentLayoutManager is AndroidX.RecyclerView.Widget.GridLayoutManager gridLayoutManager)
            {
                if (currentLayoutManager is BufferedGridLayoutManager bufferedGridLayoutManager)
                {
                    bufferedGridLayoutManager.ItemPrefetchEnabled = true;
                    bufferedGridLayoutManager.InitialPrefetchItemCount = CachedViewCount;
                }
                else
                {
                    var savedState = currentLayoutManager.OnSaveInstanceState();
                    var bufferedLayoutManager = new BufferedGridLayoutManager(
                        recyclerView.Context!,
                        gridLayoutManager.SpanCount,
                        gridLayoutManager.Orientation,
                        gridLayoutManager.ReverseLayout)
                    {
                        StackFromEnd = gridLayoutManager.StackFromEnd,
                        ItemPrefetchEnabled = true,
                        InitialPrefetchItemCount = CachedViewCount
                    };
                    bufferedLayoutManager.SetSpanSizeLookup(gridLayoutManager.GetSpanSizeLookup());

                    recyclerView.SetLayoutManager(bufferedLayoutManager);
                    if (savedState is not null)
                    {
                        bufferedLayoutManager.OnRestoreInstanceState(savedState);
                    }
                }
            }
            else if (currentLayoutManager is AndroidX.RecyclerView.Widget.LinearLayoutManager linearLayoutManager)
            {
                if (currentLayoutManager is BufferedLinearLayoutManager bufferedLinearLayoutManager)
                {
                    bufferedLinearLayoutManager.ItemPrefetchEnabled = true;
                    bufferedLinearLayoutManager.InitialPrefetchItemCount = CachedViewCount;
                }
                else
                {
                    var savedState = currentLayoutManager.OnSaveInstanceState();
                    var bufferedLayoutManager = new BufferedLinearLayoutManager(
                        recyclerView.Context!,
                        linearLayoutManager.Orientation,
                        linearLayoutManager.ReverseLayout)
                    {
                        StackFromEnd = linearLayoutManager.StackFromEnd,
                        ItemPrefetchEnabled = true,
                        InitialPrefetchItemCount = CachedViewCount
                    };

                    recyclerView.SetLayoutManager(bufferedLayoutManager);
                    if (savedState is not null)
                    {
                        bufferedLayoutManager.OnRestoreInstanceState(savedState);
                    }
                }
            }
        }
#endif
    }

#if ANDROID
    private static int GetExtraLayoutSpace(AndroidX.RecyclerView.Widget.RecyclerView.LayoutManager layoutManager)
    {
        var viewportSize = layoutManager.CanScrollVertically()
            ? layoutManager.Height
            : layoutManager.Width;

        return Math.Max(viewportSize, 1) * ExtraViewportCount;
    }

    private sealed class BufferedGridLayoutManager(
        Android.Content.Context context,
        int spanCount,
        int orientation,
        bool reverseLayout)
        : AndroidX.RecyclerView.Widget.GridLayoutManager(context, spanCount, orientation, reverseLayout)
    {
        protected override void CalculateExtraLayoutSpace(
            AndroidX.RecyclerView.Widget.RecyclerView.State? state,
            int[]? extraLayoutSpace)
        {
            if (extraLayoutSpace is null || extraLayoutSpace.Length < 2)
            {
                return;
            }

            var extraSpace = CollectionViewScrollTuner.GetExtraLayoutSpace(this);
            extraLayoutSpace[0] = extraSpace;
            extraLayoutSpace[1] = extraSpace;
        }
    }

    private sealed class BufferedLinearLayoutManager(
        Android.Content.Context context,
        int orientation,
        bool reverseLayout)
        : AndroidX.RecyclerView.Widget.LinearLayoutManager(context, orientation, reverseLayout)
    {
        protected override void CalculateExtraLayoutSpace(
            AndroidX.RecyclerView.Widget.RecyclerView.State? state,
            int[]? extraLayoutSpace)
        {
            if (extraLayoutSpace is null || extraLayoutSpace.Length < 2)
            {
                return;
            }

            var extraSpace = CollectionViewScrollTuner.GetExtraLayoutSpace(this);
            extraLayoutSpace[0] = extraSpace;
            extraLayoutSpace[1] = extraSpace;
        }
    }
#endif
}
