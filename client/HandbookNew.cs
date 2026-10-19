using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's marks of new notes and tapes. A note or tape the character has found but not yet looked at in the Handbook
    // is new: the menu bar's HANDBOOK shows how many (a green box with the collectible mark, after the box of new items),
    // and the Handbook's categories that hold one show the green collectible mark. Choosing it in the Handbook makes it
    // seen. What each character has seen is kept in BepInEx/config/Interface10.handbook.json; a character without an
    // entry there (the first start with this) has seen everything found until then.
    internal static class HandbookNew10
    {
        private static Dictionary<string, HashSet<string>> _seen;

        private static string PathOf => Path.Combine(BepInEx.Paths.ConfigPath, "Interface10.handbook.json");

        private static Dictionary<string, HashSet<string>> Seen
        {
            get
            {
                if (_seen == null)
                {
                    try
                    {
                        _seen = File.Exists(PathOf)
                            ? JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(File.ReadAllText(PathOf, Encoding.UTF8))
                            : null;
                    }
                    catch (Exception ex)
                    {
                        Interface10Plugin.Log.LogWarning($"Handbook marks not read: {ex.Message}");
                    }

                    _seen = _seen ?? new Dictionary<string, HashSet<string>>();
                }

                return _seen;
            }
        }

        internal static bool On => Interface10Plugin.Handbook.Value && Tutorial.Present;

        internal static string ProfileId() =>
            TarkovApplication.Exist(out var app) ? (app.Session as IProfileSession)?.Profile?.Id : null;

        // The character's found notes and tapes not seen in the Handbook yet.
        internal static List<string> New(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                return new List<string>();
            }

            var found = Tutorial.FoundBy(profileId);
            if (!Seen.TryGetValue(profileId, out var seen))
            {
                Seen[profileId] = new HashSet<string>(found);
                Save();
                return new List<string>();
            }

            return found.Where(id => !seen.Contains(id)).ToList();
        }

        internal static void See(string profileId, string template)
        {
            if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(template))
            {
                return;
            }

            if (!Seen.TryGetValue(profileId, out var seen))
            {
                Seen[profileId] = seen = new HashSet<string>();
            }

            if (seen.Add(template))
            {
                Save();
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(PathOf, JsonConvert.SerializeObject(Seen, Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Handbook marks not saved: {ex.Message}");
            }
        }

        // The menu bar's box: a copy of the game's box of new Handbook items (green, a black count), with 1.2's
        // collectible mark (28 x 28, black) after the count, next to it in the same row.
        [HarmonyPatch(typeof(MenuTaskBar), nameof(MenuTaskBar.InitHandbook))]
        internal static class TaskBarPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MenuTaskBar __instance)
            {
                if (!On)
                {
                    return;
                }

                try
                {
                    var nodes = Traverse.Create(__instance).Field<GameObject>("_newNodesObject").Value;
                    var row = nodes != null ? nodes.transform.parent : null;
                    if (row == null || row.GetComponent<NewBox10>() != null)
                    {
                        return;
                    }

                    var box = UnityEngine.Object.Instantiate(nodes, row, false);
                    box.name = "NewCompletables";
                    box.transform.SetSiblingIndex(nodes.transform.GetSiblingIndex() + 1);
                    var button = box.GetComponent<Button>();
                    if (button != null)
                    {
                        // the copy keeps the original's click handlers: off
                        button.onClick.RemoveAllListeners();
                        for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        {
                            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                        }
                    }

                    var label = box.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        label.name = "NewCompletablesLabel";
                    }

                    var root = Ui.Rect(box.transform, "CompletablesIconRoot");
                    root.sizeDelta = new Vector2(18f, 19.8f);
                    // the row lays out heights too: the mark's root as high as the box, the mark centred on it
                    Ui.Element(root.gameObject, minWidth: 18f, minHeight: 19.8f);
                    var icon = Ui.Image(root, "CompletablesIcon", Color.black, Ui.FileSprite("CompletableIcons_0"));
                    icon.raycastTarget = false;
                    var rect = icon.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(28f, 28f);

                    var marker = row.gameObject.AddComponent<NewBox10>();
                    marker.Box = box;
                    marker.Label = label;
                    box.SetActive(false);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Handbook box of new notes: {ex.Message}");
                }
            }
        }

        // The Handbook's categories: the green mark on Info items, Notes and Audio tapes while one of them holds a new
        // one, left of the game's mark of new items when that shows too.
        [HarmonyPatch(typeof(HandbookScreen), nameof(HandbookScreen.Show),
            typeof(InventoryController), typeof(IEftSession), typeof(Handbook), typeof(EFT.HealthSystem.IHealthController))]
        internal static class ScreenPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HandbookScreen __instance)
            {
                if (On && __instance.GetComponent<CategoryMarks10>() == null)
                {
                    __instance.gameObject.AddComponent<CategoryMarks10>();
                }
            }
        }
    }

    // Keeps the menu bar's box of new notes and tapes up to date.
    internal class NewBox10 : MonoBehaviour
    {
        internal GameObject Box;
        internal TMP_Text Label;
        private float _next;

        private void Update()
        {
            if (Box == null || Time.unscaledTime < _next)
            {
                return;
            }

            _next = Time.unscaledTime + 0.5f;
            var count = HandbookNew10.On ? HandbookNew10.New(HandbookNew10.ProfileId()).Count : 0;
            if (Box.activeSelf != count > 0)
            {
                Box.SetActive(count > 0);
            }

            if (Label != null && count > 0)
            {
                var text = count > 99 ? ">99" : count.ToString();
                if (Label.text != text)
                {
                    Label.text = text;
                }
            }
        }
    }

    internal class CategoryMarks10 : MonoBehaviour
    {
        private const string InfoCategory = "5b47574386f77428ca22b341";
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }

            _next = Time.unscaledTime + 0.5f;
            var fresh = HandbookNew10.On ? HandbookNew10.New(HandbookNew10.ProfileId()) : new List<string>();
            var notes = fresh.Any(Tutorial.IsNote);
            var tapes = fresh.Any(Tutorial.IsTape);
            foreach (var view in GetComponentsInChildren<NodeBaseView>())
            {
                var id = view.Node?.Data?.Id;
                var on = id == Tutorial.NotesCategory ? notes
                    : id == Tutorial.TapesCategory ? tapes
                    : id == InfoCategory && (notes || tapes);
                var mark = view.transform.Find("NewCompletable");
                if (mark == null && on)
                {
                    mark = Make(view.transform);
                }

                if (mark == null)
                {
                    continue;
                }

                if (mark.gameObject.activeSelf != on)
                {
                    mark.gameObject.SetActive(on);
                }

                if (on)
                {
                    // 1.2's StatusIcons: the collectible mark, then the mark of new items at the row's right end
                    var newNode = Traverse.Create(view).Field<GameObject>("_newNodeObject").Value;
                    var x = newNode != null && newNode.activeInHierarchy ? -30f : 0f;
                    var rect = (RectTransform)mark;
                    if (!Mathf.Approximately(rect.anchoredPosition.x, x))
                    {
                        rect.anchoredPosition = new Vector2(x, -3f);
                    }
                }
            }
        }

        private static Transform Make(Transform view)
        {
            var sprite = Ui.FileSprite("CompletableIcons_1");
            if (sprite == null)
            {
                return null;
            }

            var image = Ui.Image(view, "NewCompletable", Color.white, sprite);
            image.raycastTarget = false;
            Ui.Element(image.gameObject).ignoreLayout = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(28f, 28f);
            rect.anchoredPosition = new Vector2(0f, -3f);
            return rect;
        }
    }
}
