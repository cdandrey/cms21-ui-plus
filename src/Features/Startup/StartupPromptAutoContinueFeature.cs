using MelonLoader;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cms21UiPlus
{
    /// <summary>
    /// Continues past the startup confirmation prompt by invoking the game's
    /// own scene-loading coroutine instead of synthesizing keyboard input.
    /// </summary>
    public static class StartupPromptAutoContinueFeature
    {
        private const string StartupSceneName = "LoadResources";
        private const float PromptTimeoutSeconds = 15f;

        private static bool workerRunning;
        private static bool transitionRequested;
        private static int sceneGeneration;

        public static void OnSceneLoaded(string sceneName)
        {
            if (sceneName != StartupSceneName || Main.SettingsEntry == null ||
                !Main.SettingsEntry.Value.autoContinueStartupPrompt ||
                workerRunning || transitionRequested)
                return;

            int generation = ++sceneGeneration;
            workerRunning = true;
            MelonCoroutines.Start(ContinueWhenReady(generation));
        }

        public static void OnSceneUnloaded(string sceneName)
        {
            if (sceneName != StartupSceneName)
                return;

            sceneGeneration++;
            workerRunning = false;
            transitionRequested = false;
        }

        private static IEnumerator ContinueWhenReady(int generation)
        {
            float deadline = Time.realtimeSinceStartup + PromptTimeoutSeconds;

            try {
                MenuPressButton button = null;
                while (IsCurrentStartupScene(generation) &&
                    Time.realtimeSinceStartup < deadline) {
                    if (button == null)
                        button = UnityEngine.Object.FindObjectOfType<MenuPressButton>();

                    if (button != null &&
                        button.MenuState != MenuPressButtonState.Lock) {
                        if (button.changeLevelInProgress || transitionRequested)
                            yield break;

                        transitionRequested = true;
                        try {
                            ModLogger.Log(
                                "[Startup] Continuing past the startup prompt.",
                                Types.LoggingLevels.Normal);
                            button.StartCoroutine(button.LoadScene());
                        } catch (Exception exception) {
                            transitionRequested = false;
                            ModLogger.Log(
                                "[Startup] Automatic prompt continuation failed; " +
                                "manual confirmation remains available." +
                                Environment.NewLine + exception,
                                Types.LoggingLevels.Error);
                        }
                        yield break;
                    }

                    yield return null;
                }

                if (IsCurrentStartupScene(generation) &&
                    !transitionRequested) {
                    ModLogger.Log(
                        "[Startup] Startup prompt was not ready within " +
                        PromptTimeoutSeconds +
                        " seconds; manual confirmation remains available.",
                        Types.LoggingLevels.Warning);
                }
            } finally {
                if (generation == sceneGeneration)
                    workerRunning = false;
            }
        }

        private static bool IsCurrentStartupScene(int generation)
        {
            return generation == sceneGeneration &&
                SceneManager.GetSceneByName(StartupSceneName).isLoaded;
        }
    }
}
