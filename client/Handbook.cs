using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.0's Handbook for the tutorial mod's notes and tapes:
    // - a note or tape is listed only once the character has found it (1.0's "completable" items);
    // - a tape's description ends with its transcript, as in 1.0 ("Tapes/transcript");
    // - a tape plays under its model: 1.0's RecorderPanel (PLAY, PAUSE, STOP, the time played and the length), with
    //   the recorder in a special slot or the Intelligence Center built, else 1.0's message that it needs one;
    // - a note has 1.0's read button (ReadNoteButton) next to the item's other buttons, opening the note reader.
    internal static class Handbook10
    {
        private static Handbook _handbook;
        private static int _builtFor = -1;

        internal static Profile CurrentProfile(Handbook handbook)
        {
            var session = handbook != null ? Traverse.Create(handbook).Field("iClientSession").GetValue() : null;
            return (session as IProfileSession)?.Profile;
        }

        internal static bool Hidden(Profile profile, HashSet<string> carried, HandbookData data)
        {
            if (data == null || data.Type != ENodeType.Item)
            {
                return false;
            }

            var category = data.ParentId;
            if (category != Tutorial.NotesCategory && category != Tutorial.TapesCategory)
            {
                return false;
            }

            return !Tutorial.Found(profile.Id, data.Id) && !carried.Contains(data.Id);
        }

        // Everything the character has now, on them and in the stash.
        internal static HashSet<string> Carried(Profile profile)
        {
            var items = new HashSet<string>();
            var inventory = profile?.Inventory;
            if (inventory == null)
            {
                return items;
            }

            foreach (var item in inventory.AllRealPlayerItems)
            {
                items.Add(item.StringTemplateId);
            }

            if (inventory.Stash != null)
            {
                foreach (var item in inventory.Stash.GetAllItems())
                {
                    items.Add(item.StringTemplateId);
                }
            }

            return items;
        }

        // A Handbook built before something was found is rebuilt when it is shown again.
        internal static void Refresh()
        {
            if (_handbook != null && Tutorial.Present && _builtFor != Tutorial.FoundVersion)
            {
                _handbook.UpdateCompletableCount();
            }
        }

        [HarmonyPatch(typeof(Handbook), nameof(Handbook.CreateNodeTree))]
        internal static class TreePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Handbook __instance, ref HandbookData[] data)
            {
                if (!Interface10Plugin.Handbook.Value || !Tutorial.Present || data == null)
                {
                    return;
                }

                var profile = CurrentProfile(__instance);
                if (profile == null)
                {
                    return;
                }

                _handbook = __instance;
                _builtFor = Tutorial.FoundVersion;
                var carried = Carried(profile);
                data = data.Where(d => !Hidden(profile, carried, d)).ToArray();
            }
        }

        [HarmonyPatch(typeof(HandbookScreen), nameof(HandbookScreen.Show),
            typeof(InventoryController), typeof(IEftSession), typeof(Handbook), typeof(EFT.HealthSystem.IHealthController))]
        internal static class ShowPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Handbook handbook)
            {
                if (Interface10Plugin.Handbook.Value)
                {
                    try
                    {
                        _handbook = handbook ?? _handbook;
                        Refresh();
                    }
                    catch (Exception ex)
                    {
                        Interface10Plugin.Log.LogWarning($"Handbook not refreshed: {ex.Message}");
                    }
                }
            }
        }

        // The transcript after a tape's description, wherever the description is shown (the Handbook, Inspect).
        [HarmonyPatch(typeof(ItemInfoWindowLabels), nameof(ItemInfoWindowLabels.SetDescriptionText))]
        internal static class DescriptionPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ItemInfoWindowLabels __instance, ref string value)
            {
                if (!Interface10Plugin.Handbook.Value || !Tutorial.Present)
                {
                    return;
                }

                var panel = __instance.GetComponentInParent<ItemSpecificationPanel>();
                var item = panel != null ? Traverse.Create(panel).Field<Item>("_item").Value : null;
                if (item == null || !Tutorial.IsTape(item.StringTemplateId))
                {
                    return;
                }

                var lines = Tutorial.Transcript(item.StringTemplateId);
                if (lines.Count == 0)
                {
                    return;
                }

                var text = new StringBuilder(value ?? string.Empty);
                text.Append("\n\n").Append(Lang.Get("tapes.transcript", "<b>Transcript:</b>"));
                foreach (var line in lines)
                {
                    text.Append("\n\n");
                    if (!string.IsNullOrEmpty(line.Key))
                    {
                        text.Append("<color=#8C8C8C>").Append(line.Key).Append(":</color>\n");
                    }

                    text.Append(line.Value);
                }

                value = text.ToString();
            }
        }

        [HarmonyPatch(typeof(HandbookItemPreview), nameof(HandbookItemPreview.ChooseEntity))]
        internal static class ChoosePatch
        {
            [HarmonyPostfix]
            private static void Postfix(HandbookItemPreview __instance, Item item)
            {
                if (!Interface10Plugin.Handbook.Value || !Tutorial.Present)
                {
                    return;
                }

                try
                {
                    var panel = Traverse.Create(__instance).Field<ItemSpecificationPanel>("_itemSpecificationPanel").Value;
                    if (panel == null)
                    {
                        return;
                    }

                    var template = item != null ? item.StringTemplateId : null;
                    var tape = Tutorial.IsTape(template);
                    var note = Tutorial.IsNote(template);
                    RecorderPanel10.For(panel)?.Show(tape ? item : null);
                    ReadButton10.For(panel)?.Show(note ? item : null);
                    if (tape || note)
                    {
                        // looked at: no longer new (HandbookNew10)
                        HandbookNew10.See(HandbookNew10.ProfileId(), template);
                    }
                    // 1.0 shows no other buttons under a note or tape (the flea market searches, the wishlist): only READ
                    var others = panel.transform.Find("Contents/ItemInfo/InteractionButtonsPanel/InteractionButtonsContainer");
                    if (others != null)
                    {
                        others.gameObject.SetActive(!tape && !note);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Handbook preview: {ex}");
                }
            }
        }

        // 1.2's mark of a collectible (CompletableIcon, 18 x 18) on a note's or tape's picture in the list, under the
        // wishlist mark in the picture's left column.
        [HarmonyPatch(typeof(EntityListElement), nameof(EntityListElement.Show))]
        internal static class EntityPatch
        {
            [HarmonyPostfix]
            private static void Postfix(EntityListElement __instance, HandbookNode node)
            {
                if (!Interface10Plugin.Handbook.Value || !Tutorial.Present)
                {
                    return;
                }

                try
                {
                    var template = node?.Data?.Item?.StringTemplateId ?? node?.Data?.Id;
                    var collectible = Tutorial.IsNote(template) || Tutorial.IsTape(template);
                    var column = __instance.transform.Find("Main/ItemView/Info Panel/LeftLayout");
                    var mark = column != null ? column.Find("CompletableIcon") : null;
                    if (mark == null && collectible && column != null)
                    {
                        var sprite = Ui.FileSprite("CompletableIcons_0");
                        if (sprite == null)
                        {
                            return;
                        }

                        var image = Ui.Image(column, "CompletableIcon", Color.white, sprite);
                        image.raycastTarget = false;
                        image.rectTransform.sizeDelta = new Vector2(18f, 18f);
                        Ui.Element(image.gameObject, minWidth: 18f, minHeight: 18f, preferredWidth: 18f, preferredHeight: 18f);
                        var wishlist = column.Find("WishlistIcon");
                        image.transform.SetSiblingIndex(wishlist != null ? wishlist.GetSiblingIndex() + 1 : 0);
                        mark = image.transform;
                    }

                    // the list's rows are reused for other items
                    if (mark != null)
                    {
                        mark.gameObject.SetActive(collectible);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Handbook list mark: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(HandbookItemPreview), nameof(HandbookItemPreview.ClearEntity))]
        internal static class ClearPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HandbookItemPreview __instance)
            {
                var panel = Traverse.Create(__instance).Field<ItemSpecificationPanel>("_itemSpecificationPanel").Value;
                if (panel != null)
                {
                    panel.GetComponentInChildren<RecorderPanel10>(true)?.Show(null);
                    panel.GetComponentInChildren<ReadButton10>(true)?.Show(null);
                }
            }
        }
    }

    // 1.0's RecorderPanel at the bottom of the Handbook's model view: 600 x 85, the three buttons in a row (40 high),
    // the time under them, or the red notice that a recorder is needed.
    internal class RecorderPanel10 : MonoBehaviour
    {
        private static readonly Color TimeColor = new Color(0.9765f, 0.9765f, 0.8902f, 1f);
        private static readonly Color NoticeColor = new Color(0.7686f, 0.0627f, 0.0706f, 1f);

        private AudioSource _audio;
        private GameObject _active;
        private GameObject _inactive;
        private DefaultUIButton _play;
        private DefaultUIButton _pause;
        private DefaultUIButton _stop;
        private TextMeshProUGUI _current;
        private TextMeshProUGUI _total;
        private bool _paused;

        internal static RecorderPanel10 For(ItemSpecificationPanel panel)
        {
            var existing = panel.GetComponentInChildren<RecorderPanel10>(true);
            if (existing != null)
            {
                return existing;
            }

            var preview = panel.transform.Find("Contents/Preview Panel");
            var template = Template();
            if (preview == null || template == null)
            {
                Interface10Plugin.Log.LogWarning($"No recorder panel: preview {preview != null}, button template {template != null}");
                return null;
            }

            var rect = Ui.Rect(preview, "RecorderPanel");
            Ui.Place(rect, new[] { 0.5f, 0f, 0.5f, 0f, 0f, 0f, 600f, 85f, 0.5f, 0f });
            var recorder = rect.gameObject.AddComponent<RecorderPanel10>();
            recorder.Build(template);
            rect.gameObject.SetActive(false);
            return recorder;
        }

        // A button of the game's own to copy (it brings the 1.0 look with it): the Handbook's BACK.
        private static DefaultUIButton Template()
        {
            var screen = FindObjectOfType<HandbookScreen>();
            return screen != null ? screen.GetComponentsInChildren<DefaultUIButton>(true).FirstOrDefault() : null;
        }

        private void Build(DefaultUIButton template)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.loop = false;

            var active = Ui.Rect(transform, "ActiveState");
            Ui.Stretch(active);
            _active = active.gameObject;
            var buttons = Ui.Rect(active, "Buttons");
            Ui.Place(buttons, new[] { 0f, 1f, 1f, 1f, 0f, 0f, 0f, 40f, 0.5f, 1f });
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            row.childControlWidth = false;
            row.childControlHeight = false;
            _play = Button(template, buttons, "Play", Lang.Get("tapes.play", "PLAY"), Play);
            _pause = Button(template, buttons, "Pause", Lang.Get("tapes.pause", "PAUSE"), Pause);
            _stop = Button(template, buttons, "Stop", Lang.Get("tapes.stop", "STOP"), Stop);

            var timers = Ui.Rect(active, "Timers");
            Ui.Place(timers, new[] { 0f, 0f, 1f, 0f, 0f, 0f, 0f, 46f, 0.5f, 0f });
            var line = timers.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = true;
            line.childControlHeight = false;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = true;
            _current = Time(timers, "Current", "00:00");
            var dots = Time(timers, "Dots", "/");
            Ui.Element(dots.gameObject, preferredWidth: 25f);
            _total = Time(timers, "Total", "00:00");

            var inactive = Ui.Rect(transform, "IncativeState");
            Ui.Stretch(inactive);
            _inactive = inactive.gameObject;
            var notice = Ui.Text(inactive, "IncativeLabel", 24f, NoticeColor);
            Ui.Stretch(notice.rectTransform);
            notice.alignment = TextAlignmentOptions.Center;
            notice.enableWordWrapping = true;
            notice.text = Lang.Get("tapes.no_recorder", "Equip recorder or upgrade Intelligence Center in the Hideout");
            _inactive.SetActive(false);
        }

        private static DefaultUIButton Button(DefaultUIButton template, Transform parent, string name, string text, Action onClick)
        {
            var go = Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            var button = go.GetComponent<DefaultUIButton>();
            // the copy keeps the original's persistent click handlers (BACK's): off, ours instead
            button.OnClick.RemoveAllListeners();
            for (var i = 0; i < button.OnClick.GetPersistentEventCount(); i++)
            {
                button.OnClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }

            button.OnClick.AddListener(() => onClick());
            button.SetRawText(text, 24);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.minWidth = 100f;
            return button;
        }

        private static TextMeshProUGUI Time(Transform parent, string name, string text)
        {
            var label = Ui.Text(parent, name, 24f, TimeColor);
            var font = Ui.Font("Jovanny Lemonad - Bender Normal SDF");
            if (font != null)
            {
                label.font = font;
            }

            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            return label;
        }

        private Item _item;

        // A tape to play, or nothing (the panel goes).
        internal void Show(Item tape)
        {
            if (tape == _item && tape != null)
            {
                return;
            }

            Stop();
            _item = tape;
            gameObject.SetActive(tape != null);
            if (tape == null)
            {
                return;
            }

            var clip = Tutorial.Recording(tape.StringTemplateId);
            _audio.clip = clip;
            var can = clip != null && CanPlay();
            _active.SetActive(can);
            _inactive.SetActive(!can);
            _total.text = Clock(clip != null ? clip.length : 0f);
            _current.text = Clock(0f);
            Refresh();
        }

        // As in 1.0: the recorder equipped (in a special slot), or the Intelligence Center built.
        private static bool CanPlay()
        {
            var profile = TarkovApplication.Exist(out var app) ? (app.Session as IProfileSession)?.Profile : null;
            if (profile == null)
            {
                return true;
            }

            var recorder = profile.Inventory?.AllRealPlayerItems.Any(i => i.StringTemplateId == Tutorial.Recorder
                && i.Parent?.Container?.ID != null && i.Parent.Container.ID.StartsWith("SpecialSlot", StringComparison.Ordinal)) == true;
            var intelligence = profile.Hideout?.Areas?.Any(a => a.Type == (int)EAreaType.IntelligenceCenter && a.Level >= 1) == true;
            return recorder || intelligence;
        }

        private void Play()
        {
            if (_audio.clip == null)
            {
                return;
            }

            if (_paused)
            {
                _audio.UnPause();
            }
            else
            {
                _audio.time = 0f;
                _audio.Play();
            }

            _paused = false;
            Refresh();
        }

        private void Pause()
        {
            if (_audio.isPlaying)
            {
                _audio.Pause();
                _paused = true;
            }

            Refresh();
        }

        private void Stop()
        {
            if (_audio != null)
            {
                _audio.Stop();
                _audio.time = 0f;
            }

            _paused = false;
            if (_current != null)
            {
                _current.text = Clock(0f);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_play == null)
            {
                return;
            }

            // as in 1.0: PLAY lit while it can start, PAUSE while playing, STOP while playing or paused
            var playing = _audio.isPlaying;
            Set(_play, !playing);
            Set(_pause, playing);
            Set(_stop, playing || _paused);
        }

        private static void Set(DefaultUIButton button, bool on)
        {
            if (button.Interactable != on)
            {
                button.Interactable = on;
            }
        }

        private void Update()
        {
            if (_audio == null || _audio.clip == null)
            {
                return;
            }

            Refresh();

            if (_audio.isPlaying)
            {
                _current.text = Clock(_audio.time);
            }
            else if (!_paused && _audio.time <= 0f && _current.text != Clock(0f))
            {
                // played to the end
                Stop();
            }
        }

        private void OnDisable()
        {
            Stop();
            _item = null;
        }

        private static string Clock(float seconds)
        {
            var whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{whole / 60:00}:{whole % 60:00}";
        }
    }

    // 1.0's read button (ReadNoteButton, 41 x 41) in the row of item buttons under the Handbook's model view.
    internal class ReadButton10 : MonoBehaviour
    {
        private Item _note;

        internal static ReadButton10 For(ItemSpecificationPanel panel)
        {
            var existing = panel.GetComponentInChildren<ReadButton10>(true);
            if (existing != null)
            {
                return existing;
            }

            var row = panel.transform.Find("Contents/ItemInfo/InteractionButtonsPanel");
            var sprite = Ui.FileSprite("ReadNoteButton");
            if (row == null || sprite == null)
            {
                return null;
            }

            var image = Ui.Image(row, "ReadNote", Color.white, sprite);
            image.raycastTarget = true;
            Ui.Element(image.gameObject, minWidth: 41f, minHeight: 41f, preferredWidth: 41f, preferredHeight: 41f);
            var read = image.gameObject.AddComponent<ReadButton10>();
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // a little dim at rest, full under the mouse, darker while pressed (Unity's default tint barely shows)
            var colors = button.colors;
            colors.normalColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (read._note != null)
                {
                    Tutorial.Read(read._note);
                }
            });
            image.gameObject.SetActive(false);
            return read;
        }

        internal void Show(Item note)
        {
            _note = note;
            gameObject.SetActive(note != null);
        }
    }
}
