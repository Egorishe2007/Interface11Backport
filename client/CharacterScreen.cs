using System;
using System.Linq;
using EFT;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Interface10
{
    // 1.0's character screen, Overall tab: REPEAT TUTORIAL at the bottom right (250 x 55.6, 25 from the right edge,
    // 80 up: over where BACK sits in 1.0's Overall Panel). It asks "Repeat tutorial?" and then starts the tutorial
    // raid of the tutorial mod with the current character, from the main menu. Only with the tutorial mod installed,
    // for the PMC.
    internal static class CharacterScreen10
    {
        [HarmonyPatch(typeof(OverallScreen), nameof(OverallScreen.Show))]
        internal static class OverallPatch
        {
            [HarmonyPostfix]
            private static void Postfix(OverallScreen __instance, Profile currentProfile)
            {
                if (!Interface10Plugin.RepeatTutorial.Value || !Tutorial.Present)
                {
                    return;
                }

                try
                {
                    var button = Button(__instance);
                    if (button != null)
                    {
                        button.gameObject.SetActive(currentProfile != null && currentProfile.Side != EPlayerSide.Savage);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Repeat tutorial button: {ex.Message}");
                }
            }
        }

        private static DefaultUIButton Button(OverallScreen overall)
        {
            var existing = overall.transform.Find("TutorialButton");
            if (existing != null)
            {
                return existing.GetComponent<DefaultUIButton>();
            }

            // a button of the game's own to copy: the screen's BACK
            var back = Traverse.Create(overall).Field<DefaultUIButton>("_backButton").Value
                       ?? overall.GetComponentInParent<InventoryScreen>()?.GetComponentsInChildren<DefaultUIButton>(true).FirstOrDefault();
            if (back == null)
            {
                return null;
            }

            var go = UnityEngine.Object.Instantiate(back.gameObject, overall.transform, false);
            go.name = "TutorialButton";
            var button = go.GetComponent<DefaultUIButton>();
            button.OnClick.RemoveAllListeners();
            for (var i = 0; i < button.OnClick.GetPersistentEventCount(); i++)
            {
                button.OnClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }

            button.SetRawText(Lang.Get("tutorial.repeat", "REPEAT TUTORIAL"), 26);
            Ui.Place((RectTransform)go.transform, new[] { 1f, 0f, 1f, 0f, -25f, 80f, 250f, 55.6f, 1f, 0f });
            button.OnClick.AddListener(() => Ask(overall));
            return button;
        }

        private static void Ask(OverallScreen overall)
        {
            if (!Tutorial.CanStartTutorial)
            {
                return;
            }

            ItemUiContext.Instance.ShowMessageWindow(Lang.Get("tutorial.repeat_question", "Repeat tutorial?"), () =>
            {
                // back to the main menu (the tutorial raid starts from there), then the raid
                var back = Traverse.Create(overall).Field<DefaultUIButton>("_backButton").Value;
                var screen = overall.GetComponentInParent<InventoryScreen>();
                var close = screen != null ? screen.GetComponentsInChildren<DefaultUIButton>(true).FirstOrDefault(b => b.name == "BackButton" && b.transform.parent == screen.transform) : null;
                (close ?? back)?.OnClick.Invoke();
                Tutorial.StartTutorial();
            }, () => { }, string.Empty, 0f, false, TMPro.TextAlignmentOptions.Center);
        }
    }
}
