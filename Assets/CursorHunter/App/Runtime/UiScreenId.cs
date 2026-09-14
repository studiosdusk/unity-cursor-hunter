namespace CursorHunter.App
{
    /// <summary>
    /// Identifies the mutually exclusive application screens hosted by Main.
    /// UI navigation uses this stable id instead of toggling scene objects
    /// directly from individual buttons.
    /// </summary>
    public enum UiScreenId
    {
        MainMenu = 0,
        FieldSelect = 1,
        BossSelect = 2,
        Trait = 3,
        Settings = 4,
        Combat = 5
    }
}
