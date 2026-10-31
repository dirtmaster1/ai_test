public enum GameLaunchMode
{
    Auto,
    NewGame,
    LoadGame
}

public static class GameLaunchOptions
{
    public static GameLaunchMode Mode { get; set; } = GameLaunchMode.Auto;
    public static string SavePath { get; set; } = SaveGameSlots.AutosavePath;
    public static string LoadError { get; set; } = "";
}
