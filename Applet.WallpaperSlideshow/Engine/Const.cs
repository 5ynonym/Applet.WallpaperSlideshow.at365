namespace at365.WallpaperSlideshow
{
    internal class Const
    {
        public static string AppFolderName = @"at365\Applets\WallpaperSlideshow";
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

        public static string ConfigFileName = "config.json";
        public static string ConfigPath = Path.Combine(AppDataFolder, ConfigFileName);

        public static string WallpaperPicturePath => Path.Combine(AppDataFolder, "wallpaper.bmp");
    }
}
