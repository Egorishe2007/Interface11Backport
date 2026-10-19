using System;
using System.Linq;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's loading screen at the game's start (level210's Canvas/SplashScreen, against 0.16's):
    // - the pictures (Resources ui/splash): 1.2 keeps 0.16's fifteen, the same pictures, and adds four, shipped in
    //   ui/splash. Each of the nineteen comes up as often as the others; a picture is loaded only when it is picked;
    // - the copyright line as 1.2's: the year 2026 (the same words otherwise), 1.2's (c) icon 12 x 12 in its grey-blue,
    //   no Quixel logo, so the description beside it is wider, its line broken where 1.2's is.
    internal static class Splash10
    {
        private static readonly string[] Added = { "splash_16", "splash_17", "splash_18", "splash_19_season_1" };

        // MENUTWEAKS_SPLASH=<name>: that picture every time, for the tests
        private static string Forced => Environment.GetEnvironmentVariable("MENUTWEAKS_SPLASH");

        // One of 1.2's pictures in place of the game's pick, or null to keep the game's own.
        private static Sprite Pick(Sprite[] game)
        {
            var count = game?.Length ?? 0;
            var forced = Forced;
            string name = null;
            if (!string.IsNullOrEmpty(forced))
            {
                name = Added.FirstOrDefault(n => n == forced);
            }
            else
            {
                var at = UnityEngine.Random.Range(0, count + Added.Length);
                name = at >= count ? Added[at - count] : null;
            }

            return name != null ? Ui.FileSprite("splash/" + name) : null;
        }

        private static readonly Color IconColor = new Color(0.808f, 0.863f, 0.886f, 1f);

        private static void Copyright(SplashScreenPanel panel)
        {
            var info = panel.transform.Find("Info");
            if (info == null || info.GetComponent<Restyled>() != null)
            {
                return;
            }

            info.gameObject.AddComponent<Restyled>();
            info.Find("Quixel")?.gameObject.SetActive(false);
            if (info.Find("Copyright/Text")?.GetComponent<TMP_Text>() is TMP_Text line && line.text.StartsWith("2025 ", StringComparison.Ordinal))
            {
                line.text = "2026" + line.text.Substring(4);
            }

            if (info.Find("Copyright/IconMargins") is Transform margins)
            {
                (margins.GetComponent<LayoutElement>() ?? margins.gameObject.AddComponent<LayoutElement>()).flexibleHeight = 1f;
                if (margins.Find("Icon")?.GetComponent<Image>() is Image icon && Ui.FileSprite("splash/copyright_icon_new") is Sprite sign)
                {
                    icon.sprite = sign;
                    icon.color = IconColor;
                    icon.preserveAspect = true;
                    var size = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
                    size.minWidth = size.minHeight = size.preferredWidth = size.preferredHeight = 12f;
                }
            }

            if (info.Find("Description") is RectTransform description)
            {
                Ui.Place(description, new[] { 0f, 0f, 1f, 0f, -80.5f, 43f, -639f, 0f, 0.5f, 0f });
                if (description.GetComponent<TMP_Text>() is TMP_Text text)
                {
                    text.text = text.text.Replace("used in the game, are not", "used in the game,\nare not");
                }
            }
        }

        // The game draws its pick from all but its last picture: here every picture of the game's and 1.2's.
        [HarmonyPatch(typeof(SplashScreenPanel), nameof(SplashScreenPanel.RandomSprite), MethodType.Getter)]
        internal static class PickPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(SplashScreenPanel __instance, ref Sprite __result)
            {
                if (!Interface10Plugin.Splash.Value)
                {
                    return true;
                }

                try
                {
                    Copyright(__instance);
                    var game = __instance._sprites ?? new Sprite[0];
                    var forced = Forced;
                    __result = Pick(game) ?? (string.IsNullOrEmpty(forced) ? null : game.FirstOrDefault(s => s != null && s.name == forced))
                        ?? (game.Length > 0 ? game[UnityEngine.Random.Range(0, game.Length)] : null);
                    Interface10Plugin.Log.LogInfo($"Loading picture: {__result?.name} ({game.Length + Added.Length} in all)");
                    return __result == null;
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Loading picture: {ex.Message}");
                    return true;
                }
            }
        }

        // The panel may be up before the plugin loads: then 1.2's pictures get their share in place of its picture.
        internal static void Late()
        {
            if (!Interface10Plugin.Splash.Value)
            {
                return;
            }

            try
            {
                foreach (var panel in Resources.FindObjectsOfTypeAll<SplashScreenPanel>().Where(p => p.gameObject.scene.IsValid() && p.isActiveAndEnabled))
                {
                    Copyright(panel);
                    var mine = Pick(panel._sprites);
                    if (mine != null)
                    {
                        foreach (var image in panel._images.Where(i => i != null))
                        {
                            image.sprite = mine;
                        }
                    }

                    Interface10Plugin.Log.LogInfo($"Loading picture, already shown: {(mine != null ? mine.name : "the game's")}");
                }
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Loading picture: {ex.Message}");
            }
        }
    }
}
