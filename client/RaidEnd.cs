using System;
using System.Linq;
using System.Reflection;
using EFT.UI;
using EFT.UI.SessionEnd;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's screens after a raid (SessionEndUIScene, against 0.16's):
    // - raid over: 1.2's redrawn clock (a light square) by the raid time, the level badge 91.6, the warning 21 higher;
    // - kills: the panel wider (1360) and darker, 1.2's columns, the scroll bar in the list; NO KILLS in the middle of
    //   the panel over 1.2's stripes;
    // - statistics: 1.2's banner_default instead of 0.16's smaller Factory picture, the location's name in capitals, a
    //   scroll bar by the list;
    // - experience: the figures at the right end, the sections' experience icon the big one;
    // - treatment: the condition icons in one column, 33 x 32;
    // - on all of them the buttons at the foot 16 higher and 2 apart.
    // FinalStatisticsScreen (1.2's statistics after a story ending) belongs to the story and is not here.
    internal static class RaidEnd10
    {
        private static readonly Color ClockColor = new Color(0.915f, 0.906f, 0.833f, 1f);
        private static readonly Color HandleColor = new Color(0.714f, 0.757f, 0.78f, 1f);

        // ---- raid over ----

        internal static void ExitStatus(SessionResultExitStatus screen)
        {
            if (!Once(screen))
            {
                return;
            }

            var root = screen.transform;
            RaidPrep10.Foot(root, "ButtonsPanel");
            if (root.Find("Warning Panel") is RectTransform warning)
            {
                warning.anchoredPosition = new Vector2(warning.anchoredPosition.x, 283.5f);
            }

            if (root.Find("Level Panel/Level Icon") is RectTransform badge)
            {
                badge.anchoredPosition = new Vector2(badge.anchoredPosition.x, 0f);
                badge.sizeDelta = new Vector2(91.6f, 91.6f);
            }

            if (root.Find("StatusLine/Time Panel/Time/Icon") is RectTransform clock && clock.GetComponent<Image>() is Image image)
            {
                image.sprite = Ui.FileSprite("icon_time");
                image.color = ClockColor;
                image.preserveAspect = true;
                var element = Element(clock.gameObject);
                element.preferredWidth = 28f;
                element.preferredHeight = 28f;
                clock.sizeDelta = new Vector2(28f, 28f);
            }
        }

        // ---- kills ----

        // 1.2's column widths, the same in the header and in the rows.
        private static readonly (string Name, float Width)[] Columns =
        {
            ("Location", 205f), ("Timestamp", 125f), ("Faction", 160f), ("Status", 325f),
        };

        internal static void KillList(SessionResultKillList screen)
        {
            if (!Once(screen))
            {
                return;
            }

            var root = screen.transform;
            RaidPrep10.Foot(root, "ScreenDefaultButtons");
            if (!(root.Find("Panel") is RectTransform panel))
            {
                return;
            }

            Ui.Place(panel, new[] { 0.5f, 0f, 0.5f, 1f, 0f, 25f, 1360f, -430f, 0.5f, 0.5f });
            if (panel.GetComponent<Image>() is Image back)
            {
                back.color = new Color(0f, 0f, 0f, 0.922f);
            }

            foreach (var row in new[] { panel.Find("UpperPanel"), panel.Find("Scroll View/Viewport/KillList/VictimTemplate") })
            {
                foreach (var (name, width) in Columns)
                {
                    if (row?.Find(name)?.GetComponent<LayoutElement>() is LayoutElement element)
                    {
                        element.minWidth = width;
                        element.preferredWidth = width;
                    }
                }
            }

            // the scroll bar inside the list's right edge, in the 12 the panel keeps free there
            if (panel.Find("Scrollbar") is RectTransform bar && panel.Find("Scroll View") is RectTransform view)
            {
                bar.SetParent(view, false);
                Ui.Place(bar, new[] { 1f, 0f, 1f, 1f, 11f, 0f, 10f, 0f, 1f, 0.5f });
            }

            if (panel.Find("NoKills") is RectTransform none)
            {
                Ui.Place(none, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, none.sizeDelta.x, none.sizeDelta.y, 0.5f, 0.5f });
                if (none.Find("Background10") == null)
                {
                    var stripes = Ui.Image(none, "Background10", new Color(1f, 1f, 1f, 0.455f), Ui.FileSprite("CHTOTO"), Image.Type.Tiled);
                    Ui.Place(stripes.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, -29f, 1357.9f, 589.8f, 0.5f, 0.5f });
                }
            }
        }

        // ---- statistics ----

        internal static void Statistics(SessionResultStatistics screen)
        {
            if (!Once(screen))
            {
                return;
            }

            var root = screen.transform;
            RaidPrep10.Foot(root, "ScreenDefaultButtons");

            // 1.2's picture: banner_default (Factory, its edges faded into the screen), the same after every location as
            // 0.16's smaller location_banner_factory was; the game's own sprite (Resources UI/Banners)
            var preview = root.Find("Preview");
            preview?.gameObject.SetActive(false);
            var banner = Ui.Image(root, "Banner10", Color.white, Resources.Load<Sprite>("UI/Banners/banner_default"));
            banner.rectTransform.SetSiblingIndex(preview != null ? preview.GetSiblingIndex() : 0);
            Ui.Place(banner.rectTransform, new[] { 0.5f, 1f, 0.5f, 1f, 0f, -70f, 550f, 300f, 0.5f, 1f });

            if (root.Find("Location Name Panel/Name")?.GetComponent<TMP_Text>() is TMP_Text name)
            {
                name.fontStyle |= FontStyles.UpperCase;
            }

            if (root.Find("Scroll View") is RectTransform view)
            {
                Ui.Place(view, new[] { 0.5f, 0f, 0.5f, 1f, 0f, 7f, 996f, -354f, 0.5f, 0.5f });
                if (view.Find("Viewport") is RectTransform viewport)
                {
                    viewport.sizeDelta = new Vector2(-7f, 0f);
                }

                ScrollBar(view);
            }
        }

        // A thin scroll bar 10 to the right of a list (1.2's: black, the handle light grey and 2 in), hidden when all fits.
        private static void ScrollBar(RectTransform view)
        {
            var scroll = view.GetComponent<ScrollRect>();
            if (scroll == null || view.Find("Scrollbar10") != null)
            {
                return;
            }

            var back = Ui.Image(view, "Scrollbar10", Color.black);
            back.raycastTarget = true;
            Ui.Place(back.rectTransform, new[] { 1f, 0f, 1f, 1f, 10f, 0f, 10f, 0f, 1f, 0.5f });
            var area = Ui.Rect(back.transform, "Sliding Area");
            Ui.Stretch(area);
            var handle = Ui.Image(area, "Handle", HandleColor);
            handle.raycastTarget = true;
            Ui.Place(handle.rectTransform, new[] { 0f, 0f, 1f, 1f, 0f, 0f, -4f, -4f, 0.5f, 0.5f });

            var bar = back.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // ---- experience ----

        internal static void Experience(SessionResultExperienceCount screen)
        {
            if (!Once(screen))
            {
                return;
            }

            RaidPrep10.Foot(screen.transform, "ScreenDefaultButtons");
            var t = Traverse.Create(screen);
            var section = t.Field<ExperienceSectionView>("_sectionTemplate").Value;
            if (section != null)
            {
                if (section.transform.Find("Header/Value")?.GetComponent<Image>() is Image icon && Ui.GameSprite("icon_experience_big") is Sprite big)
                {
                    icon.sprite = big;
                }

                Right(section.transform.Find("Header/Value/Text"));
                var article = Traverse.Create(section).Field<ExperienceArticleView>("_articleTemplate").Value;
                Right((article != null ? article.transform : section.transform.Find("Item"))?.Find("Value/Text"));
            }
        }

        private static void Right(Transform target)
        {
            if (target?.GetComponent<TMP_Text>() is TMP_Text text)
            {
                text.alignment = TextAlignmentOptions.Right;
            }
        }

        // ---- treatment ----

        private static readonly string[] Conditions =
            { "Overall Health", "Temperature", "Poisoning", "Radiation", "Blood Pressure", "Hydration", "Energy" };

        // The condition icons of a health panel in one column at 20, 1.2's 33 x 32 (radiation 32 x 32); the treatment
        // screen's and the character's (inventory, Scav inventory) are the same panel.
        internal static void ConditionIcons(Transform parameters)
        {
            foreach (var name in Conditions)
            {
                if (parameters?.Find(name + "/Icon") is RectTransform icon)
                {
                    var width = name == "Radiation" ? 32f : 33f;
                    Ui.Place(icon, new[] { 0f, 0.5f, 0f, 0.5f, 20f, 0f, width, 32f, 0.5f, 0.5f });
                }
            }
        }

        internal static void Treatment(HealthTreatmentScreen screen)
        {
            if (!Once(screen))
            {
                return;
            }

            var root = screen.transform;
            RaidPrep10.Foot(root, "ScreenDefaultButtons");
            ConditionIcons(root.Find("HealthTreatmentService/Content/PlayerConditionPanel/Health/Health Panel/Parameters"));
            if (root.Find("HealthTreatmentService/Content/HealthTreatmentPanel")?.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup panel)
            {
                var pad = panel.padding;
                panel.padding = new RectOffset(pad.left, pad.right, pad.top, 21);
            }
        }

        // ---- helpers ----

        private static bool Once(Component screen)
        {
            if (screen == null || screen.GetComponent<Restyled>() != null)
            {
                return false;
            }

            screen.gameObject.AddComponent<Restyled>();
            return true;
        }

        private static LayoutElement Element(GameObject go) => go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();

        private static void Run(string what, Action action)
        {
            if (!Interface10Plugin.RaidEnd.Value)
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

        private static MethodBase Show(Type type, int parameters) => AccessTools.GetDeclaredMethods(type)
            .First(m => m.Name == "Show" && m.GetParameters().Length == parameters);

        // ---- patches ----

        [HarmonyPatch]
        internal static class ExitStatusPatch
        {
            private static MethodBase TargetMethod() => Show(typeof(SessionResultExitStatus), 7);

            [HarmonyPostfix]
            private static void Postfix(SessionResultExitStatus __instance) => Run("Raid over screen", () => ExitStatus(__instance));
        }

        // Before the rows are made from the template.
        [HarmonyPatch]
        internal static class KillListPatch
        {
            private static MethodBase TargetMethod() => Show(typeof(SessionResultKillList), 2);

            [HarmonyPrefix]
            private static void Prefix(SessionResultKillList __instance) => Run("Kill list", () => KillList(__instance));
        }

        [HarmonyPatch]
        internal static class StatisticsPatch
        {
            private static MethodBase TargetMethod() => Show(typeof(SessionResultStatistics), 2);

            [HarmonyPostfix]
            private static void Postfix(SessionResultStatistics __instance) => Run("Raid statistics", () => Statistics(__instance));
        }

        // Before the sections are made from the template.
        [HarmonyPatch]
        internal static class ExperiencePatch
        {
            private static MethodBase TargetMethod() => Show(typeof(SessionResultExperienceCount), 3);

            [HarmonyPrefix]
            private static void Prefix(SessionResultExperienceCount __instance) => Run("Raid experience", () => Experience(__instance));
        }

        [HarmonyPatch]
        internal static class TreatmentPatch
        {
            private static MethodBase TargetMethod() => Show(typeof(HealthTreatmentScreen), 5);

            [HarmonyPostfix]
            private static void Postfix(HealthTreatmentScreen __instance) => Run("Treatment screen", () => Treatment(__instance));
        }
    }
}
