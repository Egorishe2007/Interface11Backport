using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using EFT.UI;

namespace Interface10
{
    // 1.2's redrawn icons. 1.2 drew a large part of the interface's icons again under the same sprite names, square and
    // mostly bigger (the stash filters 30, the health icons 33 x 32, the context menu's 28, the tabs' 24...). The ones
    // shipped in ui/icons12 (compared by name and picture with 0.16's, see the item 11 notes) take the place of 0.16's
    // wherever an image gets one: when its sprite is set, its override sprite is set or it is switched on. An image
    // drawn at 0.16's sprite size gets 1.2's (as 1.2's layouts have them), and every swapped icon keeps its proportions.
    // The item attributes' icons (icon_info_*) keep their boxes: 1.2 draws its 28 x 28 ones in 0.16's 18 x 18.
    // The task bar's CHARACTER takes 1.2's own sprite there (mainmenu_character_icon, a seated figure) instead of
    // the character screen's OVERALL tab icon 0.16 lends it.
    internal static class Icons12
    {
        private static HashSet<string> _names;
        private static bool? _on;
        // the game's sprites already looked at: their 1.2 picture, or null for none
        private static readonly Dictionary<Sprite, Sprite> Swaps = new Dictionary<Sprite, Sprite>();
        private static readonly HashSet<Sprite> Ours = new HashSet<Sprite>();
        // the few frames redrawn by 1.2 (cut into nine): their 1.2 borders, left, bottom, right, top
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            ["checkbox"] = new Vector4(3f, 3f, 3f, 3f),
        };

        private static bool On => _on ?? (_on = Interface10Plugin.Icons12.Value).Value;

        private static HashSet<string> Names
        {
            get
            {
                if (_names == null)
                {
                    _names = new HashSet<string>(StringComparer.Ordinal);
                    var dir = Path.Combine(Interface10Plugin.Dir, "ui", "icons12");
                    if (Directory.Exists(dir))
                    {
                        foreach (var file in Directory.GetFiles(dir, "*.png"))
                        {
                            _names.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }

                    Interface10Plugin.Log.LogInfo($"1.2 icons: {_names.Count}");
                }

                return _names;
            }
        }

        // The 1.2 picture of one of the game's sprites, null when it has none.
        internal static Sprite For(Sprite sprite)
        {
            if (sprite == null || Ours.Contains(sprite))
            {
                return null;
            }

            if (Swaps.TryGetValue(sprite, out var mine))
            {
                return mine;
            }

            // the item pictures the game makes have no name: not kept, there are many and they come and go
            var name = sprite.name;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // plain icons, and the few frames listed with their 1.2 borders: other sprites cut into nine are left alone
            mine = !Names.Contains(name) ? null
                : sprite.border == Vector4.zero ? Ui.FileSprite("icons12/" + name)
                : Borders.TryGetValue(name, out var border) ? Ui.FileSprite("icons12/" + name, border) : null;
            if (mine != null)
            {
                Ours.Add(mine);
            }

            if (Swaps.Count > 20000)
            {
                Swaps.Clear();
            }

            Swaps[sprite] = mine;
            return mine;
        }

        // An image sized to 0.16's sprite (by its rect or its layout element) gets the size of 1.2's.
        private static void Fit(Image image, Sprite old, Sprite mine)
        {
            var was = old.rect.size;
            var now = mine.rect.size;
            var rect = image.rectTransform;
            if (rect.anchorMin == rect.anchorMax && Near(rect.sizeDelta, was))
            {
                rect.sizeDelta = now;
            }

            var element = image.GetComponent<LayoutElement>();
            if (element != null && Near(new Vector2(element.preferredWidth, element.preferredHeight), was))
            {
                element.preferredWidth = now.x;
                element.preferredHeight = now.y;
            }

            image.preserveAspect = true;
        }

        private static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.6f && Mathf.Abs(a.y - b.y) < 0.6f;

        private static Sprite Swap(Image image, Sprite sprite)
        {
            if (!On)
            {
                return null;
            }

            var mine = For(sprite);
            if (mine != null && mine.border != Vector4.zero)
            {
                // a frame: its size is the image's own, whatever its type
                return image.type == Image.Type.Filled ? null : mine;
            }

            if (image.type != Image.Type.Simple && image.type != Image.Type.Filled)
            {
                return null;
            }

            if (mine != null)
            {
                if (sprite.name.StartsWith("icon_info_", StringComparison.Ordinal))
                {
                    image.preserveAspect = true;
                }
                else
                {
                    Fit(image, sprite, mine);
                }
            }

            return mine;
        }

        [HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
        internal static class SpritePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Image __instance, ref Sprite value)
            {
                try
                {
                    var mine = Swap(__instance, value);
                    if (mine != null)
                    {
                        value = mine;
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"1.2 icons: {ex.Message}");
                }
            }
        }

        // Buttons that swap sprites on hover and press set this one.
        [HarmonyPatch(typeof(Image), nameof(Image.overrideSprite), MethodType.Setter)]
        internal static class OverridePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Image __instance, ref Sprite value)
            {
                try
                {
                    var mine = Swap(__instance, value);
                    if (mine != null)
                    {
                        value = mine;
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"1.2 icons: {ex.Message}");
                }
            }
        }

        // 1.2's tab icons: every tab bar's (character, raid inventory, builds, mannequin...) at 24 from the left in the
        // middle, 24 x 24 (the selected one 25 x 24), for the redrawn square icons.
        [HarmonyPatch(typeof(Tab), nameof(Tab.Awake))]
        internal static class TabPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Tab __instance)
            {
                if (!On)
                {
                    return;
                }

                try
                {
                    TabIcon(__instance._normalVersion, 24f);
                    TabIcon(__instance._selectedVersion, 25f);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"1.2 icons, tab: {ex.Message}");
                }
            }
        }

        private static void TabIcon(GameObject version, float width)
        {
            // only the usual tab: the icon at the left edge's middle, about 24 in
            if (version == null || !(version.transform.Find("Icon") is RectTransform icon) || icon.anchorMin != new Vector2(0f, 0.5f)
                || icon.anchorMax != new Vector2(0f, 0.5f) || icon.anchoredPosition.x < 20f || icon.anchoredPosition.x > 28f)
            {
                return;
            }

            icon.anchoredPosition = new Vector2(24f, 0f);
            icon.sizeDelta = new Vector2(width, 24f);
            if (icon.GetComponent<Image>() is Image image)
            {
                image.preserveAspect = true;
            }
        }

        [HarmonyPatch(typeof(MenuTaskBar), nameof(MenuTaskBar.Awake))]
        internal static class TaskBarPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MenuTaskBar __instance)
            {
                if (!On)
                {
                    return;
                }

                try
                {
                    var figure = Ui.FileSprite("icons12/mainmenu_character_icon");
                    foreach (var image in __instance.GetComponentsInChildren<Image>(true))
                    {
                        if (figure != null && image.name == "Icon" && image.transform.parent != null && image.transform.parent.name == "CharacterButton")
                        {
                            image.sprite = figure;
                            image.preserveAspect = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"1.2 icons, task bar: {ex.Message}");
                }
            }
        }

        // The sprites the screens were saved with are never set: they are swapped when the image comes on.
        [HarmonyPatch(typeof(Image), "OnEnable")]
        internal static class EnablePatch
        {
            [HarmonyPostfix]
            private static void Postfix(Image __instance)
            {
                try
                {
                    var sprite = __instance.sprite;
                    if (sprite != null && Swap(__instance, sprite) is Sprite mine)
                    {
                        __instance.sprite = mine;
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"1.2 icons: {ex.Message}");
                }
            }
        }
    }
}
