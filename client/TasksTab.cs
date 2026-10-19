using System;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's look of the character screen's TASKS tab (Tasks Panel): the active/operational tasks toggles in a full-width
    // bar (QuestTypeSelectionBackground, 50 high, a separator between them) right on top of the task list, the list
    // reaching up to the tab bar on 1.2's backgrounds, and the quest items panel beside it as high, its ITEMS/NOTES
    // toggles in the same kind of bar over it. VisitAPI (the storyline mod) builds this tab its own 1.0 way: left as it is.
    internal static class TasksTab10
    {
        private const float Top = 43f;    // under the tab bar
        private const float Bottom = 33f;
        private const float Bar = 50f;

        internal static bool VisitApi => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.sora.visitapi");

        [HarmonyPatch]
        internal static class ShowPatch
        {
            private static MethodBase TargetMethod() =>
                AccessTools.GetDeclaredMethods(typeof(InventoryScreen)).First(m => m.Name == nameof(InventoryScreen.Show) && m.GetParameters().Length == 10);

            [HarmonyPostfix]
            private static void Postfix(InventoryScreen __instance)
            {
                if (!Interface10Plugin.TasksTab.Value || VisitApi)
                {
                    return;
                }

                try
                {
                    var panel = __instance.transform.Find("Tasks Panel");
                    if (panel != null && panel.GetComponent<TasksLook10>() == null)
                    {
                        Restyle(panel);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Tasks tab: {ex}");
                }
            }
        }

        private static void Restyle(Transform panel)
        {
            var part = panel.Find("TasksPart") as RectTransform;
            var types = part != null ? part.Find("QuestTypeGroup") as RectTransform : null;
            var list = part != null ? part.Find("TasksPanel") as RectTransform : null;
            var items = panel.Find("QuestItemsPanel") as RectTransform;
            if (part == null || types == null || list == null || items == null)
            {
                Interface10Plugin.Log.LogWarning("Tasks tab: not the 0.16 layout, left as it is");
                return;
            }

            // the light strips the toggles stood on go; the dark background reaches up to the tab bar
            panel.Find("WhiteBackground_Left")?.gameObject.SetActive(false);
            panel.Find("WhiteBackground_Right")?.gameObject.SetActive(false);
            if (panel.Find("OverallBackground") is RectTransform overall)
            {
                Insets(overall, Top, 0f);
                Paint(overall, "QuestsTabMainBackground");
            }

            // the task list: from the tab bar down, the bar of toggles first
            Insets(part, Top, Bottom);
            var background = Ui.Image(part, "Background10", Color.white, Ui.FileSprite("QuestsTabQuestListBackground"));
            background.raycastTarget = false;
            Ui.Stretch(background.rectTransform);
            Ui.Element(background.gameObject).ignoreLayout = true;
            background.transform.SetAsFirstSibling();
            var column = part.GetComponent<VerticalLayoutGroup>();
            if (column != null)
            {
                column.padding.top += (int)Bar;
            }

            BarOf(types, "QuestTypeBackground");
            var row = types.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                row.padding = new RectOffset(10, 10, 0, 0);
                row.spacing = 10f;
                row.childAlignment = TextAnchor.MiddleLeft;
                // the bar is the list's width now: the toggles keep their own (0.16's are flexible, sized by the group's
                // content size fitter before)
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;
                foreach (Transform child in types)
                {
                    var element = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                    if (!element.ignoreLayout)
                    {
                        element.flexibleWidth = 0f;
                    }
                }
            }

            var separator = types.Find("Separator");
            if (separator != null)
            {
                var line = separator.GetComponent<Image>() ?? separator.gameObject.AddComponent<Image>();
                line.sprite = Ui.FileSprite("QuestTypeSelectionSeparator");
                line.color = Color.white;
                line.raycastTarget = false;
                ((RectTransform)separator).sizeDelta = new Vector2(2f, 40f);
                Ui.Element(separator.gameObject, minWidth: 2f, minHeight: 40f, preferredWidth: 2f, preferredHeight: 40f);
            }

            // the quest items: as high as the list, their toggles in a bar of their own above (shared with the notes)
            var toggles = panel.Find("QuestItemsToggleGroup");
            var itemsBar = Ui.Rect(panel, "QuestItemsTypeBar10");
            itemsBar.anchorMin = itemsBar.anchorMax = itemsBar.pivot = new Vector2(1f, 1f);
            itemsBar.sizeDelta = new Vector2(items.sizeDelta.x, Bar);
            itemsBar.anchoredPosition = new Vector2(items.anchoredPosition.x, -Top);
            itemsBar.SetSiblingIndex(items.GetSiblingIndex());
            var barImage = Ui.Image(itemsBar, "QuestTypeBackground", Color.white, Ui.FileSprite("QuestTypeSelectionBackground"));
            barImage.raycastTarget = false;
            Ui.Stretch(barImage.rectTransform);
            Ui.Element(barImage.gameObject).ignoreLayout = true;
            var itemsRow = itemsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            itemsRow.padding = new RectOffset(15, 10, 0, 0);
            itemsRow.spacing = 10f;
            itemsRow.childAlignment = TextAnchor.MiddleLeft;
            itemsRow.childControlWidth = itemsRow.childControlHeight = false;
            itemsRow.childForceExpandWidth = itemsRow.childForceExpandHeight = false;
            if (toggles != null)
            {
                foreach (var name in new[] { "QuestItemsImage", "QuestItemsToggleSpawner", "NotesToggleSpawner" })
                {
                    var child = toggles.Find(name);
                    if (child == null)
                    {
                        continue;
                    }

                    child.SetParent(itemsBar, false);
                    if (name == "QuestItemsToggleSpawner")
                    {
                        var line = Ui.Image(itemsBar, "Separator", Color.white, Ui.FileSprite("QuestTypeSelectionSeparator"));
                        line.raycastTarget = false;
                        line.rectTransform.sizeDelta = new Vector2(2f, 40f);
                    }
                }
            }

            Insets(items, Top + Bar, Bottom);
            if (panel.Find("NotesPart") is RectTransform notes)
            {
                Insets(notes, Top + Bar, Bottom);
            }

            panel.gameObject.AddComponent<TasksLook10>().Set(part, list);
        }

        // The bar a row of toggles stands in: 50 high across its parent's top, out of the parent's layout, on 1.2's
        // QuestTypeSelectionBackground.
        private static void BarOf(RectTransform bar, string backgroundName)
        {
            var fitter = bar.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            Ui.Element(bar.gameObject).ignoreLayout = true;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, Bar);
            var image = Ui.Image(bar, backgroundName, Color.white, Ui.FileSprite("QuestTypeSelectionBackground"));
            image.raycastTarget = false;
            Ui.Stretch(image.rectTransform);
            Ui.Element(image.gameObject).ignoreLayout = true;
            image.transform.SetAsFirstSibling();
        }

        // A rect stretched over its parent's height between these insets, its width and side as they are.
        private static void Insets(RectTransform rect, float top, float bottom)
        {
            rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
            rect.offsetMax = new Vector2(rect.offsetMax.x, -top);
        }

        private static void Paint(RectTransform rect, string sprite)
        {
            var image = rect.GetComponent<Image>();
            var file = Ui.FileSprite(sprite);
            if (image != null && file != null)
            {
                image.sprite = file;
                image.color = Color.white;
                image.type = Image.Type.Simple;
            }
        }
    }

    // Keeps the task list as high as the space under the bar (the list's height is fixed in 0.16's layout).
    internal class TasksLook10 : MonoBehaviour
    {
        private RectTransform _part;
        private LayoutElement _list;
        private RectTransform _listRect;
        private int _frame;

        internal void Set(RectTransform part, RectTransform list)
        {
            _part = part;
            _listRect = list;
            _list = list.GetComponent<LayoutElement>();
        }

        private void LateUpdate()
        {
            if (_part == null || _listRect == null || ++_frame % 30 != 1)
            {
                return;
            }

            var column = _part.GetComponent<VerticalLayoutGroup>();
            var height = _part.rect.height - (column != null ? column.padding.top + column.padding.bottom : 50f);
            if (height <= 0f || Mathf.Abs(_listRect.rect.height - height) < 0.5f)
            {
                return;
            }

            if (_list != null)
            {
                _list.minHeight = _list.preferredHeight = height;
            }

            _listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.MarkLayoutForRebuild(_part);
        }
    }
}
