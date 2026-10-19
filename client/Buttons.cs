using System.Collections.Generic;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.0's buttons: the plate that comes up under the text on hover and press is a worn stroke of light paint instead of
    // 0.16's even plate. 1.0 draws its "Button" plate under a soft mask of "EFT_UI_Buttons_Mask" (tiled, 1.3 pixels per
    // unit); here the two are baked into one sprite (ui/Button_Worn.png) set on the plate image the button animation
    // already fades and slides, so it looks the same without a mask per button. Some buttons in 0.16 have a plate of
    // their own (the main menu's "button_back_big", most use "button_idle"); 1.0 gave them all the same worn one.
    internal static class Buttons10
    {
        // EFT_UI_Buttons_Mask's 9-slice borders: left, bottom, right, top.
        private static readonly Vector4 WornBorder = new Vector4(124f, 23f, 124f, 18f);
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static readonly Color NormalLabel = new Color(0.9059f, 0.898f, 0.8314f, 1f);

        internal static Sprite Worn => Ui.FileSprite("Button_Worn", WornBorder);

        internal static void Restyle(DefaultUIButton button)
        {
            if (button == null || button.GetComponent<Restyled>() != null)
            {
                return;
            }

            var animation = button.GetComponent<DefaultUIButtonAnimation>();
            var plate = animation != null ? animation.Image : null;
            var worn = Worn;
            if (plate == null || worn == null)
            {
                return;
            }

            button.gameObject.AddComponent<Restyled>();
            var was = plate.sprite != null ? plate.sprite.name : "none";
            if (Logged.Add(was))
            {
                Interface10Plugin.Log.LogInfo($"1.0 button plate over '{was}' (first on {button.name})");
            }

            // 1.0's resting state for every button (its DefaultUIButton prefab): light text, no plate. 0.16 gives
            // ESCAPE FROM TARKOV a grey plate even at rest.
            animation._backgorundNormalStateAplha = 0f;
            animation._normalLabelColor = NormalLabel;

            // 0.16's big plate (ESCAPE FROM TARKOV's "button_back_big", a glow with clear margins) reaches 20 out of the
            // button at the sides and 26 over and under it; 1.2's plate is the button's own rect
            if (was == "button_back_big" && plate.rectTransform.anchorMin == Vector2.zero && plate.rectTransform.anchorMax == Vector2.one)
            {
                plate.rectTransform.sizeDelta = Vector2.zero;
            }

            plate.sprite = worn;
            // Stretched, not tiled as 1.0's mask is: the plate's top-to-bottom shading is baked in and would repeat.
            plate.type = Image.Type.Sliced;
            plate.fillCenter = true;
            plate.pixelsPerUnitMultiplier = 1.3f;
        }
    }

    // Marks an element already given its 1.0 look.
    internal class Restyled : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(DefaultUIButton), "Awake")]
    internal static class ButtonAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(DefaultUIButton __instance)
        {
            if (Interface10Plugin.Buttons.Value)
            {
                Buttons10.Restyle(__instance);
            }
        }
    }
}
