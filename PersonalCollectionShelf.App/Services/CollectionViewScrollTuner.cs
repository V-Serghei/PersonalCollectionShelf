namespace PersonalCollectionShelf.App.Services;

internal static class CollectionViewScrollTuner
{
    public static void EnableFreeScrolling(CollectionView collectionView)
    {
#if ANDROID
        if (collectionView.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView)
        {
            // SnapHelper installs an OnFlingListener. Removing it restores normal
            // continuous RecyclerView motion instead of stopping at item boundaries.
            recyclerView.SetOnFlingListener(null);
            recyclerView.NestedScrollingEnabled = true;
        }
#endif
    }
}
