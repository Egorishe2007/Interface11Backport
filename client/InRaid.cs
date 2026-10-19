using System;
using System.Collections.Generic;
using EFT.UI;
using EFT.UI.BattleTimer;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's raid interface (GameUIScene, against 0.16's; the values in InRaidLayout, generated from the dumps):
    // - the exits list (O): the rows 572 wide (the name 430, the time 140 beside it, 2 apart), the names 18, the times
    //   24, the state over the list 24 in bold with its icon 30, the rows of events, the BTR and transits 55 high;
    // - the messages at the foot (extraction, exit trigger, transit, event): their plate at least 350 x 50, the icon
    //   40 in the row, the text in the middle with 32 at each side (the event's 48 at the left), the glow behind its
    //   middle;
    // - the PostFX screen in the raid (VISUALIZE): its panel at the top left, 20 in, 850 x 730 (Settings10 lays out its
    //   rows as on the settings screen).
    // The game's templates are restyled before it copies them, so its own code fills every copy.
    internal static class InRaid10
    {
        internal static void Apply(Transform root, Dictionary<string, float[]> rects, Dictionary<string, float[]> elements,
            Dictionary<string, float[]> groups, Dictionary<string, float[]> texts)
        {
            foreach (var entry in groups)
            {
                if (root.Find(entry.Key)?.GetComponent<HorizontalOrVerticalLayoutGroup>() is HorizontalOrVerticalLayoutGroup group)
                {
                    var v = entry.Value;
                    group.padding = new RectOffset((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
                    group.spacing = v[4];
                    group.childAlignment = (TextAnchor)(int)v[5];
                    group.childControlWidth = v[6] > 0f;
                    group.childControlHeight = v[7] > 0f;
                    group.childForceExpandWidth = v[8] > 0f;
                    group.childForceExpandHeight = v[9] > 0f;
                }
            }

            foreach (var entry in elements)
            {
                if (root.Find(entry.Key) is Transform target)
                {
                    var v = entry.Value;
                    var element = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
                    element.ignoreLayout = v[0] > 0f;
                    element.minWidth = v[1];
                    element.minHeight = v[2];
                    element.preferredWidth = v[3];
                    element.preferredHeight = v[4];
                    element.flexibleWidth = v[5];
                    element.flexibleHeight = v[6];
                }
            }

            foreach (var entry in texts)
            {
                if (root.Find(entry.Key)?.GetComponent<TMP_Text>() is TMP_Text text)
                {
                    text.fontSize = entry.Value[0];
                    text.fontStyle = (FontStyles)(int)entry.Value[1];
                }
            }

            foreach (var entry in rects)
            {
                if (root.Find(entry.Key) is RectTransform rect)
                {
                    Ui.Place(rect, entry.Value);
                }
            }
        }

        private static bool Once(Component target)
        {
            if (target == null || target.GetComponent<Restyled>() != null)
            {
                return false;
            }

            target.gameObject.AddComponent<Restyled>();
            return true;
        }

        internal static void Timers(ExtractionTimersPanel panel)
        {
            if (Once(panel))
            {
                Apply(panel.transform, InRaidLayout.TimerRects, InRaidLayout.TimerElements, InRaidLayout.TimerGroups, InRaidLayout.TimerTexts);
            }
        }

        internal static void Message(BattleUIPanel panel)
        {
            if (!Once(panel))
            {
                return;
            }

            var root = panel.transform;
            switch (panel)
            {
                case BattleUIPanelExtraction _:
                    Apply(root, InRaidLayout.ExtractionRects, InRaidLayout.ExtractionElements, InRaidLayout.ExtractionGroups, InRaidLayout.ExtractionTexts);
                    // 1.2's text is sized by the row, not by itself
                    if (root.Find("Info Panel/Text")?.GetComponent<ContentSizeFitter>() is ContentSizeFitter fitter)
                    {
                        fitter.enabled = false;
                    }

                    break;
                case BattleUIPanelExitTrigger _:
                    Apply(root, InRaidLayout.TriggerRects, InRaidLayout.TriggerElements, InRaidLayout.TriggerGroups, InRaidLayout.TriggerTexts);
                    break;
                case LocationTransitTimerPanel _:
                    Apply(root, InRaidLayout.TransitRects, InRaidLayout.TransitElements, InRaidLayout.TransitGroups, InRaidLayout.TransitTexts);
                    break;
                case EventStatePanel _:
                    Apply(root, InRaidLayout.EventRects, InRaidLayout.EventElements, InRaidLayout.EventGroups, InRaidLayout.EventTexts);
                    break;
            }
        }

        // The PostFX tab of the raid's preview screen: 1.2's place for it, at the top left.
        internal static bool PostFxPreview(Transform tab)
        {
            if (tab.GetComponentInParent<EFT.UI.Screens.PostFXPreviewScreen>(true) == null)
            {
                return false;
            }

            Ui.Place((RectTransform)tab, new[] { 0f, 0f, 1f, 1f, 0f, 0f, -800f, 0f, 0f, 1f });
            if (tab.Find("Panel") is RectTransform panel)
            {
                Ui.Place(panel, new[] { 0f, 1f, 0f, 1f, 20f, -20f, 850f, 730f, 0f, 1f });
                if (panel.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup layout)
                {
                    layout.spacing = 4f;
                }
            }

            return true;
        }

        private static void Run(string what, Action action)
        {
            if (!Interface10Plugin.RaidHud.Value)
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

        [HarmonyPatch(typeof(ExtractionTimersPanel), nameof(ExtractionTimersPanel.SetTime))]
        internal static class TimersPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ExtractionTimersPanel __instance) => Run("Raid timers", () => Timers(__instance));
        }

        [HarmonyPatch(typeof(ExtractionTimersPanel), nameof(ExtractionTimersPanel.SetTransits))]
        internal static class TransitsPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ExtractionTimersPanel __instance) => Run("Raid timers", () => Timers(__instance));
        }

        // the event's panel can show without its plate's coroutine (a past event's time)
        [HarmonyPatch(typeof(EventStatePanel), nameof(EventStatePanel.Show))]
        internal static class EventPatch
        {
            [HarmonyPrefix]
            private static void Prefix(EventStatePanel __instance) => Run("Raid message", () => Message(__instance));
        }

        [HarmonyPatch(typeof(BattleUIPanel), nameof(BattleUIPanel.Co_ShowInfoPanel))]
        internal static class MessagePatch
        {
            [HarmonyPrefix]
            private static void Prefix(BattleUIPanel __instance) => Run("Raid message", () => Message(__instance));
        }
    }
}
