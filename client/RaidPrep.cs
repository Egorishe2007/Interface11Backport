using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using EFT.UI.Insurance;
using EFT.UI.Matchmaker;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Interface10
{
    // 1.2's raid preparation, the screens after the location (MenuUIScene, against 0.16's):
    // - the offline raid settings: every drop-down as 1.2's (a grey frame, 1.2's arrow button and arrows, the value 15
    //   in the middle, the open list framed too) and 1.2's big check marks; the window higher on the screen;
    // - insurance: the two steps as numbered bars across the panel; the insurer's avatar and drop-down on a dark band,
    //   its standing, insurance rate, return time and return rate in a striped box at the band's right end; the item
    //   rows darker with the arrow button at their right end; the money in the stash and the sum to pay in two boxes
    //   under the list; the warning centred in bold, the tab icons 24;
    // - the ready screen: the player's account and prestige icons beside the name instead of over it, the name
    //   17.75, the raid conditions 20 higher;
    // - on all three the buttons at the foot 16 higher and 2 apart, and on the side selection, the map's entry points and
    //   the key access screens, where 1.2 changed nothing else (the side screen's health icons are the character's
    //   panel, see Inventory10).
    // The game's own objects are moved and restyled, so its code still fills them.
    internal static class RaidPrep10
    {
        private static readonly Color FrameColor = new Color(0.361f, 0.377f, 0.38f, 1f);
        private static readonly Color Muted = new Color(0.533f, 0.525f, 0.49f, 1f);
        private static readonly Color Light = new Color(0.894f, 0.886f, 0.82f, 1f);
        private static readonly Color StepColor = new Color(0.196f, 0.208f, 0.188f, 1f);
        private static readonly Color StepText = new Color(0.769f, 0.757f, 0.69f, 1f);
        private static readonly Color BandColor = new Color(0.067f, 0.071f, 0.071f, 1f);
        private static readonly Color PanelColor = new Color(0.051f, 0.051f, 0.055f, 1f);
        private static readonly Color BoxColor = new Color(0.055f, 0.059f, 0.067f, 1f);
        private static readonly Color RowColor = new Color(0.0745f, 0.0784f, 0.0784f, 1f);
        private static readonly Color RowSelected = new Color(0.1431f, 0.1509f, 0.1509f, 1f);
        private static readonly Color StatusColor = new Color(0.765f, 0.804f, 0.828f, 1f);

        private static Sprite Frame => Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f));

        // 1.2's values of what moved on the insurance screen (ui10/cmp/insurance-12.json), by path under the screen.
        private static readonly Dictionary<string, float[]> InsuranceLayout = new Dictionary<string, float[]>
        {
            ["WarningPanel"] = new[] { 0f, 1f, 0f, 1f, 1267.6f, -102f, 1118.5f, 80f, 0.5f, 1f },
            ["Tab Bar"] = new[] { 0.3663f, 1f, 0.952f, 1f, 1.01f, -188.9f, -4f, 36.1f, 0.5f, 1f },
            ["Tab Bar/Tabs"] = new[] { 0f, 0.5f, 0f, 0.5f, 0f, 0f, 30f, 35f, 0f, 0.5f },
            ["Tab Bar/Tabs/Insured/Normal/Icon"] = new[] { 0f, 0.5f, 0f, 0.5f, 24f, 0f, 24f, 24f, 0f, 0.5f },
            ["Tab Bar/Tabs/Insured/Selected/Icon"] = new[] { 0f, 0.5f, 0f, 0.5f, 24f, 0f, 25f, 24f, 0f, 0.5f },
            ["Tab Bar/Tabs/ToInsure/Normal/Icon"] = new[] { 0f, 0.5f, 0f, 0.5f, 24f, 0f, 24f, 24f, 0f, 0.5f },
            ["Tab Bar/Tabs/ToInsure/Selected/Icon"] = new[] { 0f, 0.5f, 0f, 0.5f, 24f, 0f, 25f, 24f, 0f, 0.5f },
            ["ItemsToInsurePanel"] = new[] { 0.3713f, 0f, 0.952f, 1f, -3.49f, -11.25f, 5f, -422.5f, 0.5f, 0.5f },
            ["InsuredItemsPanel"] = new[] { 0.3713f, 0f, 0.952f, 1f, -3.49f, -14.1f, 5f, -416.8f, 0.5f, 0.5f },
            ["InsuredItemsPanel/InsuredItemsList"] = new[] { 0f, 0f, 1f, 1f, 0f, 0f, 0f, -20f, 0.5f, 0.5f },
        };

        // The buttons at the foot of 1.2's matchmaker screens: 56 from the bottom (0.16: 40), 2 apart (0.16: 15).
        internal static void Foot(Transform screen, string name)
        {
            if (!(screen.Find(name) is RectTransform group))
            {
                return;
            }

            group.anchoredPosition = new Vector2(group.anchoredPosition.x, 56f);
            if (group.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup layout && layout.spacing > 2f)
            {
                group.sizeDelta = new Vector2(group.sizeDelta.x, group.sizeDelta.y - (layout.spacing - 2f));
                layout.spacing = 2f;
            }
        }

        // ---- drop-downs and check boxes ----

        internal static void Dropdown(DropDownBox box)
        {
            if (box == null || box.GetComponent<Restyled>() != null)
            {
                return;
            }

            box.gameObject.AddComponent<Restyled>();
            var t = box.transform;
            if (t.Find("Background")?.GetComponent<Image>() is Image back)
            {
                back.color = new Color(0f, 0f, 0f, 0.8f);
            }

            if (t.Find("Text") is RectTransform value)
            {
                value.anchoredPosition = new Vector2(-5f, -2f);
                value.sizeDelta = new Vector2(-50f, -8f);
                if (value.GetComponent<TMP_Text>() is TMP_Text text)
                {
                    text.enableAutoSizing = false;
                    text.fontSize = 15f;
                }
            }

            if (t.Find("Button") is RectTransform button)
            {
                Ui.Place(button, new[] { 1f, 0.5f, 1f, 0.5f, -8f, 0f, 18f, 18f, 1f, 0.5f });
                if (button.GetComponent<Image>() is Image plate)
                {
                    plate.sprite = Ui.FileSprite("dropdown_button_background_new");
                }

                Arrow(button, "Closed Image", "Dropdown_arrow_down_new");
                Arrow(button, "Open Image", "Dropdown_arrow_up_new");
            }

            if (t.Find("Shadow")?.GetComponent<Image>() is Image shadow)
            {
                shadow.color = new Color(1f, 1f, 1f, 0.341f);
            }

            if (t.Find("Border")?.GetComponent<Image>() is Image border)
            {
                Framed(border);
                border.gameObject.SetActive(true);
            }

            if (t.Find("Element") is Transform template)
            {
                Option(template);
            }

            // the list it opens: no room kept for the scroll bar (it goes over the options, as in 1.2), framed
            if (t.Find("Open Panel") is RectTransform panel)
            {
                if (panel.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list)
                {
                    list.padding = new RectOffset(1, 1, 1, 1);
                }

                if (panel.Find("Shadow")?.GetComponent<Image>() is Image listShadow)
                {
                    listShadow.color = new Color(1f, 1f, 1f, 0.545f);
                }

                if (panel.Find("Border10") == null)
                {
                    var frame = Ui.Image(panel, "Border10", FrameColor);
                    Framed(frame);
                    Ui.Stretch(frame.rectTransform);
                    Ui.Element(frame.gameObject, ignore: true);
                }

                foreach (var option in panel.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Element")))
                {
                    Option(option.transform);
                }
            }
        }

        private static void Arrow(Transform button, string name, string sprite)
        {
            if (button.Find(name) is RectTransform arrow)
            {
                arrow.sizeDelta = new Vector2(10f, 8f);
                if (arrow.GetComponent<Image>() is Image image)
                {
                    image.sprite = Ui.FileSprite(sprite);
                }
            }
        }

        // An option of an open list: its name in the middle, 8 in from each side.
        private static void Option(Transform element)
        {
            if (element.Find("Text") is RectTransform text)
            {
                text.sizeDelta = new Vector2(-16f, text.sizeDelta.y);
                if (text.GetComponent<TMP_Text>() is TMP_Text label)
                {
                    label.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        private static void Framed(Image image)
        {
            image.sprite = Frame;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = FrameColor;
            image.raycastTarget = false;
        }

        // 1.2's check mark (checkmark_big), 3 in from the box on every side.
        private static void Check(UpdatableToggle toggle)
        {
            if (toggle != null && toggle.transform.Find("Background/Checkmark") is RectTransform mark && mark.GetComponent<Image>() is Image image)
            {
                image.sprite = Ui.FileSprite("checkmark_big");
                if (mark.anchorMin == Vector2.zero && mark.anchorMax == Vector2.one)
                {
                    mark.sizeDelta = new Vector2(-6f, -6f);
                }
            }
        }

        // ---- offline raid settings ----

        internal static void OfflineScreen(MatchmakerOfflineRaidScreen screen)
        {
            if (screen.GetComponent<Restyled>() != null)
            {
                return;
            }

            screen.gameObject.AddComponent<Restyled>();
            var root = screen.transform;
            if (root.Find("Content")?.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup content)
            {
                var pad = content.padding;
                content.padding = new RectOffset(pad.left, pad.right, pad.top, 196);
            }

            Foot(root, "ScreenDefaultButtons");
            if (root.Find("ReadyButton") is RectTransform ready)
            {
                ready.anchoredPosition = new Vector2(ready.anchoredPosition.x, 56f);
            }
        }

        internal static void SettingsWindow(RaidSettingsWindow window)
        {
            if (window.GetComponent<Restyled>() != null)
            {
                return;
            }

            window.gameObject.AddComponent<Restyled>();
            // 1.2 hangs the window by a point 37% up it instead of its middle: it sits 89 higher, clear of the buttons
            var rect = (RectTransform)window.transform;
            rect.pivot = new Vector2(rect.pivot.x, 0.37f);
            foreach (var box in window.GetComponentsInChildren<DropDownBox>(true))
            {
                Dropdown(box);
            }

            foreach (var toggle in window.GetComponentsInChildren<UpdatableToggle>(true))
            {
                Check(toggle);
            }
        }

        // ---- insurance ----

        internal static void InsuranceScreen(MatchmakerInsuranceScreen screen)
        {
            if (screen.GetComponent<Restyled>() != null)
            {
                return;
            }

            screen.gameObject.AddComponent<Restyled>();
            var root = screen.transform;
            Ui.Place(root, InsuranceLayout);
            Foot(root, "ScreenDefaultButtons");
            Foot(root, "ScreenAdditionalButtons");

            if (root.Find("WarningPanel") is Transform warning)
            {
                if (warning.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup layout)
                {
                    layout.padding = new RectOffset(15, 15, 5, 5);
                }

                Style(warning.Find("Warning Description"), 16f, null, FontStyles.Bold, TextAlignmentOptions.Center);
                warning.Find("Icon")?.gameObject.SetActive(false);
            }

            root.Find("Tab Bar/Background")?.gameObject.SetActive(false);

            var t = Traverse.Create(screen);
            if (root.Find("ItemsToInsurePanel") is RectTransform panel)
            {
                ItemsToInsure(panel);
            }

            var toInsure = t.Field<ItemsToInsureScreen>("_itemsToInsureScreen").Value;
            ItemRow(toInsure != null ? Traverse.Create(toInsure).Field<ItemToInsurePanel>("_itemToInsurePanel").Value : null);

            if (root.Find("InsuredItemsPanel") is RectTransform insuredPanel)
            {
                if (insuredPanel.GetComponent<Image>() is Image back)
                {
                    back.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.839f);
                }

                Header(insuredPanel.Find("InsuredItemsList/HeaderLabel"));
            }

            var insured = t.Field<InsuredItemsScreen>("_insuredItemsScreen").Value;
            InsuredRow(insured != null ? Traverse.Create(insured).Field<InsuredItemPanel>("_insuredItemPanel").Value : null);
        }

        // The panel of the items to insure as one column: step 1, the insurer's band, step 2, the item list (the rest of
        // the height), the money boxes; dark under all but the boxes.
        private static void ItemsToInsure(RectTransform panel)
        {
            if (panel.GetComponent<Image>() is Image old)
            {
                old.enabled = false;
            }

            var layout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.gameObject.AddComponent<VerticalLayoutGroup>();
            Group(layout, TextAnchor.UpperLeft, new RectOffset(), 5f, true, true, true);

            var back = Ui.Image(panel, "Background10", PanelColor);
            back.rectTransform.SetAsFirstSibling();
            Ui.Place(back.rectTransform, new[] { 0f, 0f, 1f, 1f, 0f, 32.5f, 0f, -65f, 0.5f, 0.5f });
            Ui.Element(back.gameObject, ignore: true);

            Step(panel.Find("FirstStepLabel"), "1", StepText);
            Step(panel.Find("SecondStepLabel"), "2", new Color(0.772f, 0.765f, 0.698f, 1f));
            InsureAll(panel.Find("SecondStepLabel/Toggle") as RectTransform);

            var details = panel.Find("ReturnDetails");
            if (panel.Find("SelectRepairerPanel") is RectTransform band)
            {
                Insurer(band, details);
            }

            if (panel.Find("ItemsToInsureList") is Transform items)
            {
                Header(items.Find("HeaderLabel"));
            }

            Sums(panel, details);
            details?.gameObject.SetActive(false);
        }

        // A step's title: a bar 40 high with the step's number on 1.2's tab shape at its left.
        private static void Step(Transform step, string number, Color color)
        {
            if (step == null)
            {
                return;
            }

            var go = step.gameObject;
            NoFitter(go);
            var bar = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            bar.color = StepColor;
            bar.raycastTarget = false;
            var element = Element(go);
            element.minHeight = 40f;
            element.preferredHeight = 40f;
            if (go.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                Group(row, TextAnchor.MiddleLeft, new RectOffset(0, 14, 0, 0), 10f, true, false);
            }

            var old = step.Find("Image");
            old?.gameObject.SetActive(false);
            var plate = Ui.Image(step, "NumberContainer", Color.white, Ui.FileSprite("Insurance_LL-form"));
            plate.rectTransform.SetSiblingIndex(old != null ? old.GetSiblingIndex() + 1 : 0);
            plate.rectTransform.sizeDelta = new Vector2(84f, 40f);
            Ui.Element(plate.gameObject, preferredWidth: 84f, preferredHeight: 40f);
            var digit = Ui.Text(plate.transform, "Text (TMP)", 18f, Muted);
            digit.fontStyle = FontStyles.Bold;
            digit.alignment = TextAlignmentOptions.Center;
            digit.text = number;
            Ui.Place(digit.rectTransform, new[] { 0f, 0f, 1f, 1f, -5f, 0f, -20f, 0f, 0.5f, 0.5f });

            if (step.Find("Text") is Transform title)
            {
                Style(title, 18f, color);
                if (title.GetComponent<ContentSizeFitter>() is ContentSizeFitter fitter)
                {
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }
        }

        // INSURE ALL at the right end of step 2's bar: the box and a button 139 wide, its text in the middle.
        private static void InsureAll(RectTransform toggle)
        {
            if (toggle == null)
            {
                return;
            }

            var space = Ui.Rect(toggle.parent, "Space10");
            space.SetSiblingIndex(toggle.GetSiblingIndex());
            space.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            toggle.sizeDelta = new Vector2(170f, 26.9f);
            var element = Element(toggle.gameObject);
            element.preferredWidth = 170f;
            element.flexibleWidth = -1f;
            if (toggle.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.childControlWidth = true;
                row.childForceExpandWidth = false;
            }

            toggle.Find("Image")?.gameObject.SetActive(false);
            if (toggle.Find("InsureAllToggle") is RectTransform box)
            {
                Element(box.gameObject).preferredWidth = 21f;
                if (box.Find("Background") is RectTransform frame)
                {
                    frame.sizeDelta = new Vector2(20f, 20f);
                }

                if (box.Find("Background/Checkmark") is RectTransform mark)
                {
                    mark.anchoredPosition = Vector2.zero;
                }
            }

            if (toggle.Find("InsureAllLabel") is RectTransform label)
            {
                Element(label.gameObject).preferredWidth = 139f;
                label.sizeDelta = new Vector2(label.sizeDelta.x, 23.4f);
                if (label.Find("Text") is RectTransform text)
                {
                    Ui.Stretch(text);
                    if (text.GetComponent<TMP_Text>() is TMP_Text tmp)
                    {
                        tmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                label.Find("IconPlaceholder")?.gameObject.SetActive(false);
            }
        }

        // The insurer's band: avatar and drop-down at the left, the striped box of its figures at the right end.
        private static void Insurer(RectTransform band, Transform details)
        {
            NoFitter(band.gameObject);
            if (band.GetComponent<Image>() is Image back)
            {
                back.enabled = true;
                back.color = BandColor;
            }

            if (band.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                Group(row, TextAnchor.MiddleLeft, new RectOffset(), 0f, true, true);
            }

            var repairer = band.Find("Repairer");
            var standing = band.Find("Repairer/RepairerOptionsPanel/Standing");
            if (repairer != null)
            {
                if (repairer.GetComponent<Image>() is Image plate)
                {
                    plate.enabled = false;
                }

                if (repairer.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup layout)
                {
                    Group(layout, TextAnchor.MiddleLeft, new RectOffset(33, 0, 5, 5), 0f, true, true);
                }

                foreach (var element in repairer.GetComponents<LayoutElement>())
                {
                    element.minWidth = -1f;
                    element.preferredWidth = 391f;
                    element.flexibleWidth = 0f;
                    element.minHeight = 110f;
                    element.preferredHeight = 110f;
                }

                if (repairer.Find("RepairerAvatarPanel") is Transform avatar)
                {
                    var element = Element(avatar.gameObject);
                    element.preferredWidth = 90f;
                    element.preferredHeight = 90f;
                    if (avatar.Find("Border")?.GetComponent<Image>() is Image border)
                    {
                        border.color = StepText;
                    }

                    Style(avatar.Find("Rank Panel/Rank"), 14f, null, null, TextAlignmentOptions.Center);
                    if (avatar.Find("Rank Panel/Elite Rank") is RectTransform elite)
                    {
                        Ui.Place(elite, new[] { 0.5f, 0.5f, 0.5f, 0.5f, -0.5f, 1f, 18f, 18f, 0.5f, 0.5f });
                    }
                }

                if (repairer.Find("RepairerOptionsPanel") is Transform options)
                {
                    var element = Element(options.gameObject);
                    element.preferredWidth = 268f;
                    element.preferredHeight = 90f;
                    if (options.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup column)
                    {
                        column.padding = new RectOffset(10, 10, 0, 0);
                        column.spacing = 0f;
                    }

                    if (options.Find("TraderDropdown") is Transform drop)
                    {
                        Element(drop.gameObject).preferredHeight = 35f;
                        Dropdown(drop.GetComponent<DropDownBox>());
                    }
                }
            }

            var space = Ui.Rect(band, "Space10");
            space.SetSiblingIndex(repairer != null ? repairer.GetSiblingIndex() + 1 : 0);
            space.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (!(band.Find("Qualities") is Transform qualities))
            {
                return;
            }

            var go = qualities.gameObject;
            NoFitter(go);
            Group(Swap<HorizontalLayoutGroup>(go), TextAnchor.MiddleLeft, new RectOffset(), 3.55f, true, true);
            if (go.GetComponent<Image>() is Image stripes)
            {
                stripes.sprite = Ui.FileSprite("CHTOTO");
                stripes.type = Image.Type.Tiled;
                stripes.color = Color.white;
            }

            foreach (var element in go.GetComponents<LayoutElement>())
            {
                element.minWidth = 410f;
                element.preferredWidth = 410f;
                element.minHeight = 100f;
                element.preferredHeight = 100f;
                // 410 wide: the space before it takes the rest (its figures column would pass its flexibility on)
                element.flexibleWidth = 0f;
            }

            // the standing and the insurance rate from under the drop-down, in a column at the box's left
            if (standing != null)
            {
                standing.SetParent(qualities, false);
                standing.SetSiblingIndex(0);
                Group(Swap<VerticalLayoutGroup>(standing.gameObject), TextAnchor.MiddleCenter, new RectOffset(21, 0, 0, 0), 10f, true, true);
                var element = Element(standing.gameObject);
                element.preferredWidth = 87f;
                element.preferredHeight = 100f;
                element.flexibleWidth = -1f;
                Figure(standing.Find("Standing"), "standing_icon", 30f, 30f);
                Figure(standing.Find("InsuranceRate"), "insurance_shield_icon", 24f, 27f);
            }

            if (!(qualities.Find("RepairerQualitiesPanel") is Transform list))
            {
                return;
            }

            NoFitter(list.gameObject);
            if (list.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup lines)
            {
                Group(lines, TextAnchor.MiddleLeft, new RectOffset(24, 0, 0, 0), 10f, true, true);
            }

            var listElement = Element(list.gameObject);
            listElement.preferredWidth = 236f;
            listElement.preferredHeight = 95f;
            listElement.flexibleWidth = 1f;

            // the return time from the old line under the list, after the minimum payment
            if (details?.Find("ReturnTime") is Transform time)
            {
                var min = list.Find("MinPaymentGroup");
                time.SetParent(list, false);
                time.SetSiblingIndex(min != null ? min.GetSiblingIndex() + 1 : 0);
                if (time.Find("Image") is Transform icon && icon.GetComponent<Image>() is Image image)
                {
                    image.sprite = Ui.FileSprite("icon_time");
                    image.type = Image.Type.Simple;
                    image.color = Muted;
                    var element = Element(icon.gameObject);
                    element.preferredWidth = 20f;
                    element.preferredHeight = 20f;
                }

                Style(time.Find("Label"), 15f, Muted);
                Style(time.Find("Value"), 15f, Muted);
            }

            Quality(list.Find("MinPaymentGroup"), null, 12f);
            Quality(list.Find("ReturnRateGroup"), "Insurance_Return-Rate_Icon", 15f);
            Quality(list.Find("ExceptionsGroup"), "icon_info_removeeffects", 15f);
            list.Find("ExceptionsGroup")?.gameObject.SetActive(false);
        }

        // A line of the standing column: the icon in 1.2's muted khaki, the figure 15.
        private static void Figure(Transform row, string sprite, float width, float height)
        {
            if (row == null)
            {
                return;
            }

            NoFitter(row.gameObject);
            var element = Element(row.gameObject);
            element.preferredHeight = 25f;
            element.flexibleWidth = 1f;
            if (row.Find("Image") is Transform icon && icon.GetComponent<Image>() is Image image)
            {
                image.sprite = Ui.FileSprite(sprite);
                image.color = Muted;
                var size = Element(icon.gameObject);
                size.preferredWidth = width;
                size.preferredHeight = height;
            }

            Style(row.Find("Text"), 15f, Muted);
        }

        // A line of the figures column: 1.2's icon in front, everything muted khaki.
        private static void Quality(Transform group, string icon, float size)
        {
            if (group == null)
            {
                return;
            }

            NoFitter(group.gameObject);
            Element(group.gameObject).preferredHeight = 25f;
            if (icon != null && group.Find("Icon10") == null)
            {
                var image = Ui.Image(group, "Icon10", Muted, Ui.FileSprite(icon));
                image.transform.SetAsFirstSibling();
                Ui.Element(image.gameObject, preferredWidth: 20f, preferredHeight: 20f);
            }

            foreach (var text in group.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing = false;
                text.fontSize = size;
                text.color = Muted;
            }

            if (group.Find("PriceIcon")?.GetComponent<Image>() is Image price)
            {
                price.color = Muted;
            }
        }

        // The list's header: on 1.2's stripes, its icon and count in the middle.
        private static void Header(Transform header)
        {
            if (header == null)
            {
                return;
            }

            if (header.GetComponent<Image>() is Image back)
            {
                back.sprite = Ui.FileSprite("CHTOTO");
                back.type = Image.Type.Tiled;
                back.color = Color.white;
            }

            if (header.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.childAlignment = TextAnchor.MiddleCenter;
            }

            if (header.Find("Icon") is Transform icon)
            {
                var element = Element(icon.gameObject);
                element.preferredWidth = 20f;
                element.preferredHeight = 20f;
            }
        }

        // Under the list: the money in the stash (label over figure) in a wide box, the sum to pay in a box at the right.
        private static void Sums(RectTransform panel, Transform details)
        {
            var block = Ui.Rect(panel, "ReturnBlock");
            block.SetAsLastSibling();
            Ui.Layout<HorizontalLayoutGroup>(block.gameObject, TextAnchor.LowerCenter, spacing: 5f);
            // 60 high, whatever is left goes to the list (its boxes would pass their flexibility on)
            Ui.Element(block.gameObject, preferredHeight: 60f).flexibleHeight = 0f;

            var stash = Ui.Image(block, "Stash", BoxColor);
            Ui.Layout<HorizontalLayoutGroup>(stash.gameObject, TextAnchor.MiddleLeft, top: 5, bottom: 5);
            Ui.Element(stash.gameObject, preferredWidth: 785f, preferredHeight: 60f).flexibleWidth = 1f;
            if (details?.Find("CashInStash") is Transform cash)
            {
                cash.SetParent(stash.transform, false);
                NoFitter(cash.gameObject);
                Group(Swap<VerticalLayoutGroup>(cash.gameObject), TextAnchor.UpperLeft, new RectOffset(35, 0, 5, 5), 0f, true, true);
                Element(cash.gameObject).flexibleWidth = 1f;
                Style(cash.Find("Label"), 18f, Muted);
                Style(cash.Find("Value"), 18f, Muted);
                if (cash.Find("Icon") is RectTransform rouble && cash.Find("Value") is Transform value)
                {
                    rouble.SetParent(value, false);
                    Element(rouble.gameObject).ignoreLayout = true;
                    Ui.Place(rouble, new[] { 1f, 0.5f, 1f, 0.5f, 3f, 0f, 18f, 18f, 0f, 0.5f });
                    if (rouble.GetComponent<Image>() is Image image)
                    {
                        image.color = Muted;
                    }
                }
            }

            var pay = Ui.Image(block, "ToPay", BoxColor);
            Ui.Layout<HorizontalLayoutGroup>(pay.gameObject, TextAnchor.MiddleLeft);
            Ui.Element(pay.gameObject, preferredWidth: 324f, preferredHeight: 60f);
            if (details?.Find("TotalSum") is Transform sum)
            {
                sum.SetParent(pay.transform, false);
                NoFitter(sum.gameObject);
                Group(Swap<VerticalLayoutGroup>(sum.gameObject), TextAnchor.MiddleCenter, new RectOffset(), 0f, true, true);
                Element(sum.gameObject).flexibleWidth = 1f;
                Style(sum.Find("Label"), 18f, Light);

                var row = Ui.Rect(sum, "ValueContainer");
                Ui.Layout<HorizontalLayoutGroup>(row.gameObject, TextAnchor.UpperLeft, spacing: 4f);
                var value = sum.Find("Value");
                var icon = sum.Find("Icon");
                if (value != null)
                {
                    value.SetParent(row, false);
                    Style(value, 18f, Light);
                }

                if (icon != null)
                {
                    icon.SetParent(row, false);
                    var size = Element(icon.gameObject);
                    size.preferredWidth = 20f;
                    size.preferredHeight = 20f;
                    if (icon.GetComponent<Image>() is Image image)
                    {
                        image.color = Light;
                    }
                }
            }
        }

        // The row of an item to insure (the template every row and child row is made from): darker, the arrow that opens
        // its contents at the right end, the price before it.
        private static void ItemRow(ItemToInsurePanel row)
        {
            if (row == null || row.GetComponent<Restyled>() != null)
            {
                return;
            }

            row.gameObject.AddComponent<Restyled>();
            var t = Traverse.Create(row);
            t.Field("_defaultColor").SetValue(RowColor);
            t.Field("_selectedColor").SetValue(RowSelected);
            var main = row.transform.Find("MainItemContainer/Background");
            if (main == null)
            {
                return;
            }

            if (main.GetComponent<Image>() is Image back)
            {
                back.color = RowColor;
            }

            var bar = main.Find("RightSideBar") as RectTransform;
            if (bar?.Find("Button") is RectTransform button)
            {
                button.SetParent(main, false);
                Element(button.gameObject).ignoreLayout = true;
                Ui.Place(button, new[] { 1f, 0.5f, 1f, 0.5f, -6f, 0f, 17f, 17f, 1f, 0.5f });
                if (button.GetComponent<Image>() is Image plate)
                {
                    plate.sprite = Ui.FileSprite("button_itemtype_idle");
                }
            }

            if (bar != null)
            {
                bar.anchoredPosition = new Vector2(-32f, bar.anchoredPosition.y);
                if (bar.Find("PriceGroup/PriceIcon") is Transform rouble)
                {
                    var size = Element(rouble.gameObject);
                    size.preferredWidth = 16f;
                    size.preferredHeight = 16f;
                }
            }
        }

        private static void InsuredRow(InsuredItemPanel row)
        {
            if (row == null || row.GetComponent<Restyled>() != null)
            {
                return;
            }

            row.gameObject.AddComponent<Restyled>();
            Element(row.gameObject).preferredHeight = 40f;
            if (row.transform.Find("Icon") is RectTransform icon)
            {
                icon.sizeDelta = new Vector2(30f, 30f);
            }

            if (row.transform.Find("Background")?.GetComponent<Image>() is Image back)
            {
                back.color = RowColor;
            }
        }

        // ---- ready screen ----

        internal static void AcceptScreen(MatchMakerAcceptScreen screen)
        {
            if (screen.GetComponent<Restyled>() != null)
            {
                return;
            }

            screen.gameObject.AddComponent<Restyled>();
            var root = screen.transform;
            Foot(root, "ScreenDefaultButtons");
            if (root.Find("Conditions Panel") is RectTransform conditions)
            {
                conditions.anchoredPosition = new Vector2(conditions.anchoredPosition.x, 190f);
            }

            if (!(root.Find("PreviewsPanel/CurrentPlayerModelView/PlayerInfo/Player Selected Name Panel") is Transform panel))
            {
                Interface10Plugin.Log.LogWarning("Ready screen: no player name panel");
                return;
            }

            Style(panel.Find("ScavName"), 20f, null);
            Style(panel.Find("Status"), 12f, StatusColor);
            if (!(panel.Find("ProfileInfo") is Transform info))
            {
                return;
            }

            if (info.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.spacing = 4f;
            }

            foreach (var element in info.GetComponents<LayoutElement>())
            {
                element.preferredWidth = 210f;
            }

            if (!(info.Find("Name") is Transform name))
            {
                return;
            }

            Style(name, 17.75f, null);
            Element(name.gameObject).minHeight = 24f;
            // the account icon before the name and the prestige after it, in the row (0.16 hangs them on the name)
            if (name.Find("AccountType") is Transform account)
            {
                account.SetParent(info, false);
                account.SetSiblingIndex(name.GetSiblingIndex());
                Size(account, 20f);
            }

            if (name.Find("PrestigeLevel") is Transform prestige)
            {
                prestige.SetParent(info, false);
                prestige.SetSiblingIndex(name.GetSiblingIndex() + 1);
                Size(prestige, 24f);
            }
        }

        private static void Size(Transform icon, float size)
        {
            foreach (var element in icon.GetComponents<LayoutElement>())
            {
                element.minWidth = size;
                element.minHeight = size;
                element.preferredWidth = size;
                element.preferredHeight = size;
            }
        }

        // ---- helpers ----

        private static void Group(HorizontalOrVerticalLayoutGroup group, TextAnchor alignment, RectOffset padding, float spacing,
            bool controlWidth, bool controlHeight, bool expandWidth = false)
        {
            group.childAlignment = alignment;
            group.padding = padding;
            group.spacing = spacing;
            group.childControlWidth = controlWidth;
            group.childControlHeight = controlHeight;
            group.childForceExpandWidth = expandWidth;
            group.childForceExpandHeight = false;
        }

        // A layout group of the other direction: an object holds one, so the old one goes first.
        private static T Swap<T>(GameObject go) where T : HorizontalOrVerticalLayoutGroup
        {
            var old = go.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (old != null && !(old is T))
            {
                Object.DestroyImmediate(old);
            }

            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static void NoFitter(GameObject go)
        {
            foreach (var fitter in go.GetComponents<ContentSizeFitter>())
            {
                Object.DestroyImmediate(fitter);
            }
        }

        private static LayoutElement Element(GameObject go) => go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();

        private static void Style(Transform target, float size, Color? color, FontStyles? style = null, TextAlignmentOptions? alignment = null)
        {
            if (target == null || !(target.GetComponent<TMP_Text>() is TMP_Text text))
            {
                return;
            }

            text.enableAutoSizing = false;
            text.fontSize = size;
            if (color.HasValue)
            {
                text.color = color.Value;
            }

            if (style.HasValue)
            {
                text.fontStyle = style.Value;
            }

            if (alignment.HasValue)
            {
                text.alignment = alignment.Value;
            }
        }

        private static void Run(string what, Action action)
        {
            if (!Interface10Plugin.RaidPrep.Value)
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

        // ---- side selection, entry points, key access ----

        // Only the buttons at the foot: the group 16 higher and 2 apart, and the button standing alone as high.
        internal static void FootOnly(Component screen, string alone)
        {
            if (screen == null || screen.GetComponent<Restyled>() != null)
            {
                return;
            }

            screen.gameObject.AddComponent<Restyled>();
            var root = screen.transform;
            Foot(root, "ScreenDefaultButtons");
            if (alone != null && root.Find(alone) is RectTransform button)
            {
                button.anchoredPosition = new Vector2(button.anchoredPosition.x, 56f);
            }
        }

        // ---- patches ----

        [HarmonyPatch(typeof(RaidSettingsWindow), nameof(RaidSettingsWindow.Show))]
        internal static class SettingsWindowPatch
        {
            [HarmonyPrefix]
            private static void Prefix(RaidSettingsWindow __instance) => Run("Raid settings window", () => SettingsWindow(__instance));
        }

        [HarmonyPatch]
        internal static class OfflineScreenPatch
        {
            private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(MatchmakerOfflineRaidScreen))
                .First(m => m.Name == nameof(MatchmakerOfflineRaidScreen.Show) && m.GetParameters().Length == 3);

            [HarmonyPostfix]
            private static void Postfix(MatchmakerOfflineRaidScreen __instance) => Run("Offline raid screen", () => OfflineScreen(__instance));
        }

        // Before the first rows are made, so they come from the restyled templates.
        [HarmonyPatch]
        internal static class InsuranceScreenPatch
        {
            private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(MatchmakerInsuranceScreen))
                .First(m => m.Name == nameof(MatchmakerInsuranceScreen.Show) && m.GetParameters().Length == 4);

            [HarmonyPrefix]
            private static void Prefix(MatchmakerInsuranceScreen __instance) => Run("Insurance screen", () => InsuranceScreen(__instance));
        }

        [HarmonyPatch]
        internal static class FootOnlyPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                new[] { typeof(MatchMakerSideSelectionScreen), typeof(MatchmakerMapPointsScreen), typeof(MatchmakerKeyAccessScreen) }
                    .Select(type => AccessTools.GetDeclaredMethods(type).First(m => m.Name == "Show" && m.GetParameters().Length == 1));

            [HarmonyPostfix]
            private static void Postfix(Component __instance) => Run("Matchmaker screen", () => FootOnly(__instance,
                __instance is MatchmakerMapPointsScreen ? "ReadyButton" : __instance is MatchmakerKeyAccessScreen ? "BackButton" : null));
        }

        [HarmonyPatch]
        internal static class AcceptScreenPatch
        {
            private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(MatchMakerAcceptScreen))
                .First(m => m.Name == nameof(MatchMakerAcceptScreen.Show) && m.GetParameters().Length == 3);

            [HarmonyPostfix]
            private static void Postfix(MatchMakerAcceptScreen __instance) => Run("Ready screen", () => AcceptScreen(__instance));
        }
    }
}
