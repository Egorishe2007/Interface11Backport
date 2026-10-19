using System;
using System.Collections.Generic;
using EFT;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's tooltips on the settings (each tab's Overlay Layer/SettingsTooltip, against 0.16, which has none): a dark
    // box 340 wide over the pointer, a grey frame and its shadow, 25 in from its edges and 25 between its parts:
    // - a note in italics when the setting cannot be changed now (V-sync under NVIDIA Reflex, the DLSS preset with
    //   DLSS off, Resampling under DLSS or FSR);
    // - what the setting does, grey;
    // - how it weighs on the computer: the CPU, GPU and RAM chips and a line, green when it helps, orange when it costs
    //   a little or moderately, red when it costs a lot.
    // The texts are 1.2's own "Ui/Settings/Hint/*" (in its client's TestBackendLocale, which SPT's locales lack: ui10
    // "settings.hint.*", "settings.usage.*", "settings.note.*" in the 17 languages); which chips and line go with which
    // setting comes from the live game's screens, so the settings never seen there have the text alone. Rows are found
    // by their names' localization keys.
    internal static class SettingsTips10
    {
        // 1.2's Ui/Settings/Hint/Usage/* keys
        internal enum Usage { None, LowIncrease, LowDecrease, AvgIncrease, AvgDecrease, HighIncrease, HighDecrease, AvgDecreaseFov, HighDecreaseResampling, LowIncreaseFramerateLimit }

        [Flags]
        internal enum Parts { None = 0, Cpu = 1, Gpu = 2, Ram = 4 }

        internal sealed class Tip
        {
            internal string Hint;
            internal string English;
            internal Parts Parts;
            internal Usage Usage;
            // the note when the row is blocked, if 1.2 has its own words for it
            internal Func<string> Note;
        }

        private static readonly Func<string> ReflexOn = () => "NVidia Reflex is on. Turn it off to change this parameter.".Localized();
        private static readonly Func<string> DlssOff = () => Lang.Get("settings.note.NVidiaDlssIsOff", "NVIDIA DLSS is disabled");
        private static readonly Func<string> UpscalerOn = () => Lang.Get("settings.note.ResamplingTitle", "Unavailable when DLSS, FSR 2.2, or FSR 3.0 is enabled.");

        private static Tip T(string hint, Parts parts, Usage usage, string english, Func<string> note = null) =>
            new Tip { Hint = hint, Parts = parts, Usage = usage, English = english, Note = note };

        // by the localization key of the setting's name (0.16 names some rows made in code by their setting's key)
        private static readonly Dictionary<string, Tip> Tips = new Dictionary<string, Tip>();

        static SettingsTips10()
        {
            void Add(Tip tip, params string[] keys)
            {
                foreach (var key in keys)
                {
                    Tips[key] = tip;
                }
            }

            // Game
            Add(T("HeadBobbing", Parts.Cpu, Usage.LowDecrease, "Enhances immersion, but high values may cause motion sickness."), "Headbobbing:");
            Add(T("Fov", Parts.Cpu | Parts.Gpu, Usage.AvgDecreaseFov, "Shows how wide the visible game world is. Higher values provide a wider field of view."), "FOV:");
            Add(T("ClearRam", Parts.Ram, Usage.LowIncrease, "Enables forced memory cleanup between raids to improve performance. Recommended for players with less than 16 GB RAM."), "Clear memory");
            Add(T("SetAffinity", Parts.Cpu, Usage.LowDecrease, "Disables the use of virtual CPU cores."), "SetAffinityToLogicalCores");
            Add(T("MalfunctionVisibility", Parts.None, Usage.None, "Displays notifications about weapon malfunctions in the corner of the screen."), "Ui/Settings/MalfunctionVisability");
            Add(T("PreloadHideout", Parts.Cpu | Parts.Ram, Usage.LowDecrease, "Increases resource usage in the main menu but speeds up Hideout loading."), "Ui/Settings/EnableHideoutPreload");
            Add(T("TraderIntermediateScreen", Parts.None, Usage.None, "Shows a separate trader selection screen when opening the Trading tab."), "Ui/Settings/TraderIntermadiateScreen");

            // Graphics
            Add(T("VSync", Parts.Gpu, Usage.AvgDecrease, "Limits FPS to the monitor refresh rate to reduce screen tearing.", ReflexOn), "VSync");
            Add(T("Antialiasing", Parts.Gpu, Usage.AvgDecrease, "FXAA — Low performance impact, but may blur the image;\n\nTAA — A modern anti-aliasing method with minimal performance impact and less blurring. May cause flickering and object ghosting."), "Antialiasing:");
            Add(T("VolumeLight", Parts.Cpu | Parts.Gpu, Usage.HighDecrease, "Provides realistic light beams and shadow scattering. Can heavily load the GPU."), "Settings/Graphics/VolumetricLight", "VolumetricLight");
            Add(T("NvidiaDlss", Parts.Gpu, Usage.HighIncrease, "A technology that improves performance and overall image quality. Effective at reconstructing small details. Supported only on RTX graphics cards and may slightly blur the image on high-resolution displays."), "Settings/Graphics/DLSSMode", "NVIDIA DLSS:");
            Add(T("DlssPreset", Parts.Gpu, Usage.HighIncrease, "F — Default preset for Max Performance and DLAA modes.\n\nJ — Improves DLSS upscaling and image detail. Affects Performance and Quality modes and delivers better results compared to other CCN models.\n\nK — Enhanced version of preset J that reduces grass flickering and ghosting artifacts.\n\nL — Sharper image with a focus on detail. Gives a crisp picture in Quality and Performance modes, but in some scenes may increase the flickering of fine details.\n\nM — Prioritizes image stability. Gives a softer and steadier picture than L.", DlssOff), "Settings/Graphics/DLSSPreset");
            Add(T("AmdFsr22", Parts.Gpu, Usage.HighIncrease, "An image upscaling technology that improves performance and visual quality using temporal upscaling. Works on most graphics cards without additional hardware requirements."), "Settings/Graphics/FSR2Mode", "AMD FSR 2.1:", "AMD FSR 2.2:");
            Add(T("AmdFsr3", Parts.Gpu, Usage.HighIncrease, "An image upscaling technology that additionally uses Frame Generation to increase FPS by 1.5 to 2 times. May slightly increase input lag."), "Settings/Graphics/FSR3Mode", "AMD FSR 3.0:");
            Add(T("Resampling", Parts.Gpu, Usage.HighDecreaseResampling, "Changes the internal rendering resolution without altering the game window size.\nPerformance impact depends on the selected value.", UpscalerOn), "Resampling:", "Resampling");
            Add(T("Hbao", Parts.Gpu, Usage.HighDecrease, "Adds soft ambient shadows for greater depth and improved spatial perception."), "HBAO:");
            Add(T("Ssr", Parts.Gpu, Usage.HighDecrease, "Adds reflections only for objects currently visible on screen. More resource-efficient than Ray Tracing but may introduce visual artifacts."), "SSR:");
            Add(T("AnisoFilter", Parts.Gpu, Usage.LowDecrease, "Improves the quality of textures viewed at sharp angles."), "Anisotropic Filtering:");
            Add(T("NvidiaReflexLowLatency", Parts.None, Usage.None, "Reduces system latency between input actions and on-screen response. Useful when you need fastest reaction time."), "Ui/Settings/NVidiaReflexType");
            Add(T("FramerateLimit", Parts.Gpu, Usage.LowIncreaseFramerateLimit, "When enabled, the frame rate limit caps maximum FPS, reduces hardware load, prevents overheating, and ensures stable frame pacing.\n\nWhen disabled, the game renders the maximum possible FPS, improving smoothness and reducing input lag. This increases GPU and CPU load and may cause FPS fluctuations on low-end systems.", ReflexOn), "DisableGameFramerateLimit");
            Add(T("HqColor", Parts.Gpu, Usage.LowDecrease, "Improves color accuracy and smoothness for a more realistic image. May increase GPU load and require compatible monitors."), "HighQualityColor");
            Add(T("ZBlur", Parts.Gpu, Usage.AvgDecrease, "Adds depth-of-field blur to objects out of focus, enhancing visual immersion."), "Z-Blur");
            Add(T("ChromaticAberrations", Parts.Gpu, Usage.LowDecrease, "Adds color fringing at object edges for a cinematic effect. May reduce image clarity and cause eye strain."), "Chromatic aberrations");
            Add(T("Noise", Parts.Gpu, Usage.LowDecrease, "Adds film grain to the image."), "Noise");
            // 1.2 has these texts, but its list doesn't show the rows: no chips known
            Add(T("GrassShadow", Parts.None, Usage.None, "Adds realistic shadows to grass, increasing scene depth and detail."), "GrassShadow");
            Add(T("MipStreaming", Parts.None, Usage.None, "Loads textures dynamically on demand to save video memory. May temporarily reduce detail on distant objects. Recommended for graphics cards with less than 8 GB of VRAM."), "Mip Streaming");
            Add(T("OptimizeTarkovStreets", Parts.None, Usage.None, "Reduces system memory usage."), "SD Tarkov Streets");

            // PostFX
            Add(T("EnablePostFx", Parts.None, Usage.None, "Post-processing effects applied after the main rendering pass. Used to enhance visuals, atmosphere, or scene realism."), "Enable PostFX");
            Add(T("LumaSharpen", Parts.Gpu, Usage.LowDecrease, "A filter that increases the clarity of light and dark details without affecting colors. Makes the image more contrast-rich. High values may introduce grain and noise."), "Luma sharpen:");
            Add(T("AdaptiveSharpen", Parts.None, Usage.None, "A smart sharpening filter that enhances clarity only where needed. Used to counter slight blur from DLSS or FSR and improve detail without excessive artifacts."), "Adaprive sharpen:");
            Add(T("ColorBlind", Parts.None, Usage.None, "Adjusts environment colors to ensure important gameplay elements remain distinguishable for players with color vision deficiencies. Doesn't impact performance."), "Colorblind mode:");

            // Sound, Controls
            Add(T("EnableVoip", Parts.None, Usage.None, "In-game proximity voice chat."), "Voip/Enable");
            Add(T("InvertX", Parts.None, Usage.None, "Inverts horizontal camera movement. Moving the mouse right turns the view left and vice versa."), "Inverted X axis");
            Add(T("InvertY", Parts.None, Usage.None, "Inverts vertical camera movement. Moving the mouse backward looks up, moving it forward looks down."), "Inverted Y axis");
        }

        // 1.2's key (after Ui/Settings/Hint/Usage/) and English
        private static readonly Dictionary<Usage, (string Key, string English)> UsageTexts = new Dictionary<Usage, (string, string)>
        {
            [Usage.LowIncrease] = ("LowIncrease", "Slightly increases performance"),
            [Usage.LowDecrease] = ("LowDecrease", "Slightly reduces performance"),
            [Usage.AvgIncrease] = ("AvgIncrease", "Moderately increases performance"),
            [Usage.AvgDecrease] = ("AvgDecrease", "Moderately reduces performance"),
            [Usage.HighIncrease] = ("HighIncrease", "Significantly increases performance"),
            [Usage.HighDecrease] = ("HighDecrease", "Significantly reduces performance"),
            [Usage.AvgDecreaseFov] = ("AvgDecrease-Fov", "Slightly reduces performance (at higher FOV values)"),
            [Usage.HighDecreaseResampling] = ("HIGH_DECREASE_Resampling", "Significantly reduces performance (at 2x or higher values)"),
            [Usage.LowIncreaseFramerateLimit] = ("LowIncrease-FramerateLimit", "Slightly increases performance but may reduce stability"),
        };

        private static readonly AccessTools.FieldRef<LocalizedText, string> KeyOf = AccessTools.FieldRefAccess<LocalizedText, string>("localizationKey");

        // the live game's colours of the line and its chips
        internal static readonly Color Up = new Color(0.4392f, 0.6902f, 0.2078f, 1f);
        internal static readonly Color Down = new Color(0.7922f, 0.5412f, 0f, 1f);
        internal static readonly Color StrongDown = new Color(0.8314f, 0.1608f, 0.1608f, 1f);

        internal static Tip Find(string key) => key != null && Tips.TryGetValue(key, out var tip) ? tip : null;

        internal static string Text(Tip tip) => Lang.Get("settings.hint." + tip.Hint, tip.English);

        internal static string UsageText(Usage usage) =>
            usage == Usage.None ? null : Lang.Get("settings.usage." + UsageTexts[usage].Key, UsageTexts[usage].English);

        internal static Color ColorOf(Usage usage)
        {
            switch (usage)
            {
                case Usage.LowIncrease:
                case Usage.AvgIncrease:
                case Usage.HighIncrease:
                case Usage.LowIncreaseFramerateLimit:
                    return Up;
                case Usage.HighDecrease:
                case Usage.HighDecreaseResampling:
                    return StrongDown;
                default:
                    return Down;
            }
        }

        // The rows of a tab with a known tooltip: a hover area over each (once).
        internal static void Attach(Transform tab)
        {
            foreach (var name in tab.GetComponentsInChildren<LocalizedText>(true))
            {
                string key;
                try
                {
                    key = KeyOf(name);
                }
                catch (Exception)
                {
                    return;
                }

                var tip = Find(key);
                var row = name.transform.parent as RectTransform;
                if (tip == null || row == null || row.GetComponent<SettingsTip10>() != null)
                {
                    continue;
                }

                var area = row.gameObject.AddComponent<SettingsTip10>();
                area.Tip = tip;
                area.Row = row;
                // the whole row answers the pointer, behind its own controls
                var catcher = Ui.Image(row, "Hover10", new Color(0f, 0f, 0f, 0f));
                Ui.Stretch(catcher.rectTransform);
                catcher.transform.SetSiblingIndex(0);
                Ui.Element(catcher.gameObject).ignoreLayout = true;
            }
        }
    }

    // 0.16's own tooltips on the settings (HoverTooltipArea, a small SimpleTooltip): 1.2 shows them in its box. Over a
    // row with a 1.2 text the game's message (a blocker's: why the setting can't be changed) is its note in italics;
    // elsewhere it is the text.
    [HarmonyPatch(typeof(HoverTooltipArea), nameof(HoverTooltipArea.Show))]
    internal static class GameTooltipPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HoverTooltipArea __instance)
        {
            if (!Interface10Plugin.Settings.Value || __instance.GetComponentInParent<EFT.UI.Settings.SettingsTab>() == null)
            {
                return true;
            }

            try
            {
                var message = __instance.Message;
                // a blocker's message is a note in italics, as 1.2's "*Unavailable with DLSS on"; a row's own is its text
                var name = __instance.name;
                var blocker = name.IndexOf("Bloker", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Blocker", StringComparison.OrdinalIgnoreCase) >= 0;
                var tip = __instance.GetComponentInChildren<SettingsTip10>(true) ?? __instance.GetComponentInParent<SettingsTip10>();
                if (tip != null)
                {
                    // 0.16's own words on a row with a 1.2 text (DLSS, FSR) give way to it
                    tip.Blocked = blocker ? message : null;
                    tip.ShowNow();
                    return false;
                }

                if (string.IsNullOrEmpty(message))
                {
                    return true;
                }

                SettingsTipView10.Show(__instance, blocker ? message : null, blocker ? null : message, SettingsTips10.Usage.None, SettingsTips10.Parts.None);
                return false;
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"1.2 settings tooltip: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(HoverTooltipArea), "UnityEngine.EventSystems.IPointerExitHandler.OnPointerExit")]
    internal static class GameTooltipExitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HoverTooltipArea __instance)
        {
            SettingsTipView10.Hide(__instance);
            if ((__instance.GetComponentInChildren<SettingsTip10>(true) ?? __instance.GetComponentInParent<SettingsTip10>()) is SettingsTip10 tip)
            {
                tip.Blocked = null;
            }
        }
    }

    // The controls tab is left as 0.16 laid it out, but 1.2 has texts for its inverted axes.
    [HarmonyPatch(typeof(EFT.UI.Settings.ControlSettingsTab), nameof(EFT.UI.Settings.ControlSettingsTab.Show))]
    internal static class ControlsTipsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EFT.UI.Settings.ControlSettingsTab __instance)
        {
            if (Interface10Plugin.Settings.Value)
            {
                SettingsTips10.Attach(__instance.transform);
            }
        }
    }

    // A settings row with a tooltip: shown a moment after the pointer comes over the row, gone when it leaves.
    internal class SettingsTip10 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float Delay = 0.25f;
        internal SettingsTips10.Tip Tip;
        internal RectTransform Row;
        // what a blocker over the row says (0.16's tooltip of a setting that can't be changed now)
        internal string Blocked;
        private float _since = -1f;

        public void OnPointerEnter(PointerEventData eventData) => _since = Time.unscaledTime;

        public void OnPointerExit(PointerEventData eventData)
        {
            _since = -1f;
            SettingsTipView10.Hide(this);
        }

        private void OnDisable()
        {
            _since = -1f;
            SettingsTipView10.Hide(this);
        }

        private void Update()
        {
            if (_since >= 0f && Time.unscaledTime - _since >= Delay)
            {
                _since = -1f;
                SettingsTipView10.Show(this, Note(), SettingsTips10.Text(Tip), Tip.Usage, Tip.Parts);
            }
        }

        internal void ShowNow()
        {
            _since = -1f;
            SettingsTipView10.Show(this, Note(), SettingsTips10.Text(Tip), Tip.Usage, Tip.Parts);
        }

        // Why the row can't be changed now, in italics over the text: 1.2's words where it has them (V-sync under NVIDIA
        // Reflex, the DLSS preset with DLSS off, Resampling under an upscaler), else 0.16's blocker's.
        private string Note()
        {
            var blocked = !string.IsNullOrEmpty(Blocked);
            if (!blocked)
            {
                var control = (Selectable)Row.GetComponentInChildren<Toggle>(true) ?? Row.GetComponentInChildren<Slider>(true);
                blocked = control != null && !control.IsInteractable();
                foreach (var group in Row.GetComponentsInParent<CanvasGroup>(true))
                {
                    blocked |= !group.interactable;
                }
            }

            if (!blocked)
            {
                return null;
            }

            return Tip.Note != null ? Tip.Note() : Blocked;
        }
    }

    // The tooltip box, one per screen canvas, over everything on it; it never takes the pointer itself.
    internal class SettingsTipView10 : MonoBehaviour
    {
        private static readonly Color Back = new Color(0.0196f, 0.0196f, 0.0235f, 1f);
        private static readonly Color Frame = new Color(0.1922f, 0.2196f, 0.2314f, 1f);
        private static readonly Color NoteColor = new Color(0.7725f, 0.7647f, 0.698f, 1f);
        private static readonly Color TextColor = new Color(0.5843f, 0.6196f, 0.6392f, 1f);
        private static readonly Dictionary<Canvas, SettingsTipView10> Views = new Dictionary<Canvas, SettingsTipView10>();

        private RectTransform _rect;
        private Canvas _canvas;
        private TextMeshProUGUI _note;
        private TextMeshProUGUI _text;
        private RectTransform _usage;
        private Image _cpu;
        private Image _gpu;
        private Image _ram;
        private TextMeshProUGUI _usageText;
        private Component _owner;

        internal static void Show(Component owner, string note, string text, SettingsTips10.Usage usage, SettingsTips10.Parts parts)
        {
            var canvas = owner.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null)
            {
                return;
            }

            if (!Views.TryGetValue(canvas, out var view) || view == null)
            {
                view = Build(canvas);
                Views[canvas] = view;
            }

            view.Fill(owner, note, text, usage, parts);
        }

        internal static void Hide(Component owner)
        {
            foreach (var view in Views.Values)
            {
                if (view != null && view._owner == owner)
                {
                    view._owner = null;
                    view.gameObject.SetActive(false);
                }
            }
        }

        private static SettingsTipView10 Build(Canvas canvas)
        {
            var rect = Ui.Rect(canvas.transform, "SettingsTooltip10");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(340f, 60f);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var background = rect.gameObject.AddComponent<Image>();
            background.color = Back;
            background.raycastTarget = false;
            var column = Ui.Layout<VerticalLayoutGroup>(rect.gameObject, TextAnchor.UpperLeft, 25, 25, 25, 25, 25f);
            column.childForceExpandWidth = true;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var shadow = Ui.Image(rect, "Border Shadow", new Color(1f, 1f, 1f, 0.3412f), Ui.FileSprite("border_shadow", new Vector4(5f, 5f, 5f, 5f)), Image.Type.Sliced);
            shadow.fillCenter = false;
            Ui.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-4f, -4f);
            shadow.rectTransform.offsetMax = new Vector2(4f, 4f);
            var border = Ui.Image(rect, "Border", Frame, Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f)), Image.Type.Sliced);
            border.fillCenter = false;
            Ui.Stretch(border.rectTransform);
            foreach (var part in new Image[] { shadow, border })
            {
                part.raycastTarget = false;
                Ui.Element(part.gameObject).ignoreLayout = true;
            }

            var view = rect.gameObject.AddComponent<SettingsTipView10>();
            view._rect = rect;
            view._canvas = canvas;
            view._note = Text(rect, "Title", NoteColor);
            view._note.fontStyle = FontStyles.Italic;
            view._text = Text(rect, "Text", TextColor);

            var usage = Ui.Rect(rect, "UsageRoot");
            Ui.Layout<HorizontalLayoutGroup>(usage.gameObject, TextAnchor.MiddleLeft, 0, 0, 0, 0, 5f);
            view._usage = usage;
            view._cpu = Chip(usage, "Cpu", "Property_CPU");
            view._gpu = Chip(usage, "Gpu", "Property_GPU");
            view._ram = Chip(usage, "Ram", "Property_RAM");
            view._usageText = Text(usage, "UsageText", Down);
            view._usageText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            rect.gameObject.SetActive(false);
            return view;
        }

        private static readonly Color Down = SettingsTips10.Down;

        private static TextMeshProUGUI Text(Transform parent, string name, Color color)
        {
            var text = Ui.Text(parent, name, 14f, color);
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static Image Chip(Transform parent, string name, string sprite)
        {
            var image = Ui.Image(parent, name, Color.white, Ui.FileSprite(sprite));
            image.preserveAspect = true;
            image.raycastTarget = false;
            var size = Ui.Element(image.gameObject, 32f, 32f, 32f, 32f);
            size.flexibleWidth = 0f;
            return image;
        }

        private void Fill(Component owner, string note, string text, SettingsTips10.Usage usage, SettingsTips10.Parts parts)
        {
            _owner = owner;
            _note.gameObject.SetActive(!string.IsNullOrEmpty(note));
            _note.text = note ?? string.Empty;
            _text.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _text.text = text ?? string.Empty;
            var line = SettingsTips10.UsageText(usage);
            _usage.gameObject.SetActive(line != null);
            if (line != null)
            {
                var color = SettingsTips10.ColorOf(usage);
                _usageText.text = line;
                _usageText.color = color;
                foreach (var (chip, part) in new[] { (_cpu, SettingsTips10.Parts.Cpu), (_gpu, SettingsTips10.Parts.Gpu), (_ram, SettingsTips10.Parts.Ram) })
                {
                    chip.gameObject.SetActive((parts & part) != 0);
                    chip.color = color;
                }
            }

            _rect.SetAsLastSibling();
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            Follow();
        }

        private void LateUpdate()
        {
            if (_owner == null || !_owner.gameObject.activeInHierarchy)
            {
                _owner = null;
                gameObject.SetActive(false);
                return;
            }

            Follow();
        }

        // Over the pointer, its lower left corner a little off it; kept on the screen.
        private void Follow()
        {
            var parent = (RectTransform)_rect.parent;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, camera, out var local))
            {
                return;
            }

            var area = parent.rect;
            var size = _rect.rect.size;
            var at = local + new Vector2(10f, 10f);
            if (at.x + size.x > area.xMax)
            {
                at.x = local.x - 10f - size.x;
            }

            if (at.y + size.y > area.yMax)
            {
                at.y = local.y - 10f - size.y;
            }

            // the anchors are the parent's middle: from its pivot to there
            _rect.anchoredPosition = at - new Vector2((0.5f - parent.pivot.x) * area.width, (0.5f - parent.pivot.y) * area.height);
        }
    }
}
