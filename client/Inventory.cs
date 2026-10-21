using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using EFT.UI.Health;
using EFT.UI.WeaponModding;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's inventory windows (CommonUIScene's InventoryScreen, Scavenger Inventory Screen, Transfer Items Screen,
    // TransferItemsInRaidScreen, EditBuildScreen, Weapon Modding Screen; MenuUIScene's EquipmentBuildsScreen). They
    // share their parts, so the parts are restyled wherever they show:
    // - the weight and health row under the character: wider (220), in the middle, 1.2's white weight icon, the icons
    //   33 x 32 at 20, the figures moved for them;
    // - the condition icons of the health panel in one column;
    // - the stash: filter icons 30 x 30, the pin, lock and sort buttons' icons 26 and 25, the money's icons 20 x 20
    //   (the rouble in the warm grey of its figure) with the figures centred;
    // - the key labels over the quick slots: 1.2's plate that grows with the key's name, cut at the slot's edges;
    // - builds and weapon modding: the caption's icon 42 x 42 left of the title, the sub-caption 7 lower, 1.2's check
    //   boxes (grey frame, big check mark), the characteristics' icons 18 x 18, 1.2's lights on the weapon.
    // The icons themselves are 1.2's redrawn ones (Icons12); what is left over is live only (the bodycam slot, the
    // shop's buttons over the stash).
    internal static class Inventory10
    {
        // ---- weight and health ----

        internal static void HealthBar(Transform panel)
        {
            if (!Once(panel))
            {
                return;
            }

            if (panel.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.childAlignment = TextAnchor.UpperCenter;
            }

            if (panel is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(220f, rect.sizeDelta.y);
            }

            if (panel.Find("Weight") is Transform weight)
            {
                if (weight.Find("Icon") is RectTransform icon)
                {
                    if (icon.GetComponent<Image>() is Image image && Ui.FileSprite("health_icon_weight_white") is Sprite white)
                    {
                        image.sprite = white;
                        image.preserveAspect = true;
                    }

                    Ui.Place(icon, new[] { 0f, 0.5f, 0f, 0.5f, 30f, 0f, 33f, 32f, 0.5f, 0.5f });
                }

                foreach (var name in new[] { "KG", "Max" })
                {
                    if (weight.Find(name) is RectTransform figure)
                    {
                        figure.anchoredPosition = new Vector2(110f, figure.anchoredPosition.y);
                    }
                }

                if (weight.Find("Current") is RectTransform current)
                {
                    current.sizeDelta = new Vector2(80f, current.sizeDelta.y);
                }
            }

            foreach (var name in new[] { "Overall Health", "Hydration", "Energy" })
            {
                var parameter = panel.Find(name);
                if (parameter?.Find("Icon") is RectTransform icon)
                {
                    Ui.Place(icon, new[] { 0f, 0.5f, 0f, 0.5f, 20f, 0f, 33f, 32f, 0.5f, 0.5f });
                }

                if (parameter?.Find("Glow") is RectTransform glow)
                {
                    glow.anchoredPosition += new Vector2(0f, 23.4f);
                }
            }
        }

        // ---- stash ----

        private static readonly string[] Filters =
        {
            "All", "Weapons", "Magazines", "Ammo", "Meds", "Food", "Knifes", "Mods", "Grenades", "Barter", "Gear", "Goggles",
            "Containers", "Vests", "Info", "Keys", "Special", "Raid",
        };

        private static readonly Color RoubleColor = new Color(0.772f, 0.765f, 0.698f, 1f);

        internal static void Stash(SimpleStashPanel stash)
        {
            if (!Once(stash))
            {
                return;
            }

            var root = stash.transform;
            foreach (var name in Filters)
            {
                if (root.Find("Filter Panel/" + name + "/Image") is RectTransform icon)
                {
                    var size = name == "Keys" ? new Vector2(21.7f, 25f) : new Vector2(30f, 30f);
                    Ui.Place(icon, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 1f, 0f, size.x, size.y, 0.5f, 0.5f });
                }
            }

            foreach (var (name, size) in new[] { ("Priority", 26f), ("Pin", 26f), ("Lock", 26f), ("SortButton", 25f) })
            {
                if (root.Find("BottomLeftPanel/" + name + "/Image") is RectTransform icon)
                {
                    Ui.Place(icon, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 1f, 0f, size, size, 0.5f, 0.5f });
                }
            }

            if (root.Find("Mask") is RectTransform mask)
            {
                mask.anchoredPosition = new Vector2(mask.anchoredPosition.x, 0f);
                mask.sizeDelta = new Vector2(mask.sizeDelta.x, 0f);
            }

            foreach (var currency in new[] { "Roubles", "Euro", "Dollars" })
            {
                var money = root.Find("MoneyCount/" + currency);
                if (money?.Find("Image") is Transform image)
                {
                    if (image.GetComponent<LayoutElement>() is LayoutElement element)
                    {
                        element.preferredWidth = 20f;
                        element.preferredHeight = 20f;
                    }

                    if (image.GetComponent<Image>() is Image picture)
                    {
                        picture.preserveAspect = true;
                        if (currency == "Roubles")
                        {
                            picture.color = RoubleColor;
                        }
                    }
                }

                if (money?.Find("Label")?.GetComponent<TMP_Text>() is TMP_Text label)
                {
                    label.horizontalAlignment = HorizontalAlignmentOptions.Center;
                }
            }
        }

        // ---- quick slots ----

        // 1.2's key label: bind_label cut into three (10 at each side), as wide as the key's name with 5 and 8 around it,
        // at least 25; under a mask as wide as the slot and 25 high, so a long name stops at the slot's edge.
        internal static void BindLabels(Transform panel)
        {
            if (!Once(panel))
            {
                return;
            }

            var inRaid = panel.GetComponentInParent<EftBattleUIScreen>(true) != null;
            foreach (var label in panel.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "Bind Panel").ToList())
            {
                var slot = label.parent;
                if (slot == null || slot.name == "Bind PanelRoot")
                {
                    continue;
                }

                // 1.2's places: the inventory's slots have the plate at the cell's edge; the raid's bar has it 4 in on a
                // weapon cell and a 15 high one on the small cell beside it (EFTBattleUIScreen's QuickAccessPanel)
                var small = inRaid && slot.name == "SubWeapon";
                var root = Ui.Rect(slot, "Bind PanelRoot");
                root.SetSiblingIndex(label.GetSiblingIndex());
                Ui.Place(root, small ? new[] { 0f, 1f, 1f, 1f, 12.68f, 0f, -25.35f, 15f, 0.5f, 1f } : new[] { 0f, 1f, 1f, 1f, 0f, 0f, 0f, 25f, 0.5f, 1f });
                root.gameObject.AddComponent<RectMask2D>();
                label.SetParent(root, false);
                Ui.Place(label, small ? new[] { 0f, 1f, 0f, 1f, 3.85f, -0.11f, 15.84f, 15f, 0f, 1f }
                    : new[] { 0f, 1f, 0f, 1f, inRaid ? 4f : -1f, 1f, 25f, 25f, 0f, 1f });

                if (label.GetComponent<Image>() is Image plate && Ui.FileSprite("bind_label", new Vector4(10f, 0f, 10f, 0f)) is Sprite sprite)
                {
                    plate.sprite = sprite;
                    plate.type = Image.Type.Sliced;
                }

                // the row sizes the key's name itself: from the name's own width, so a long one (a key re-bound to
                // Mouse4, Num 4) widens the plate at once (a fitter on the name ran after the row and left it 25 wide)
                var row = label.GetComponent<HorizontalLayoutGroup>() ?? label.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(5, 8, 0, 0);
                row.spacing = 0f;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = true;
                row.childControlHeight = false;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                var element = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 25f;
                var fit = label.GetComponent<ContentSizeFitter>() ?? label.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                if (label.Find("BindText") is RectTransform text)
                {
                    Ui.Place(text, small ? new[] { 0f, 1f, 0f, 1f, 7.92f, -7.5f, text.sizeDelta.x, 22f, 0.5f, 0.5f }
                        : new[] { 0f, 1f, 0f, 1f, 11f, -12.5f, text.sizeDelta.x, 22f, 0.5f, 0.5f });
                    if (text.GetComponent<TMP_Text>() is TMP_Text name)
                    {
                        // one line: at exactly its width "Num 4" must not wrap
                        name.enableWordWrapping = false;
                    }

                    LayoutRebuilder.MarkLayoutForRebuild(label);
                }
            }
        }

        // ---- builds and weapon modding ----

        private static readonly Color CaptionIcon = new Color(0.933f, 0.929f, 0.867f, 1f);

        internal static void BuildScreen(Transform root, string captionIcon)
        {
            if (!Once(root))
            {
                return;
            }

            if (root.Find("Caption/" + captionIcon) is RectTransform icon)
            {
                var edit = captionIcon == "Icon";
                Ui.Place(icon, new[] { 0f, 0.5f, 0f, 0.5f, edit ? -3f : -4.9f, 0f, 42f, 42f, 1f, 0.5f });
                if (edit && icon.GetComponent<Image>() is Image image)
                {
                    image.color = CaptionIcon;
                }

                icon.GetComponent<Image>().preserveAspect = true;
            }

            if (root.Find("Sub-caption") is RectTransform sub)
            {
                sub.anchoredPosition = new Vector2(sub.anchoredPosition.x, -72f);
            }

            var context = root.Find("ButtonsPanel/ContextButtons");
            if (context?.Find("SelectItemButton/IconPlaceholder/Icon") is RectTransform select && select.GetComponent<Image>() is Image weapon
                && Ui.FileSprite("selecti_weapon") is Sprite selectWeapon)
            {
                weapon.sprite = selectWeapon;
                weapon.preserveAspect = true;
                select.sizeDelta = new Vector2(20f, 20f);
            }

            if (context?.Find("DeleteBuildButton/IconPlaceholder/Icon") is RectTransform delete)
            {
                delete.sizeDelta = new Vector2(18f, 18f);
            }

            if (root.Find("ButtonsPanel/BottomButtons/Assemble/Button/Icon") is RectTransform assemble)
            {
                assemble.anchoredPosition = new Vector2(assemble.anchoredPosition.x, 0f);
            }

            foreach (var name in new[] { "CharTemplate", "Mastering", "Weight" })
            {
                if (root.Find("CharacteristicsPanel/CharacteristicsExpanded/" + name + "/Icon") is RectTransform characteristic)
                {
                    characteristic.sizeDelta = new Vector2(18f, 18f);
                }
            }

            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                CheckBox(toggle.transform);
            }

            if (root.Find("All Classes Toggle/Checkmark") is RectTransform all)
            {
                all.sizeDelta = new Vector2(16f, 16f);
            }

            Lights(root.Find("Preview Panel/Lights"));
        }

        // 1.2's check box: the grey frame of all its screens and the big check mark, 14 x 14 in the middle.
        private static void CheckBox(Transform toggle)
        {
            var box = toggle.Find("Checkmark");
            if (box?.Find("Border")?.GetComponent<Image>() is Image border && box.Find("Mark") is RectTransform mark
                && mark.GetComponent<Image>() is Image check)
            {
                border.sprite = Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f));
                border.type = Image.Type.Sliced;
                border.fillCenter = false;
                check.sprite = Ui.FileSprite("checkmark_big");
                check.preserveAspect = true;
                Ui.Place(mark, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 14f, 14f, 0.5f, 0.5f });
            }
        }

        // 1.2's lights on the item in the builds and modding screens: six directional lights (the main one with soft
        // shadows) and three points (cold below right, warm above left, a rim below) instead of 0.16's thirteen.
        private static readonly (string Name, LightType Type, Color Color, float Intensity, float Range, Vector3 Position, Quaternion Rotation)[] Rig =
        {
            ("Directional_main", LightType.Directional, new Color(1f, 1f, 1f), 0.8f, 1.78f, new Vector3(0.07f, 0.1252f, -0.128f), new Quaternion(0.5184f, -0.1089f, -0.0537f, 0.8465f)),
            ("Directional_fill", LightType.Directional, new Color(1f, 0.9887f, 0.95f), 0.55f, 0.5f, new Vector3(0.04f, -0.081f, -0.189f), new Quaternion(0.0981f, 0.2076f, 0.0146f, 0.9732f)),
            ("Directional_Right", LightType.Directional, new Color(0.85f, 0.946f, 1f), 0.7f, 1f, new Vector3(0.242f, 0.246f, 0.148f), new Quaternion(0.068f, -0.7349f, 0.1432f, 0.6594f)),
            ("Directional_Left", LightType.Directional, new Color(1f, 1f, 1f), 0.8f, 0.5f, new Vector3(-0.187f, -0.105f, -0.108f), new Quaternion(0.0902f, 0.5822f, -0.0592f, 0.8059f)),
            ("Directional_Top_Left_rim", LightType.Directional, new Color(1f, 1f, 1f), 0.5f, 0.5f, new Vector3(-0.065f, 0.202f, 0.113f), new Quaternion(-0.9436f, 0.0196f, -0.1534f, -0.2927f)),
            ("Directional_Bottom_rim", LightType.Directional, new Color(0.9922f, 0.9922f, 0.9922f), 0.6f, 1.78f, new Vector3(0.076f, -0.135f, 0.168f), new Quaternion(0.9483f, -0.0293f, -0.052f, -0.3118f)),
            ("Point_Cold_Bottom_Right", LightType.Point, new Color(0.2706f, 0.458f, 1f), 1.62f, 1.8f, new Vector3(0.175f, -0.221f, -0.027f), Quaternion.identity),
            ("Point_Warm_Top_Left", LightType.Point, new Color(1f, 0.8639f, 0.583f), 2.5f, 2.1234f, new Vector3(-0.418f, 0.123f, 0.366f), Quaternion.identity),
            ("Point_Bottom_rim", LightType.Point, new Color(1f, 0.992f, 0.98f), 5f, 1.5f, new Vector3(-0.048f, -0.437f, 0.183f), Quaternion.identity),
        };

        private static void Lights(Transform lights)
        {
            if (lights == null || lights.Find(Rig[0].Name) != null)
            {
                return;
            }

            var old = lights.GetComponentsInChildren<Light>(true).ToList();
            var mask = old.Count > 0 ? old[0].cullingMask : 1 << 19;
            foreach (var light in old)
            {
                light.gameObject.SetActive(false);
            }

            foreach (var (name, type, color, intensity, range, position, rotation) in Rig)
            {
                var go = new GameObject(name);
                go.layer = lights.gameObject.layer;
                go.transform.SetParent(lights, false);
                go.transform.localPosition = position;
                go.transform.localRotation = rotation;
                var light = go.AddComponent<Light>();
                light.type = type;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.cullingMask = mask;
                light.renderMode = LightRenderMode.Auto;
                if (name == "Directional_main")
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 1f;
                    light.shadowBias = 0f;
                    light.shadowNormalBias = 0f;
                    light.shadowNearPlane = 0.02f;
                }
                else
                {
                    light.shadows = LightShadows.None;
                }
            }
        }

        // ---- hideout transfers ----

        // The window that hands items over in the hideout (the cultists' circle, an area's transfer): 1.2's caption,
        // 20 high and dark grey, the search icon 16 and the close button 27 x 17 in its row.
        internal static void Handover(Transform window)
        {
            if (!Once(window) || !(window.Find("Window/Caption Panel") is RectTransform caption))
            {
                return;
            }

            if (caption.GetComponent<Image>() is Image bar)
            {
                bar.color = new Color(0.098f, 0.106f, 0.106f, 1f);
            }

            if (caption.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.padding = new RectOffset(4, 1, 0, 0);
            }

            (caption.GetComponent<LayoutElement>() ?? caption.gameObject.AddComponent<LayoutElement>()).preferredHeight = 20f;
            if (caption.Find("Caption")?.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup title)
            {
                title.spacing = 4f;
            }

            if (caption.Find("Caption/Image")?.GetComponent<LayoutElement>() is LayoutElement search)
            {
                search.preferredWidth = search.preferredHeight = 16f;
            }

            if (caption.Find("Close Button") is Transform close)
            {
                var size = close.GetComponent<LayoutElement>() ?? close.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 27f;
                size.preferredHeight = 17f;
            }
        }

        // ---- helpers ----

        private static bool Once(Component target)
        {
            if (target == null || target.GetComponent<Restyled>() != null)
            {
                return false;
            }

            target.gameObject.AddComponent<Restyled>();
            return true;
        }

        private static void Run(string what, Action action)
        {
            if (!Interface10Plugin.InventoryWindows.Value)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogError($"{what}: {ex}");
            }
        }

        private static IEnumerable<MethodBase> Shows(Type type, params int[] counts) => AccessTools.GetDeclaredMethods(type)
            .Where(m => m.Name == "Show" && counts.Contains(m.GetParameters().Length));

        // ---- patches ----

        [HarmonyPatch]
        internal static class HealthBarPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(HealthParametersPanel), 3);

            [HarmonyPostfix]
            private static void Postfix(HealthParametersPanel __instance) => Run("Weight and health row", () => HealthBar(__instance.transform));
        }

        [HarmonyPatch]
        internal static class HealthPanelPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(InventoryScreenHealthPanel), 4);

            [HarmonyPostfix]
            private static void Postfix(InventoryScreenHealthPanel __instance) =>
                Run("Health panel", () => RaidEnd10.ConditionIcons(__instance.transform.Find("Parameters")));
        }

        [HarmonyPatch]
        internal static class StashPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(SimpleStashPanel), 8);

            [HarmonyPostfix]
            private static void Postfix(SimpleStashPanel __instance) => Run("Stash", () => Stash(__instance));
        }

        [HarmonyPatch]
        internal static class QuickSlotsPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(InventoryScreenQuickAccessPanel), 2, 4);

            [HarmonyPostfix]
            private static void Postfix(InventoryScreenQuickAccessPanel __instance) => Run("Quick slots", () => BindLabels(__instance.transform));
        }

        [HarmonyPatch]
        internal static class HandoverPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(HandoverHideoutAreaStashItemsWindow), 3);

            [HarmonyPostfix]
            private static void Postfix(HandoverHideoutAreaStashItemsWindow __instance) => Run("Hideout handover window", () => Handover(__instance.transform));
        }

        [HarmonyPatch]
        internal static class EditBuildPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(EditBuildScreen), 1);

            [HarmonyPostfix]
            private static void Postfix(EditBuildScreen __instance) => Run("Edit build screen", () => BuildScreen(__instance.transform, "Icon"));
        }

        [HarmonyPatch]
        internal static class ModdingPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => Shows(typeof(WeaponModdingScreen), 1);

            [HarmonyPostfix]
            private static void Postfix(WeaponModdingScreen __instance) => Run("Weapon modding screen", () => BuildScreen(__instance.transform, "Image"));
        }
    }
}
