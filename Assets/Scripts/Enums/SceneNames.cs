namespace Enums
{
    /// <summary>
    /// Which loaded scenes count as the match itself. Besides Game, the provisional test levels in
    /// Assets/Scenes/Prototypes (Game1, Game2...) behave like it, so audience, chat and music still run there.
    /// </summary>
    public static class SceneNames
    {
        public static bool IsGame(string sceneName)
        {
            if (sceneName == nameof(Scenes.Game)) return true;

            return sceneName != null
                   && sceneName.Length > 4
                   && sceneName.StartsWith(nameof(Scenes.Game))
                   && char.IsDigit(sceneName[4]);
        }
    }
}
