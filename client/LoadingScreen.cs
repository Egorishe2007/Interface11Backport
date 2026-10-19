using System;
using System.Collections.Generic;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.0's raid loading screen (MenuUIScene, Matchmaker Time Has Come): instead of 0.16's one line "Loading map... (2%)
    // (00:09)" its MatchingProgressView under the player: the stage in capitals and its percentage over a progress bar
    // of nine segments, and below it "Loading" with the time so far, on a soft glow. Same sizes, colours and texts as
    // 1.0's (the stage texts are the game's own).
    internal class LoadingProgressView : MonoBehaviour
    {
        private const int Segments = 9;
        private static readonly Color Pale = new Color(0.7725f, 0.7647f, 0.698f);
        private static readonly Color Khaki = new Color(0.7059f, 0.7059f, 0.6039f);

        // 0.16's stages of a local raid, as segments of the bar: (first segment, segments)
        private static readonly Dictionary<string, (int First, int Count)> Stages = new Dictionary<string, (int, int)>
        {
            ["Matching..."] = (0, 1),
            ["Game world creating..."] = (0, 1),
            ["Map loading..."] = (1, 3),
            ["Caching data..."] = (4, 2),
            ["Loading objects..."] = (6, 1),
            ["Local game starting..."] = (7, 1),
            ["Preparing the game..."] = (8, 1),
        };

        // "Loading" over the time (1.0's Matchmaker/MatchingProgress/Time/Loading)
        private static readonly Dictionary<string, string> LoadingWord = new Dictionary<string, string>
        {
            ["en"] = "Loading", ["ru"] = "Загрузка", ["ge"] = "Laden", ["fr"] = "Chargement", ["es"] = "Carga", ["es-mx"] = "Carga",
            ["it"] = "Caricamento", ["pl"] = "Ładowanie", ["cz"] = "Načítání", ["sk"] = "Načítavanie", ["hu"] = "Betöltés",
            ["ro"] = "Încărcare", ["po"] = "Carregamento", ["tu"] = "Yükleme", ["ch"] = "加载", ["jp"] = "ロード", ["kr"] = "로딩",
        };

        private TextMeshProUGUI _stage;
        private TextMeshProUGUI _percent;
        private TextMeshProUGUI _time;
        private readonly List<Image> _segments = new List<Image>();
        private float _shownAt;
        private float _done;
        private int _current;

        internal static LoadingProgressView Attach(MatchmakerTimeHasCome screen)
        {
            var view = screen.GetComponentInChildren<LoadingProgressView>(true);
            if (view == null)
            {
                var root = Ui.Rect(screen.transform, "MatchingProgressView (1.0)");
                view = root.gameObject.AddComponent<LoadingProgressView>();
                view.Build(root);
            }

            view.gameObject.SetActive(true);
            view.Begin();
            return view;
        }

        private void Build(RectTransform root)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -327.5f);
            root.sizeDelta = new Vector2(0f, 200f);
            Ui.Layout<VerticalLayoutGroup>(root.gameObject, TextAnchor.MiddleCenter);
            Ui.Element(root.gameObject, 200f, 200f);
            root.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // the stage and its percentage
            var upper = Ui.Rect(root, "UpperGroup");
            var upperLayout = Ui.Layout<HorizontalLayoutGroup>(upper.gameObject, TextAnchor.UpperLeft, bottom: 3);
            upperLayout.childForceExpandWidth = true;
            var stage = Ui.Rect(upper, "StageProgress");
            Ui.Layout<VerticalLayoutGroup>(stage.gameObject, TextAnchor.UpperLeft);
            _stage = Ui.Text(stage, "StageText", 14f, Pale);
            _stage.fontStyle = FontStyles.UpperCase;
            _stage.characterSpacing = 2f;
            _stage.alignment = TextAlignmentOptions.BottomLeft;
            var stageElement = Ui.Element(_stage.gameObject, preferredWidth: 200f);
            stageElement.flexibleWidth = 1f;
            _percent = Ui.Text(stage, "ProgressValue", 14f, Khaki);
            _percent.characterSpacing = 2f;
            _percent.alignment = TextAlignmentOptions.BottomLeft;

            // the bar: nine segments in a frame
            var bar = Ui.Rect(root, "ProgressBar");
            Ui.Layout<HorizontalLayoutGroup>(bar.gameObject, TextAnchor.MiddleLeft);
            Ui.Element(bar.gameObject, 300f, 12f);
            var back = Ui.Image(bar, "Background", Color.black);
            Ui.Stretch(back.rectTransform);
            Ui.Element(back.gameObject, ignore: true);
            var cells = Ui.Rect(bar, "Segments");
            Ui.Layout<HorizontalLayoutGroup>(cells.gameObject, TextAnchor.UpperLeft, 3, 3, 3, 3, 2f);
            for (var i = 0; i < Segments; i++)
            {
                var cell = Ui.Image(cells, "Segment", Khaki);
                Ui.Element(cell.gameObject, 50f, 6f);
                _segments.Add(cell);
            }

            var border = Ui.Image(bar, "Border", Khaki, Ui.GameSprite("border_generic"), UnityEngine.UI.Image.Type.Sliced);
            border.fillCenter = false;
            border.enabled = border.sprite != null;
            Ui.Stretch(border.rectTransform);
            Ui.Element(border.gameObject, ignore: true);

            // "Loading" and the time, on a glow
            var lower = Ui.Rect(root, "LowerGroup");
            Ui.Layout<VerticalLayoutGroup>(lower.gameObject, TextAnchor.MiddleCenter, top: 20, spacing: -3f);
            var glow = Ui.Image(lower, "LoadingTimeGlow", new Color(0.9294f, 0.9216f, 0.8392f, 0.502f), Ui.FileSprite("Matchmaker_GameMods_Glow_Medium_White"));
            glow.enabled = glow.sprite != null;
            Ui.Element(glow.gameObject, ignore: true);
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            glow.rectTransform.sizeDelta = new Vector2(448.78f, 107.25f);
            _time = Ui.Text(lower, "LoadingTimeText", 14f, Pale);
            _time.fontStyle = FontStyles.UpperCase;
            _time.characterSpacing = 2f;
            _time.lineSpacing = -15f;
            _time.alignment = TextAlignmentOptions.Center;
        }

        private void Begin()
        {
            _shownAt = Time.realtimeSinceStartup;
            _done = 0f;
            _current = 0;
            _stage.text = "Game world creating...".Localized();
            _percent.gameObject.SetActive(false);
        }

        internal void SetStage(string status, float? progress)
        {
            if (string.IsNullOrEmpty(status))
            {
                return;
            }

            // a local raid's "Matching..." is 1.0's "Creating game world..."
            _stage.text = (status == "Matching..." ? "Game world creating..." : status).Localized();
            _percent.gameObject.SetActive(progress.HasValue);
            if (progress.HasValue)
            {
                _percent.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress.Value) * 100f)}%";
            }

            if (Stages.TryGetValue(status, out var stage))
            {
                // the bar only goes forward
                _done = Mathf.Max(_done, stage.First + stage.Count * Mathf.Clamp01(progress ?? 0f));
                _current = Mathf.Max(_current, Mathf.Min(Segments - 1, stage.First + (int)(stage.Count * Mathf.Clamp01(progress ?? 0f))));
            }
        }

        private void Update()
        {
            var seconds = Mathf.FloorToInt(Time.realtimeSinceStartup - _shownAt);
            _time.text = $"{Ui.Pick(LoadingWord)}\n<size=24>{seconds / 60:00}:{seconds % 60:00}</size>";
            // done segments full, the current one pulsing, the rest dim
            for (var i = 0; i < _segments.Count; i++)
            {
                var alpha = i < Mathf.FloorToInt(_done) ? 1f : i == _current ? 0.45f + 0.45f * Mathf.PingPong(Time.realtimeSinceStartup * 1.6f, 1f) : 0.1f;
                var color = Khaki;
                color.a = alpha;
                _segments[i].color = color;
            }
        }

        [HarmonyPatch(typeof(MatchmakerTimeHasCome), nameof(MatchmakerTimeHasCome.Show), typeof(MatchmakerTimeHasCome.TimeHasComeScreenController))]
        internal static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MatchmakerTimeHasCome __instance)
            {
                if (!Interface10Plugin.LoadingScreen.Value)
                {
                    return;
                }

                try
                {
                    Attach(__instance);
                    // 1.0 switches 0.16's line of text off
                    __instance.transform.Find("Loader")?.gameObject.SetActive(false);
                    var deploying = Traverse.Create(__instance).Field("_deployingText").GetValue<TextMeshProUGUI>();
                    if (deploying != null)
                    {
                        deploying.gameObject.SetActive(false);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Loading screen not rebuilt: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(MatchmakerTimeHasCome), nameof(MatchmakerTimeHasCome.ChangeStatus))]
        internal static class StatusPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MatchmakerTimeHasCome __instance, string status, float? progress)
            {
                var view = Interface10Plugin.LoadingScreen.Value ? __instance.GetComponentInChildren<LoadingProgressView>(true) : null;
                view?.SetStage(status, progress);
            }
        }
    }
}
