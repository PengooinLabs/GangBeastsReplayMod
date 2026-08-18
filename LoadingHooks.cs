using Il2CppGB.Core.Loading;

namespace PengooinLabs.ReplayMod
{
    public class LoadingHooks
    {
        public static event Action? onShowLoadingScreen;
        public static event Action<string>? onHideLoadingScreen;
        public static event Action<string>? onLoadingScreenHidden;
        
        public static HarmonyLib.Harmony? harmony;

        public static void init()
        {
            harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(LoadingPatch), null);
        }

        public class LoadingPatch
        {
            [HarmonyLib.HarmonyPatch(typeof(LoadScreenSystem), "HideLoadingScreen")]
            [HarmonyLib.HarmonyPostfix]
            public static void LoadScreenSystem_HideLoadingScreen(LoadScreenSystem __instance)
            {
                // note: this is not hiding the loading screen yet, but _starting_ to hide it

                string sceneName = Game.getSceneName();

                // our callback when loading screen starts to disappear
                if (onHideLoadingScreen != null) onHideLoadingScreen(sceneName);

                // hook callback that's called when loading screen is actually disappearing from screen
                __instance.internalOnHideCompleted += new Action(() =>
                {
                    // call our callback when loading screen has disappeared
                    if (onLoadingScreenHidden != null) onLoadingScreenHidden(sceneName);
                });
            }

            [HarmonyLib.HarmonyPatch(typeof(LoadScreenSystem), "ShowLoadingScreen")]
            [HarmonyLib.HarmonyPostfix]
            public static void LoadScreenSystem_ShowLoadingScreen(LoadScreenSystem __instance)
            {
                if (onShowLoadingScreen != null) onShowLoadingScreen();
            }
        }
    }
}
