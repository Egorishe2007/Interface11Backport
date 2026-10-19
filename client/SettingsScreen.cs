using System;
using System.Linq;
using EFT.UI;
using EFT.UI.Settings;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.0's settings (1.2 still has them so): one column of rows on an 850-wide dark panel, each row a grey label on the
    // left and its control 250 wide at the right end: drop-downs, sliders with the value in a box after them, check boxes
    // in a dark box. 0.16 had labels right-aligned before controls in the middle, two columns on the Graphics tab, check
    // boxes in a grid and the nickname's bar in red. The game's own rows are kept and only laid out again, so they work as
    // before. Sizes and colours are those of 1.0's row prefabs ("Settings Drop Down", "Settings Float Slider",
    // "SettingsToggleRight") and of its tabs in CommonUIScene. The Controls tab is the same in 1.0 and stays as it is.
    internal static class Settings10
    {
        private const float Control = 250f;
        private const float Gap = 10f;
        private const float RowHeight = 35f;
        private const float SliderHeight = 40f;
        private static readonly Color LabelColor = new Color(0.4863f, 0.4784f, 0.4353f, 1f);
        private static readonly Color ValueColor = new Color(0.7412f, 0.7294f, 0.6784f, 1f);
        private static readonly Color NumberColor = new Color(0.7647f, 0.7725f, 0.698f, 1f);
        private static readonly Color BoxColor = new Color(0.1059f, 0.1176f, 0.1216f, 0.502f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.8196f);
        private static readonly Color ListColor = new Color(0f, 0f, 0f, 0.702f);
        private static readonly Color WarningColor = new Color(0.7412f, 0.7294f, 0.6784f, 0.4f);
        private static readonly Color ClearBarColor = new Color(0.0902f, 0.098f, 0.098f, 1f);

        // Graphics: the check boxes come last, in this order (0.16 had them in two rows under the list).
        private static readonly string[] GraphicsToggles =
        {
            "High Quality Color", "Z-Blur", "Chromatic Aberrations", "Noise", "VolumetricLight", "GrassShadow", "MipStreaming",
            "SdTarkovStreets", "High Quality Fog", "Bloom"
        };

        [HarmonyPatch(typeof(GameSettingsTab), nameof(GameSettingsTab.Show))]
        internal static class GamePatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameSettingsTab __instance) => Attach(__instance);
        }

        [HarmonyPatch(typeof(GraphicsSettingsTab), nameof(GraphicsSettingsTab.Show))]
        internal static class GraphicsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GraphicsSettingsTab __instance) => Attach(__instance);
        }

        [HarmonyPatch(typeof(PostFXSettingsTab), nameof(PostFXSettingsTab.Show))]
        internal static class PostFxPatch
        {
            [HarmonyPostfix]
            private static void Postfix(PostFXSettingsTab __instance) => Attach(__instance);
        }

        [HarmonyPatch(typeof(SoundSettingsTab), nameof(SoundSettingsTab.Show))]
        internal static class SoundPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SoundSettingsTab __instance) => Attach(__instance);
        }

        private static void Attach(SettingsTab tab)
        {
            if (!Interface10Plugin.Settings.Value)
            {
                return;
            }

            try
            {
                var look = tab.GetComponent<SettingsLook10>() ?? tab.gameObject.AddComponent<SettingsLook10>();
                look.Apply();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"1.0 settings ({tab.name}): {ex}");
            }
        }

        // The tab's panel, once.
        internal static void Container(SettingsTab tab)
        {
            TabNames(tab);
            switch (tab)
            {
                case GameSettingsTab _:
                    Game(tab.transform);
                    break;
                case GraphicsSettingsTab _:
                    Graphics(tab.transform);
                    break;
                case PostFXSettingsTab _:
                    PostFx(tab.transform);
                    break;
                case SoundSettingsTab _:
                    Sound(tab.transform);
                    break;
            }
        }

        // 1.2's tooltips over the rows, and the rows 1.2 dropped kept hidden.
        internal static void Extras(SettingsTab tab)
        {
            if (tab == null)
            {
                return;
            }

            SettingsTips10.Attach(tab.transform);
            if (tab is GameSettingsTab && tab.transform.Find("Image/Scroll View/Viewport/Other Settings") is Transform content)
            {
                Hidden(content);
            }
        }

        // Rows not laid out yet (the tabs make some of theirs when shown, the sound tab its VoIP ones later).
        internal static void Rows(Transform tab)
        {
            foreach (var box in tab.GetComponentsInChildren<DropDownBox>(true))
            {
                if (New(box))
                {
                    Try(box, () => DropDownRow(box));
                }
            }

            foreach (var toggle in tab.GetComponentsInChildren<UpdatableToggle>(true))
            {
                // PostFX's on/off stays a check box with its name in the middle over the panel, as in 1.0
                if (New(toggle) && toggle.name != "Always Show Interface")
                {
                    Try(toggle, () => ToggleRow(toggle));
                }
            }

            foreach (var slider in tab.GetComponentsInChildren<NumberSlider>(true))
            {
                if (New(slider))
                {
                    Try(slider, () => SliderRow(slider, slider.transform.Find("Value Input"), true));
                }
            }

            foreach (var slider in tab.GetComponentsInChildren<SelectSlider>(true))
            {
                if (New(slider))
                {
                    Try(slider, () => SliderRow(slider, slider.transform.Find("Value Text"), false));
                }
            }
        }

        // Sizes known only once laid out: the Game tab's check boxes are a grid of one column, its cells as wide as the
        // list; a row's own button (the voice's CHANGE) gets its width when its text comes.
        internal static void Fit(Transform tab)
        {
            foreach (var grid in tab.GetComponentsInChildren<GridLayoutGroup>())
            {
                var width = grid.transform.parent is RectTransform list ? list.rect.width : 0f;
                if (grid.constraintCount == 1 && width > 1f && Mathf.Abs(grid.cellSize.x - width) > 0.5f)
                {
                    grid.cellSize = new Vector2(width, grid.cellSize.y);
                }
            }

            foreach (var row in tab.GetComponentsInChildren<ButtonRow10>())
            {
                Space(row);
            }
        }

        private static void Space(ButtonRow10 row)
        {
            var width = row.Button.rect.width;
            if (Mathf.Abs(width - row.Width) < 0.5f)
            {
                return;
            }

            row.Width = width;
            Right(row.Control, width + Gap, Control, RowHeight, 0f);
            row.Label.rectTransform.offsetMax = new Vector2(-(width + Gap + Control + Gap), 0f);
        }

        // Rows the tab adds later go before the Graphics check boxes, which stay last.
        internal static void Order(SettingsTab tab)
        {
            if (!(tab is GraphicsSettingsTab))
            {
                return;
            }

            var content = tab.transform.Find("Other Settings/SettingsList/Viewport/Content");
            if (content == null)
            {
                return;
            }

            var toggles = GraphicsToggles.Select(n => content.Find(n)).Where(t => t != null).ToList();
            var last = content.childCount - toggles.Count;
            if (toggles.Where((t, i) => t.GetSiblingIndex() != last + i).Any())
            {
                foreach (var toggle in toggles)
                {
                    toggle.SetAsLastSibling();
                }
            }
        }

        // The tab names a little higher: 1.0's row of them is 46 high at 79 from the top, its middle at 102.
        private static void TabNames(SettingsTab tab)
        {
            var screen = tab.GetComponentInParent<SettingsScreen>();
            var names = screen != null ? screen.transform.Find("Toggles") as RectTransform : null;
            if (names != null && names.GetComponent<Row10>() == null)
            {
                names.gameObject.AddComponent<Row10>();
                names.anchoredPosition = new Vector2(names.anchoredPosition.x, -102f + names.rect.height / 2f);
            }
        }

        // Game: 1.0's Container (850 wide, from 60 under the tab's top to 60 over the screen's bottom; 0.16's tab ends 75
        // over it) with the nickname at the top, its bar grey over the panel, and the list under it. 0.16 draws no ground
        // under the list, 1.0 does.
        private static void Game(Transform tab)
        {
            var container = tab.Find("Image") as RectTransform;
            if (container == null)
            {
                return;
            }

            container.anchorMin = new Vector2(0.5f, 0f);
            container.anchorMax = new Vector2(0.5f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.offsetMin = new Vector2(-425f, -15f);
            container.offsetMax = new Vector2(425f, -60f);
            Paint(container, PanelColor, true);

            var nickname = container.Find("Nickname Panel") as RectTransform;
            if (nickname != null)
            {
                Paint(nickname, ListColor, true);
                var warning = nickname.Find("Warning Panel") as RectTransform;
                if (warning != null)
                {
                    Ui.Place(warning, new[] { 0f, 1f, 1f, 1f, 0f, 38f, 0f, 38f, 0.5f, 1f });
                    Paint(warning, WarningColor, false);
                    var text = warning.Find("Text")?.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        Ui.Place(text.rectTransform, new[] { 0f, 0f, 1f, 1f, 0f, 0f, -44f, 0f, 0.5f, 0.5f });
                        text.fontSize = 15f;
                        text.color = Color.black;
                    }
                }

                if (nickname.Find("InputField") is RectTransform input)
                {
                    Ui.Place(input, new[] { 0.5f, 0.5f, 0.5f, 0.5f, -127f, -4f, 500f, 50f, 0.5f, 0.5f });
                    if (input.Find("ChangeNickname") is RectTransform change)
                    {
                        Ui.Place(change, new[] { 1f, 0.5f, 1f, 0.5f, 25f, 0f, 236f, 43f, 0f, 0.5f });
                    }
                }

                if (nickname.Find("UsedSymbolsCaption") is RectTransform symbols)
                {
                    // under the name, from the panel's left edge like the rows' labels
                    Ui.Place(symbols, new[] { 0f, 0f, 1f, 0f, -329f, -1f, -377f, 36f, 1f, 0f });
                    var text = symbols.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        text.alignment = TextAlignmentOptions.Left;
                    }
                }
            }

            var scroll = container.Find("Scroll View") as RectTransform;
            if (scroll == null)
            {
                return;
            }

            Ui.Place(scroll, new[] { 0f, 0f, 1f, 1f, 0f, -145f, 0f, -196f, 0f, 1f });
            Paint(scroll, ListColor, true);
            if (scroll.Find("Viewport") is RectTransform viewport)
            {
                Ui.Place(viewport, new[] { 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 1f });
            }

            if (scroll.Find("Scrollbar") is RectTransform bar)
            {
                Ui.Place(bar, new[] { 1f, 0f, 1f, 1f, -10f, 0f, 10f, -20f, 1f, 0.5f });
            }

            var content = scroll.Find("Viewport/Other Settings") as RectTransform;
            if (content == null)
            {
                return;
            }

            // 1.0's "Settings": rows 752 wide, 49 in from the panel's left edge (the viewport leaves the scroll bar 7 on
            // the right), 10 apart
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(49f, content.offsetMin.y);
            content.offsetMax = new Vector2(-42f, content.offsetMax.y);
            Paint(content, Color.clear, true);
            List(content.GetComponent<VerticalLayoutGroup>(), 0, 10);
            foreach (Transform group in content)
            {
                Fitter(group, false);
                if (group.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list)
                {
                    List(list, 0, 0);
                }
                else if (group.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid)
                {
                    // one check box a row
                    grid.padding = new RectOffset(0, 0, 0, 0);
                    grid.cellSize = new Vector2(850f - 98f, RowHeight);
                    grid.spacing = new Vector2(0f, Gap);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 1;
                }

                foreach (Transform child in group)
                {
                    if (child.name == "Separator")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            // CLEAR WISHLIST across the list, on 1.2's dark bar at rest (its light one on hover is 0.16's own)
            if (content.Find("Advanced/ClearWishlist") is RectTransform clear)
            {
                Fitter(clear, false);
                Height(clear, RowHeight);
                if (clear.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
                {
                    row.childAlignment = TextAnchor.MiddleCenter;
                }

                if (clear.Find("Backgrounds/NormalBack")?.GetComponent<Image>() is Image rest)
                {
                    rest.sprite = null;
                    rest.color = ClearBarColor;
                    var hover = clear.Find("Backgrounds/HoverBack") as RectTransform;
                    Ui.Stretch(rest.rectTransform);
                    if (hover != null)
                    {
                        rest.rectTransform.sizeDelta = hover.sizeDelta;
                        rest.rectTransform.anchoredPosition = hover.anchoredPosition;
                    }
                }
            }

            // 1.2 has no aiming dead zone and no tutorial hints any more; 0.16 shows both greyed out
            Hidden(content);

            // 1.0's order: the sliders (head bobbing, then field of view) before the check boxes
            var scrolls = content.Find("Scrolls");
            var toggles = content.Find("TogglesOneColumn");
            if (scrolls != null && toggles != null && scrolls.GetSiblingIndex() > toggles.GetSiblingIndex())
            {
                scrolls.SetSiblingIndex(toggles.GetSiblingIndex());
            }

            var fov = scrolls?.Find("FOV");
            var headbob = scrolls?.Find("Headbobbing");
            if (fov != null && headbob != null && headbob.GetSiblingIndex() > fov.GetSiblingIndex())
            {
                headbob.SetSiblingIndex(fov.GetSiblingIndex());
            }
        }

        // The game settings 1.2 dropped, hidden again whenever the game shows them.
        internal static void Hidden(Transform content)
        {
            foreach (var path in new[] { "Scrolls/Aiming Deadzone (Bloker)", "TogglesOneColumn/InactiveOptionsBlocker" })
            {
                if (content.Find(path) is Transform row && row.gameObject.activeSelf)
                {
                    row.gameObject.SetActive(false);
                }
            }
        }

        // Graphics: one list, as 1.0 has it, on an 850-wide panel from 165 under the screen's top to 60 over its bottom;
        // the screen settings (resolution etc.) and the overall quality at its top, the check boxes at its end.
        private static void Graphics(Transform tab)
        {
            var root = (RectTransform)tab;
            var other = root.Find("Other Settings") as RectTransform;
            var list = other != null ? other.Find("SettingsList") as RectTransform : null;
            var content = list != null ? list.Find("Viewport/Content") as RectTransform : null;
            if (content == null)
            {
                return;
            }

            Enable<VerticalLayoutGroup>(root, false);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 60f);
            root.sizeDelta = new Vector2(850f, -225f);

            Enable<VerticalLayoutGroup>(other, false);
            Ui.Stretch(other);
            Paint(other, PanelColor, true);
            Ui.Stretch(list);
            if (list.Find("Viewport") is RectTransform viewport)
            {
                Ui.Place(viewport, new[] { 0.5f, 0f, 0.5f, 1f, 0f, 0f, 750f, -40f, 0.5f, 0.5f });
            }

            if (list.Find("Scrollbar") is RectTransform bar)
            {
                Ui.Place(bar, new[] { 1f, 0f, 1f, 1f, -10f, 0f, 10f, -40f, 1f, 0.5f });
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
            List(content.GetComponent<VerticalLayoutGroup>(), 0, 0);

            var index = 0;
            var screen = root.Find("Screen Settings");
            foreach (var path in new[] { "First Row/Resolution", "SecondRow/Aspect Ratio", "First Row/FullscreenMode", "SecondRow/Displays", "Toggles/VSync", "Toggles/Multi-monitor support" })
            {
                var row = screen != null ? screen.Find(path) : null;
                if (row != null)
                {
                    row.SetParent(content, false);
                    row.SetSiblingIndex(index++);
                }
            }

            var quality = root.Find("Overall Quality Panel");
            var overall = quality != null ? quality.Find("Overall Quality") : null;
            if (overall != null)
            {
                overall.SetParent(content, false);
                overall.SetSiblingIndex(index);
            }

            foreach (var row in new[] { "TogglesRow1", "TogglesRow2" }.Select(other.Find).Where(r => r != null).ToList())
            {
                foreach (var toggle in row.Cast<Transform>().ToList())
                {
                    toggle.SetParent(content, false);
                }

                row.gameObject.SetActive(false);
            }

            other.Find("Separator")?.gameObject.SetActive(false);
            screen?.gameObject.SetActive(false);
            quality?.gameObject.SetActive(false);
        }

        // PostFX: 1.0's Panel, 850 wide and 785 high from 165 under the top; the on/off at its top, the rows under it on
        // one dark ground.
        private static void PostFx(Transform tab)
        {
            Ui.Place((RectTransform)tab, new[] { 0.5f, 1f, 0.5f, 1f, 0f, -165f, 930f, 785f, 0.5f, 1f });
            var panel = tab.Find("Panel") as RectTransform;
            if (panel == null)
            {
                return;
            }

            Fitter(panel, false);
            Ui.Place(panel, new[] { 0.5f, 1f, 0.5f, 1f, 0f, 0f, 850f, 785f, 0.5f, 1f });
            if (panel.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup layout)
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 0f;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }

            Transform lastSection = null;
            foreach (Transform section in panel)
            {
                Paint(section, PanelColor, true);
                if (section.name == "EnablePanel" || !(section.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list))
                {
                    continue;
                }

                List(list, 50, 10);
                lastSection = section;
            }

            // the last part takes the rest of the panel down to its bottom
            if (lastSection != null)
            {
                var element = lastSection.GetComponent<LayoutElement>() ?? lastSection.gameObject.AddComponent<LayoutElement>();
                element.flexibleHeight = 1f;
            }

            // the raid's PostFX screen (VISUALIZE) has it at the top left, as 1.2
            if (Interface10Plugin.RaidHud.Value)
            {
                InRaid10.PostFxPreview(tab);
            }
        }

        // Sound: 1.0's Panel, 850 wide and 785 high from 165 under the top, the rows 50 in from its sides.
        private static void Sound(Transform tab)
        {
            var root = (RectTransform)tab;
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, -82.5f);
            root.sizeDelta = new Vector2(root.sizeDelta.x, -165f);
            var panel = tab.Find("Panel") as RectTransform;
            if (panel == null)
            {
                return;
            }

            Fitter(panel, false);
            Ui.Place(panel, new[] { 0.5f, 1f, 0.5f, 1f, 0f, 0f, 850f, 785f, 0.5f, 1f });
            Paint(panel, PanelColor, true);
            // the voice is chosen on the character screen's CHARACTER tab, as in 1.2
            if (Interface10Plugin.CharacterTab.Value)
            {
                panel.Find("SlidersSection/ChangeVoice")?.gameObject.SetActive(false);
            }
            if (panel.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup layout)
            {
                List(layout, 50, 10);
                layout.padding.top = 20;
            }

            // the sections and the groups in them (VoIP's are in blockers), not the rows' own lists
            foreach (var list in panel.GetComponentsInChildren<VerticalLayoutGroup>(true).Where(l => l.transform != panel))
            {
                if (list.GetComponent<SettingControl>() != null || Parent<SettingControl>(list.transform) != null || Parent<DropDownBox>(list.transform) != null)
                {
                    continue;
                }

                if (list.transform.parent == panel)
                {
                    Fitter(list.transform, false);
                }

                if (list.name == "Group" && list.transform.parent.GetComponent<LayoutGroup>() == null)
                {
                    var rect = (RectTransform)list.transform;
                    rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
                    rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
                    rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
                }

                List(list, 0, 0);
            }
        }

        private static void DropDownRow(DropDownBox box)
        {
            var control = (RectTransform)box.transform;
            var setting = Parent<SettingControl>(control);
            var root = setting != null ? (RectTransform)setting.transform : control.parent as RectTransform;
            var label = root != null ? Label(root, control, setting) : null;
            if (label == null)
            {
                return;
            }

            var item = Item(root);
            Unlayout(root);

            Reparent(control, root);
            Right(control, 0f, Control, RowHeight, 0f);
            var value = control.Find("Text")?.GetComponent<TMP_Text>();
            if (value != null)
            {
                value.enableAutoSizing = false;
                value.fontSize = 15f;
                value.color = ValueColor;
            }

            Reparent(label.transform, root);
            Caption(label, Control + Gap, false);
            Height(item, RowHeight);

            // a button of the row's own (the voice's CHANGE): at the right end, the drop-down before it
            var button = root.GetComponentsInChildren<DefaultUIButton>(true).FirstOrDefault(b => b.transform.parent == root);
            if (button != null)
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                var row = root.gameObject.AddComponent<ButtonRow10>();
                row.Button = rect;
                row.Control = control;
                row.Label = label;
                Space(row);
            }
        }

        // A check box: its name on the left, the box in a dark field 250 wide at the right end.
        private static void ToggleRow(UpdatableToggle toggle)
        {
            var root = (RectTransform)toggle.transform;
            var check = root.Find("Background") as RectTransform
                        ?? (toggle.graphic != null ? toggle.graphic.rectTransform.parent as RectTransform : null);
            if (check == null)
            {
                return;
            }

            var setting = Parent<SettingControl>(root) ?? root.GetComponent<SettingControl>();
            var label = Label(root, check, null) ?? (setting != null ? Label(setting.transform, check, setting) : null);
            if (label == null)
            {
                return;
            }

            var item = Item(setting != null && setting.transform != root ? (RectTransform)setting.transform : root);
            if (item != root)
            {
                for (var t = root; t != item; t = (RectTransform)t.parent)
                {
                    Ui.Stretch(t);
                }
            }

            Unlayout(root);
            var field = Ui.Rect(root, "Field10");
            field.SetSiblingIndex(0);
            Right(field, 0f, Control, RowHeight, 0f);
            field.gameObject.AddComponent<Image>().color = BoxColor;

            Reparent(check, root);
            check.anchorMin = check.anchorMax = new Vector2(1f, 0.5f);
            check.pivot = new Vector2(0.5f, 0.5f);
            check.anchoredPosition = new Vector2(-Control / 2f, 0f);
            check.sizeDelta = new Vector2(22f, 22f);

            Reparent(label.transform, root);
            Caption(label, Control + Gap, true);
            Height(item, RowHeight);
        }

        // A slider: 1.0's number slider is 202 wide with its value in a 40-wide box 7.5 after it, together 250 at the
        // right end. A slider of named steps (quality: "medium") has a wider value, so a shorter slider.
        private static void SliderRow(Component slider, Transform value, bool number)
        {
            var control = (RectTransform)slider.transform;
            var setting = Parent<SettingControl>(control);
            var root = setting != null ? (RectTransform)setting.transform : control.parent as RectTransform;
            var label = root != null ? Label(root, control, setting) : null;
            if (label == null)
            {
                return;
            }

            var item = Item(root);
            Unlayout(root);

            var valueWidth = number ? 40f : 74f;
            Reparent(control, root);
            Right(control, valueWidth + 8f, Control - valueWidth - 8f, 20f, 3f);
            if (value is RectTransform box)
            {
                Fitter(box, false);
                box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f);
                box.pivot = new Vector2(0f, 0.5f);
                box.anchoredPosition = new Vector2(8f, -3f);
                box.sizeDelta = new Vector2(valueWidth, number ? 31f : 27f);
                foreach (var text in box.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.enableAutoSizing = false;
                    text.fontSize = 15f;
                    text.color = NumberColor;
                    if (!number)
                    {
                        text.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                }
            }

            Reparent(label.transform, root);
            Caption(label, Control + Gap, true);
            Height(item, SliderHeight);
        }

        // The row's name: the setting's own text, else a localized text of the row outside its control.
        private static TMP_Text Label(Transform root, Transform control, SettingControl setting)
        {
            if (setting != null && setting.Text != null && setting.Text.GetComponent<TMP_Text>() is TMP_Text own && !own.transform.IsChildOf(control))
            {
                return own;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true).Where(t => !t.transform.IsChildOf(control)).ToList();
            return texts.FirstOrDefault(t => t.GetComponent<LocalizedText>() != null) ?? texts.FirstOrDefault();
        }

        // 1.0's label: grey, 18, from the row's left edge to before its control; a drop-down's on one line, a check box's
        // or slider's over two when it is long.
        private static void Caption(TMP_Text text, float right, bool wrap)
        {
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-right, 0f);
            text.enableAutoSizing = false;
            text.fontSize = 18f;
            text.color = LabelColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = wrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        }

        // What the list lays out for this row: the row, or the wrapper it is in (a blocker with a tooltip, an HLG pair).
        // Rows in between are stretched over it.
        private static RectTransform Item(RectTransform root)
        {
            var item = root;
            for (var i = 0; i < 4; i++)
            {
                var parent = item.parent as RectTransform;
                if (parent == null || parent.GetComponent<SettingsTab>() != null)
                {
                    break;
                }

                var group = parent.GetComponent<LayoutGroup>();
                if (group != null && group.enabled && !(group is HorizontalLayoutGroup))
                {
                    if (group is VerticalLayoutGroup list)
                    {
                        list.childControlWidth = true;
                        list.childForceExpandWidth = true;
                    }

                    for (var t = root; t != item; t = (RectTransform)t.parent)
                    {
                        Ui.Stretch(t);
                    }

                    return item;
                }

                if (group is HorizontalLayoutGroup pair)
                {
                    pair.enabled = false;
                }

                item = parent;
            }

            return root;
        }

        private static void Right(RectTransform rect, float inset, float width, float height, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-inset, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Height(RectTransform item, float height)
        {
            var element = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
            element.minWidth = -1f;
            element.preferredWidth = -1f;
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        // A list of rows: full width, each as high as it asks, 10 apart.
        private static void List(VerticalLayoutGroup list, int sides, int top)
        {
            if (list == null)
            {
                return;
            }

            list.padding = new RectOffset(sides, sides, top, top);
            list.spacing = Gap;
            list.childAlignment = TextAnchor.UpperCenter;
            list.childControlWidth = true;
            list.childForceExpandWidth = true;
            list.childControlHeight = true;
            list.childForceExpandHeight = false;
        }

        // The row places its parts itself from now on.
        private static void Unlayout(Transform root)
        {
            foreach (var group in root.GetComponents<LayoutGroup>())
            {
                group.enabled = false;
            }

            Fitter(root, false);
        }

        private static void Fitter(Transform t, bool on)
        {
            foreach (var fitter in t.GetComponents<ContentSizeFitter>())
            {
                fitter.enabled = on;
            }
        }

        private static void Enable<T>(Transform t, bool on) where T : Behaviour
        {
            var component = t.GetComponent<T>();
            if (component != null)
            {
                component.enabled = on;
            }
        }

        private static void Paint(Transform t, Color color, bool plain)
        {
            var image = t.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.enabled = true;
            if (plain)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
            }

            image.color = color;
        }

        private static void Reparent(Transform t, Transform root)
        {
            if (t.parent != root)
            {
                t.SetParent(root, false);
            }
        }

        private static T Parent<T>(Transform t) where T : Component
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var found = p.GetComponent<T>();
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool New(Component control)
        {
            if (control.GetComponent<Row10>() != null)
            {
                return false;
            }

            control.gameObject.AddComponent<Row10>();
            return true;
        }

        private static void Try(Component control, Action restyle)
        {
            try
            {
                restyle();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"1.0 settings row {control.name}: {ex.Message}");
            }
        }
    }

    // On a settings tab: lays it out as 1.0 when shown, and the rows it makes afterwards.
    internal class SettingsLook10 : MonoBehaviour
    {
        private bool _laidOut;

        internal void Apply()
        {
            var tab = GetComponent<SettingsTab>();
            if (!_laidOut)
            {
                _laidOut = true;
                Settings10.Container(tab);
            }

            Settings10.Rows(transform);
            Settings10.Order(tab);
            Settings10.Fit(transform);
            Settings10.Extras(tab);
        }

        private void LateUpdate()
        {
            if (Time.frameCount % 30 == 0)
            {
                Settings10.Rows(transform);
                Settings10.Order(GetComponent<SettingsTab>());
                Settings10.Fit(transform);
                Settings10.Extras(GetComponent<SettingsTab>());
            }
        }
    }

    // A drop-down row with a button of its own after the drop-down.
    internal class ButtonRow10 : MonoBehaviour
    {
        internal RectTransform Button;
        internal RectTransform Control;
        internal TMP_Text Label;
        internal float Width = -1f;
    }

    // Marks a settings row (or the tab names) already laid out as 1.0.
    internal class Row10 : MonoBehaviour
    {
    }
}
