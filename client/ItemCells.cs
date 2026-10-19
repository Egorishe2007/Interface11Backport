using System;
using System.Collections.Generic;
using System.IO;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's item cells (Prefabs/UGUI/Layouts/grid_layout, against 0.16's): the frame 1 thick (Allscreens_Borders_Default
    // instead of border_generic, same colour), the texts cut at the cell's edges, and 1.2's redrawn square icons: the
    // secured container 24x24, the lock 18x18, the favourites marks, the insurance mark, the mod types 12x12.
    internal static class ItemCells10
    {
        private static readonly HashSet<int> Done = new HashSet<int>();
        private static readonly Dictionary<string, Sprite> ModIcons = new Dictionary<string, Sprite>();

        [HarmonyPatch(typeof(ItemView), nameof(ItemView.NewItemView))]
        internal static class ViewPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemView __instance)
            {
                if (!Interface10Plugin.ItemCells.Value || !Done.Add(__instance.GetInstanceID()))
                {
                    return;
                }

                try
                {
                    Restyle(__instance);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Item cells: {ex}");
                }
            }
        }

        // The mod type icon in the cell's corner: 1.2's picture of the same name, 12x12.
        [HarmonyPatch(typeof(ItemViewStats), nameof(ItemViewStats.SetItemIcon))]
        internal static class ModIconPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemViewStats __instance)
            {
                var icon = __instance._modIcon;
                if (!Interface10Plugin.ItemCells.Value || icon == null || icon.sprite == null)
                {
                    return;
                }

                var mine = ModIcon(icon.sprite.name);
                if (mine == null)
                {
                    return;
                }

                icon.sprite = mine;
                icon.preserveAspect = true;
                icon.rectTransform.sizeDelta = new Vector2(12f, 12f);
            }
        }

        private static void Restyle(ItemView view)
        {
            var border = view._border;
            if (border != null && border.sprite != null && border.sprite.name == "border_generic")
            {
                border.sprite = Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f));
            }

            var info = view.transform.Find("Info Panel");
            if (info == null)
            {
                return;
            }

            if (info.GetComponent<RectMask2D>() == null)
            {
                info.gameObject.AddComponent<RectMask2D>();
            }

            Icon(info, "Secure Icon", "icon_secured_container", new[] { 1f, 0f, 1f, 0f, 2.5f, -1.6f, 24f, 24f, 1f, 0f });
            Icon(info, "Locked Icon", "icon_lock_small", new[] { 1f, 0f, 1f, 0f, 0f, 0f, 18f, 18f, 1f, 0f });
            Icon(info, "TopLayoutGroup/FavoritePanel/Favorite", "AddToFavorites", null, -2f);
            Icon(info, "TopLayoutGroup/FavoritePanel/UnFavorite", "RemoveFromFavorites", null, -2f);
            Icon(info, "BottomLayoutGroup/BottomRightLayoutGroup/InsuranceIcon", "icon_insured_small", null);
        }

        // An icon of the cell that still has 0.16's picture: 1.2's, kept square, placed as in 1.2.
        private static void Icon(Transform info, string path, string sprite, float[] place, float? y = null)
        {
            var image = info.Find(path)?.GetComponent<Image>();
            if (image == null || image.sprite == null || image.sprite.name != sprite)
            {
                return;
            }

            var mine = Ui.FileSprite(sprite);
            if (mine == null)
            {
                return;
            }

            image.sprite = mine;
            image.preserveAspect = true;
            if (place != null)
            {
                Ui.Place(image.rectTransform, place);
            }

            if (y.HasValue)
            {
                image.rectTransform.anchoredPosition = new Vector2(image.rectTransform.anchoredPosition.x, y.Value);
            }
        }

        private static Sprite ModIcon(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("icon_mod_", StringComparison.Ordinal))
            {
                return null;
            }

            if (!ModIcons.TryGetValue(name, out var sprite))
            {
                sprite = File.Exists(Path.Combine(Interface10Plugin.Dir, "ui", "mods", name + ".png")) ? Ui.FileSprite("mods/" + name) : null;
                ModIcons[name] = sprite;
            }

            return sprite;
        }
    }
}
