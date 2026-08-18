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

            SceneManager.LoadScene("LoadResources");
        }
    }
}
