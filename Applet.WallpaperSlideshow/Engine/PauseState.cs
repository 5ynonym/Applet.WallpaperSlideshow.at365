namespace at365.WallpaperSlideshow;

internal sealed class PauseState
{
    public bool Manual { get; set; }
    public bool SessionLocked { get; set; }
    public bool Remote { get; set; }
    public bool IsPaused => Manual || SessionLocked || Remote;
}
