namespace at365.WallpaperSlideshow
{
    public static class StableScreensProvider
    {
        private static Screen[]? _cachedScreens;
        private static readonly object _lock = new();

        public static Screen[] Screens
        {
            get
            {
                lock (_lock)
                {
                    return _cachedScreens ??= LoadScreens();
                }
            }
        }

        public static void Refresh()
        {
            lock (_lock)
            {
                _cachedScreens = LoadScreens();
            }
        }

        private static Screen[] LoadScreens()
        {
            return Screen.AllScreens
                .OrderBy(s => s.Bounds.Left)
                .ThenBy(s => s.Bounds.Top)
                .ToArray();
        }
    }
}
