using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.0's location selection ("Matchmaker Location Selection" in MenuUIScene). What 1.0 changed:
    // - the location names on the map are 1.0's toggles: light grey capitals, a worn light plate with black text when
    //   chosen (the "SeparatedViewToggleButton" animation instead of 0.16's "SeparatedNLViewToggleButton", a Background
    //   plate and a SizeLabel/Label pair instead of View/Label);
    // - the bottom buttons sit 16 px higher, the weather icon and some checkmarks are resized.
    internal static class LocationScreen10
    {
        private const string ToggleAnimation = "SeparatedViewToggleButton";
        private const string LabelFont = "Jovanny Lemonad - Bender Shadowed SDF";
        private static readonly Color LabelColor = new Color(0.7647f, 0.7725f, 0.7333f, 1f);
        private static readonly Color SecretColor = new Color(0.7647f, 0.7725f, 0.698f, 1f);
        private static RuntimeAnimatorController _animation;

        // 1.0's values of what moved (ui10/ui_diff.json), by path under the screen.
        private static readonly Dictionary<string, float[]> Layout = new Dictionary<string, float[]>
        {
            ["Content/Location Info Panel/PveServerSettings/ServerSettingsCheckmark/Background/Checkmark"] =
                new[] { 0f, 0f, 1f, 1f, 0f, 0f, -6f, -6f, 0.5f, 0.5f },
            ["Content/Location Info Panel/DescriptionPanel/Bottom Panel/IconsPanel/Difficulty/Difficulty"] =
                new[] { 0f, 1f, 0f, 1f, 33.4f, -25.9f, 68.63f, 19.8f, 0f, 0f },
            ["Conditions Panel/Weather Caption"] = new[] { 0f, 0f, 0f, 0f, 418f, 20f, 74f, 49.5f, 0f, 0f },
            ["Conditions Panel/Weather"] = new[] { 0f, 0f, 0f, 0f, 492f, 19.5f, 50f, 50f, 0f, 0f },
            ["Conditions Panel/TimePhaseContainer/CurrentPhase/Background/Checkmark"] =
                new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 19f, 19f, 0.5f, 0.5f },
            ["Conditions Panel/TimePhaseContainer/NextPhase/Background/Checkmark"] =
                new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 19f, 19f, 0.5f, 0.5f },
            ["ScreenDefaultButtons"] = new[] { 0.5f, 0f, 0.5f, 0f, 0f, 56f, 0f, 0f, 0.5f, 0f },
            ["ReadyButton"] = new[] { 0.5f, 0f, 0.5f, 0f, 300f, 56f, 0f, 0f, 0.5f, 0f },
            ["MapButton"] = new[] { 0.5f, 0f, 0.5f, 0f, -300f, 56f, 0f, 0f, 0.5f, 0f },
        };

        internal static void Apply(MatchMakerSelectionLocationScreen screen)
        {
            if (screen.GetComponent<Restyled>() == null)
            {
                screen.gameObject.AddComponent<Restyled>();
                Ui.Place(screen.transform, Layout);
            }

            var t = Traverse.Create(screen);
            // The template first: buttons made from it afterwards come out restyled; then any made before.
            Restyle(t.Field<LocationButton>("_locationButtonTemplate").Value);
            var container = t.Field<Transform>("_locationButtonsContainer").Value;
            if (container != null)
            {
                foreach (var button in container.GetComponentsInChildren<LocationButton>(true))
                {
                    Restyle(button);
                }
            }
        }

        private static void Restyle(LocationButton location)
        {
            var toggle = location != null ? Traverse.Create(location).Field<UISpawnableToggle>("_spawnableToggle").Value : null;
            if (toggle == null || toggle.GetComponent<Restyled>() != null)
            {
                return;
            }

            var spawnable = Traverse.Create(toggle);
            var oldLabel = spawnable.Field<TextMeshProUGUI>("_headerLabel").Value;
            if (oldLabel == null)
            {
                Interface10Plugin.Log.LogWarning("Location button without a label, left as it is");
                return;
            }

            var go = toggle.gameObject;
            go.AddComponent<Restyled>();
            var rect = (RectTransform)go.transform;

            // Button Panel and the toggle in it, as placed in 1.0.
            if (rect.parent is RectTransform panel)
            {
                Ui.Place(panel, new[] { 0f, 0.5f, 0f, 0.5f, -12.5f, 0f, 188f, 33f, 0f, 0.5f });
            }

            Ui.Place(rect, new[] { 0f, 0.5f, 0f, 0.5f, 14f, 0f, 164.04f, 28.61f, 0f, 0.5f });
            var layout = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The plate: hidden, shown by the toggle's animation on hover and when chosen. 16 px wider on each side.
            var plate = Ui.Image(go.transform, "Background", new Color(1f, 1f, 1f, 0f), Buttons10.Worn, Image.Type.Sliced);
            plate.pixelsPerUnitMultiplier = 3f;
            Stretch(plate.rectTransform, 16f, 0f);
            Ui.Element(plate.gameObject, ignore: true);
            plate.transform.SetAsFirstSibling();

            // SizeLabel (bold, invisible) sizes the toggle, Label over it is what shows.
            var font = Ui.Font(LabelFont);
            var size = Copy(oldLabel, go.transform, "SizeLabel", font);
            size.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            size.color = new Color(0f, 0f, 0f, 0f);
            var label = Copy(oldLabel, size.transform, "Label", font);
            label.fontStyle = FontStyles.UpperCase;
            label.color = LabelColor;
            Stretch(label.rectTransform, 0f, 6f);
            spawnable.Field("_headerLabel").SetValue(label);
            spawnable.Field("_sizeLabel").SetValue(size);

            var view = go.transform.Find("View");
            if (view != null)
            {
                view.gameObject.SetActive(false);
            }

            // The dotted border of secret locations, 16 px out on each side like the plate.
            var secret = Traverse.Create(location).Field<RectTransform>("_secretBorder").Value;
            if (secret != null)
            {
                Stretch(secret, 16f, 0f);
                if (secret.GetComponent<Image>() is Image ants)
                {
                    ants.color = SecretColor;
                }

                secret.SetAsLastSibling();
            }

            var animator = go.GetComponent<Animator>();
            _animation = _animation != null ? _animation
                : Resources.FindObjectsOfTypeAll<RuntimeAnimatorController>().FirstOrDefault(c => c.name == ToggleAnimation);
            if (animator != null && _animation != null)
            {
                animator.runtimeAnimatorController = _animation;
                animator.Rebind();
                if (toggle.Toggle != null && go.activeInHierarchy)
                {
                    toggle.Toggle.TriggerAnimation(toggle.Toggle.isOn);
                }
            }
            else
            {
                Interface10Plugin.Log.LogWarning($"No {ToggleAnimation} animation for the location names");
            }
        }

        // A copy of the game's label (its font and material), without children, for 1.0's text of the given size.
        private static TextMeshProUGUI Copy(TextMeshProUGUI source, Transform parent, string name, TMP_FontAsset font)
        {
            var go = Object.Instantiate(source.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            for (var i = go.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            }

            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = 26f;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
            text.text = source.text;
            return text;
        }

        // Stretched over the parent, the given number of pixels out on each side.
        private static void Stretch(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-x, -y);
            rect.offsetMax = new Vector2(x, y);
        }
    }

    [HarmonyPatch(typeof(MatchMakerSelectionLocationScreen), nameof(MatchMakerSelectionLocationScreen.ShowInternal))]
    internal static class LocationScreenPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MatchMakerSelectionLocationScreen __instance)
        {
            if (!Interface10Plugin.LocationScreen.Value)
            {
                return;
            }

            try
            {
                LocationScreen10.Apply(__instance);
            }
            catch (System.Exception ex)
            {
                Interface10Plugin.Log.LogError($"Location selection: {ex}");
            }
        }
    }
}
