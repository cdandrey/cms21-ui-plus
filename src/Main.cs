using MelonLoader;
using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Tomlet;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
#else
using CMS;
#endif

namespace Cms21UiPlus
{
    public static class BuildInfo
    {
        public const string Name = "CMS21 UI+";
        public const string ShortName = "CMS21 UI+";
        public const string TechnicalName = "CMS21UIPlus";
        public const string Description = "Interface and usability improvements for Car Mechanic Simulator 2021";
        public const string Version = "4.2";
        public const string Author = "CMS21 UI Plus contributors";
        public const string Company = "CMS21 UI Plus";
        public const string DownloadLink = "";
        public const string MelonGameCompany = "Red Dot Games";
        public const string MelonGameName = "Car Mechanic Simulator 2021";
    }

    public sealed class Main : MelonMod
    {
        private static readonly string NewLine = Environment.NewLine;
        private static bool initialized;
        private static bool profileMemoryDirty;

        public static MelonPreferences_Entry<Settings> SettingsEntry;
        public static Types.ProfileMemoryData ProfileMemory = new Types.ProfileMemoryData();

        public override void OnLateInitializeMelon()
        {
            string startMessage = BuildInfo.Name + " v" + BuildInfo.Version +
                " initializing; Unity " + Application.unityVersion + ", game " +
                GameSettings.BuildVersion + ".";
            ModLogger.Log(startMessage, Types.LoggingLevels.NormalClean);
            ModLogger.Log(startMessage, Types.LoggingLevels.PlayerLog);

            GlobalState.GameManager = Singleton<GameManager>.Instance;
            GlobalState.IsMenuSceneActive =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu";
            DetectPlatform();
            bool melonDebug = Environment.GetCommandLineArgs().Contains("--melonloader.debug");

            LoadSettings();
            LoadProfileMemory();
            KeyBindingsConfig.Load(GlobalConfig.cfgKeyBindings);
            ModLogger.ConfigureUnityLogForwarding(melonDebug);
            ApplyHarmonyPatches();
            initialized = true;
        }

        public override void OnDeinitializeMelon()
        {
            if (!initialized)
                return;
            SaveProfileMemory();
            ModLogger.Log(BuildInfo.ShortName + " stopped.", Types.LoggingLevels.Normal);
            ModLogger.Shutdown();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            StartupPromptAutoContinueFeature.OnSceneUnloaded(sceneName);
            if (sceneName == "Menu")
                GlobalState.IsMenuSceneActive = false;

            bool garageStillLoaded = UnityEngine.SceneManagement.SceneManager
                .GetSceneByName("garage").isLoaded;
            GlobalState.IsGarageSceneActive = garageStillLoaded;
            if (garageStillLoaded)
                return;

            if (sceneName == "garage")
                SaveProfileMemory();

            InventoryFilterManager.ResetAll();
            FilteredWarehouseTransferFeature.Reset();
            ScrapInventoryFilterFeature.ResetAll();
            RepairInventoryFilterFeature.ResetAll();
            SpringClampInventoryFilterFeature.ResetAll();
            if (sceneName == "Menu")
                ModSettingsMenuFeature.ResetAll();
            GaragePauseExitFeature.OnGarageSceneUnloaded();
            BodyPartMarkingFeature.OnGarageSceneUnloaded();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!initialized || buildIndex == -1)
                return;
            StartupIntroSkipFeature.OnSceneLoaded(sceneName);
            StartupPromptAutoContinueFeature.OnSceneLoaded(sceneName);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (!initialized || buildIndex == -1)
                return;

            if (sceneName == "garage") {
                GlobalState.LoadedProfileId = NormalizeProfileId(
                    PlayerPrefs.GetInt("selectedProfile", 0));
                GlobalState.IsGarageSceneActive = true;
                OwnedPartCache.BeginRefreshAfterGarageLoad();
            }

            if (sceneName == "Menu") {
                GlobalState.IsMenuSceneActive = true;
                GlobalState.GameManager = Singleton<GameManager>.Instance;
            }
        }

        public override void OnUpdate()
        {
            if (!initialized)
                return;
            QuickMountModeSwitchFeature.Update();
            NativeUiFactory.UpdateControlHints();
            FilteredWarehouseTransferFeature.Update();
            InventoryFilterManager.UpdateResetShortcut();
            ScrapInventoryFilterFeature.Update();
            RepairInventoryFilterFeature.Update();
            SpringClampInventoryFilterFeature.Update();
            if (GlobalState.IsMenuSceneActive)
                ModSettingsMenuFeature.Update();
        }

        public static void MarkProfileMemoryDirty()
        {
            profileMemoryDirty = true;
        }

        private void ApplyHarmonyPatches()
        {
            Type[] patchTypes = typeof(Main).Assembly.GetTypes()
                .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            int applied = 0;
            int failed = 0;
            foreach (Type patchType in patchTypes) {
                try {
                    HarmonyInstance.CreateClassProcessor(patchType).Patch();
                    applied++;
                } catch (Exception exception) {
                    failed++;
                    ModLogger.Log("[Harmony] Patch class failed: " + patchType.FullName +
                        NewLine + exception, Types.LoggingLevels.Error);
                }
            }
            ModLogger.Log("Harmony patch classes: applied=" + applied + ", failed=" + failed + ".",
                failed == 0 ? Types.LoggingLevels.NormalClean : Types.LoggingLevels.Warning);
        }

        private static void DetectPlatform()
        {
            try {
                string platform = GlobalState.GameManager.PlatformManager.platform.ToString();
                ModLogger.Log("Platform " + platform, Types.LoggingLevels.NormalClean);
            } catch (Exception exception) {
                ModLogger.Log("[Startup] Platform detection failed." + NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void LoadSettings()
        {
            bool fileExisted = File.Exists(GlobalConfig.cfgFile);
            SettingsMigration.MigrateLegacySettings();
            MelonPreferences_Category preferences =
                MelonPreferences.CreateCategory(BuildInfo.TechnicalName);
            preferences.SetFilePath(GlobalConfig.cfgFile, autoload: false);
            SettingsEntry = preferences.CreateEntry<Settings>("Settings", new Settings(),
                null, BuildInfo.ShortName + " feature switches");
            preferences.LoadFromFile();
            if (!fileExisted)
                preferences.SaveToFile(false);
        }

        private static void LoadProfileMemory()
        {
            GlobalState.LoadedProfileId = NormalizeProfileId(
                PlayerPrefs.GetInt("selectedProfile", 0));
            profileMemoryDirty = !File.Exists(GlobalConfig.cfgProfile);
            try {
                if (!profileMemoryDirty) {
                    Types.ProfileMemoryData loaded = TomletMain.To<Types.ProfileMemoryData>(
                        TomlParser.ParseFile(GlobalConfig.cfgProfile));
                    if (loaded != null)
                        ProfileMemory = loaded;
                }
            } catch (Exception exception) {
                ModLogger.Log("[ProfileMemory] Existing file is invalid; a clean state will be written." +
                    NewLine + exception, Types.LoggingLevels.Warning);
                ProfileMemory = new Types.ProfileMemoryData();
                profileMemoryDirty = true;
            }
            if (EnsureProfileArray())
                profileMemoryDirty = true;
            if (!string.Equals(ProfileMemory.lastCMS21UIPlusVersion, BuildInfo.Version,
                StringComparison.Ordinal)) {
                ProfileMemory.lastCMS21UIPlusVersion = BuildInfo.Version;
                profileMemoryDirty = true;
            }
            SaveProfileMemory();
        }

        private static bool EnsureProfileArray()
        {
            bool changed = false;
            if (ProfileMemory == null) {
                ProfileMemory = new Types.ProfileMemoryData();
                changed = true;
            }
            if (ProfileMemory.profileStates == null || ProfileMemory.profileStates.Length != 4) {
                Types.ProfileState[] previous = ProfileMemory.profileStates;
                Types.ProfileState[] normalized = new Types.ProfileState[4];
                if (previous != null) {
                    int count = Math.Min(previous.Length, normalized.Length);
                    for (int i = 0; i < count; i++)
                        normalized[i] = previous[i];
                }
                ProfileMemory.profileStates = normalized;
                changed = true;
            }
            for (int i = 0; i < ProfileMemory.profileStates.Length; i++) {
                if (ProfileMemory.profileStates[i] == null) {
                    ProfileMemory.profileStates[i] = new Types.ProfileState();
                    changed = true;
                }
            }
            return changed;
        }

        private static void SaveProfileMemory()
        {
            if (!profileMemoryDirty)
                return;
            try {
                if (EnsureProfileArray())
                    profileMemoryDirty = true;
                File.WriteAllText(GlobalConfig.cfgProfile, TomletMain.TomlStringFrom(ProfileMemory));
                profileMemoryDirty = false;
            } catch (Exception exception) {
                ModLogger.Log("[ProfileMemory] Save failed." + NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        private static int NormalizeProfileId(int profileId)
        {
            return profileId >= 0 && profileId < 4 ? profileId : 0;
        }
    }
}
