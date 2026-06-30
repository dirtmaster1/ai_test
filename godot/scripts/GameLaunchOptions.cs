public enum GameLaunchMode
{
    Auto,
    NewGame,
    LoadGame
}

public static class GameLaunchOptions
{
    public static GameLaunchMode Mode { get; set; } = GameLaunchMode.Auto;
}
