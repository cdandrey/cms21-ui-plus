using UnityEngine.SceneManagement;

namespace Cms21UiPlus
{
    /// <summary>Skips the startup intro scenes.</summary>
    public static class StartupIntroSkipFeature
    {
        public static void OnSceneLoaded(string sceneName)
        {
            if (sceneName != "IntroPlayWay" || Main.SettingsEntry == null ||
                !Main.SettingsEntry.Value.skipStartupVideosTotally)
                return;

            ModLogger.Log("[Startup] Skipping intro scenes.",
                Types.LoggingLevels.Normal);
            SceneManager.LoadScene("LoadResources");
        }
    }
}
