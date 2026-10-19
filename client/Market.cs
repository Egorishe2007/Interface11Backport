using System;
using System.Linq;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's flea market and trading screen (MenuUIScene's Ragfair Screen and Trading Screen, the filter window moved by
    // 1.2 into PreloaderUIScene's WindowsPlaceholder, against 0.16's):
    // - the filter window: its caption 20 high with the title in the middle, 1.2's drop-downs (as the raid settings'),
    //   the big check marks 14 x 14 in the middle of their boxes, the rouble in the warm grey of the figures;
    // - the trading screen's TRADERS / FLEA MARKET toggles: the worn plate of 1.2's buttons under their names;
    // - the money over the offers: the currency icons 20 x 20, 2 from their figures.
    // The rest of the flea market is as 0.16 (the offers, the categories, the windows' contents), or live only (the
    // level shown on ADD OFFER, the money windows' loading spinners).
    internal static class Market10
    {
        private static readonly Color Rouble = new Color(0.772f, 0.765f, 0.698f, 1f);

        internal static void FilterWindow(RagfairFilterWindow window)
        {
            if (!Once(window))
            {
                return;
            }

            var root = window.transform;
            if (root.Find("Inner/Caption Panel") is RectTransform caption)
            {
                Windows10.Middle(caption);
            }

            foreach (var box in root.GetComponentsInChildren<DropDownBox>(true))
            {
                RaidPrep10.Dropdown(box);
            }

            foreach (var mark in root.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "Checkmark" && t.parent != null && t.parent.name == "Background"))
            {
                if (mark.GetComponent<Image>() is Image check && Ui.FileSprite("checkmark_big") is Sprite big)
                {
                    check.sprite = big;
                    check.preserveAspect = true;
                    Ui.Place(mark, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 14f, 14f, 0.5f, 0.5f });
                }
            }

            if (root.Find("Inner/Filters/InputBased/EstimatedPrice/To/CurrencyIcon")?.GetComponent<Image>() is Image rouble)
            {
                rouble.color = Rouble;
                rouble.preserveAspect = true;
            }
        }

        internal static void TradingToggles(TradingScreen screen)
        {
            if (!Once(screen) || Buttons10.Worn is not Sprite worn)
            {
                return;
            }

            foreach (var toggle in screen.GetComponentsInChildren<AnimatedToggle>(true))
            {
                if (toggle.transform.Find("Background") is RectTransform plate && plate.GetComponent<Image>() is Image image)
                {
                    image.sprite = worn;
                    image.type = Image.Type.Sliced;
                    image.fillCenter = true;
                    image.pixelsPerUnitMultiplier = 1.3f;
                    plate.sizeDelta = Vector2.zero;
                }
            }
        }

        internal static void Money(RagfairScreen screen)
        {
            if (!(screen.transform.Find("TopRightPanel/MoneyCount") is Transform money) || !Once(money))
            {
                return;
            }

            foreach (Transform currency in money)
            {
                if (currency.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
                {
                    row.spacing = 2f;
                }

                if (currency.Find("Image")?.GetComponent<LayoutElement>() is LayoutElement icon)
                {
                    icon.preferredWidth = icon.preferredHeight = 20f;
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

        private static void Run(string what, Action action)
        {
            if (!Interface10Plugin.Market.Value)
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

        [HarmonyPatch(typeof(RagfairFilterWindow), nameof(RagfairFilterWindow.Show))]
        internal static class FilterPatch
        {
            [HarmonyPostfix]
            private static void Postfix(RagfairFilterWindow __instance) => Run("Flea market filters", () => FilterWindow(__instance));
        }

        [HarmonyPatch(typeof(RagfairScreen), nameof(RagfairScreen.Show))]
        internal static class RagfairPatch
        {
            [HarmonyPostfix]
            private static void Postfix(RagfairScreen __instance) => Run("Flea market money", () => Money(__instance));
        }

        [HarmonyPatch]
        internal static class TradingPatch
        {
            private static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(TradingScreen))
                .First(m => m.Name == nameof(TradingScreen.Show) && m.GetParameters().Length == 1);

            [HarmonyPostfix]
            private static void Postfix(TradingScreen __instance) => Run("Trading screen", () => TradingToggles(__instance));
        }
    }
}
