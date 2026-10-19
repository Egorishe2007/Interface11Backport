using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's windows (PreloaderUIScene and its sharedassets, against 0.16's):
    // - every window's caption 20 high instead of 17, its title and close button in the middle of it;
    // - the confirmation window: margins 32 at the sides and 22 under the buttons, 12 between the text and the buttons;
    // - the inspect window (InfoWindow) at least 800 wide, its caption over the contents 2 in from the edges, the action
    //   buttons with margins of 10, the durability and mastering icons 20x20, the attribute rows' icons 20 x 20 three
    //   in from the left;
    // - the container window (GridWindow) in a grey frame (Allscreens_Borders_Default) 1 outside, its caption 2 in;
    // - the error screens over everything (ErrorScreen, UnknownErrorScreen, outside the windows' placeholder): the same
    //   caption, the title across it 6 in from each side.
    internal static class Windows10
    {
        private const float Caption = 20f;
        private static readonly Color FrameColor = new Color(0.345f, 0.365f, 0.377f, 1f);
        private static readonly HashSet<int> Done = new HashSet<int>();

        // The windows of the UI context (messages, confirmations, lists, build names, ...): once, when it is set up.
        [HarmonyPatch]
        internal static class ConfigurePatch
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                AccessTools.GetDeclaredMethods(typeof(ItemUiContext)).Where(m => m.Name == nameof(ItemUiContext.Configure));

            [HarmonyPostfix]
            private static void Postfix(ItemUiContext __instance)
            {
                if (!Interface10Plugin.Windows.Value)
                {
                    return;
                }

                try
                {
                    var placeholder = __instance.transform.Find("WindowsPlaceholder");
                    if (placeholder == null)
                    {
                        return;
                    }

                    foreach (Transform window in placeholder)
                    {
                        if (!Done.Add(window.GetInstanceID()))
                        {
                            continue;
                        }

                        var caption = CaptionOf(window);
                        if (caption != null)
                        {
                            Middle(caption);
                        }

                        if (window.name == "ConfirmationWindow")
                        {
                            Confirmation(window);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Windows: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(InfoWindow), nameof(InfoWindow.Show))]
        internal static class InfoPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InfoWindow __instance)
            {
                if (Interface10Plugin.Windows.Value && Done.Add(__instance.GetInstanceID()))
                {
                    Try(() => Inspect(__instance.transform), "inspect window");
                }
            }
        }

        [HarmonyPatch(typeof(GridWindow), nameof(GridWindow.Show))]
        internal static class GridPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GridWindow __instance)
            {
                if (Interface10Plugin.Windows.Value && Done.Add(__instance.GetInstanceID()))
                {
                    Try(() => Container(__instance.transform), "container window");
                }
            }
        }

        [HarmonyPatch(typeof(ErrorScreen), nameof(ErrorScreen.Show))]
        internal static class ErrorPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ErrorScreen __instance) => Error(__instance);
        }

        [HarmonyPatch(typeof(UnknownErrorScreen), nameof(UnknownErrorScreen.Show))]
        internal static class UnknownErrorPatch
        {
            [HarmonyPostfix]
            private static void Postfix(UnknownErrorScreen __instance) => Error(__instance);
        }

        private static void Error(Component screen)
        {
            if (Interface10Plugin.Windows.Value && Done.Add(screen.GetInstanceID()))
            {
                Try(() =>
                {
                    if (screen.transform.Find("Caption Panel") is RectTransform caption)
                    {
                        caption.sizeDelta = new Vector2(caption.sizeDelta.x, Caption);
                        if (caption.Find("Caption") is RectTransform title)
                        {
                            Ui.Place(title, new[] { 0f, 0.5f, 1f, 0.5f, 0f, 0f, -12f, 17f, 0.5f, 0.5f });
                        }
                    }
                }, "error screen");
            }
        }

        // The inspect window's attribute rows with a bar (ItemCharacteristicTemplate): 1.2's icon box 20, 3 in.
        [HarmonyPatch(typeof(CharacteristicPanel), nameof(CharacteristicPanel.Show))]
        internal static class AttributeRowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CharacteristicPanel __instance)
            {
                if (!Interface10Plugin.Windows.Value || !Done.Add(__instance.GetInstanceID()))
                {
                    return;
                }

                Try(() =>
                {
                    if (__instance.transform.Find("Icon")?.GetComponent<LayoutElement>() is LayoutElement icon && Mathf.Approximately(icon.preferredWidth, 25f))
                    {
                        icon.preferredWidth = icon.preferredHeight = 20f;
                        if (__instance.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row && row.padding.left == 0)
                        {
                            row.padding = new RectOffset(3, row.padding.right, row.padding.top, row.padding.bottom);
                        }
                    }
                }, "attribute row");
            }
        }

        private static void Try(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Windows, {what}: {ex}");
            }
        }

        // A window's caption as 0.16 has it: across the top, 17 high, out of any layout.
        private static RectTransform CaptionOf(Transform window)
        {
            foreach (var rect in window.GetComponentsInChildren<RectTransform>(true))
            {
                if ((rect.name == "Caption Panel" || rect.name == "CaptionPanel") && Mathf.Approximately(rect.sizeDelta.y, 17f)
                    && Mathf.Approximately(rect.anchorMin.y, 1f) && Mathf.Approximately(rect.anchorMax.x, 1f))
                {
                    return rect;
                }
            }

            return null;
        }

        // 20 high, the title and the close button in the middle.
        internal static void Middle(RectTransform caption)
        {
            caption.sizeDelta = new Vector2(caption.sizeDelta.x, Caption);
            foreach (RectTransform child in caption)
            {
                if (child.name == "Caption" || child.name == "Title")
                {
                    var height = Mathf.Approximately(child.anchorMin.y, child.anchorMax.y) ? child.sizeDelta.y : 17f;
                    child.anchorMin = new Vector2(child.anchorMin.x, 0.5f);
                    child.anchorMax = new Vector2(child.anchorMax.x, 0.5f);
                    child.pivot = new Vector2(child.pivot.x, 0.5f);
                    child.sizeDelta = new Vector2(child.sizeDelta.x, height);
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, 0f);
                }
                else if (child.name == "Close Button")
                {
                    child.anchorMin = child.anchorMax = new Vector2(1f, 0.5f);
                    child.pivot = new Vector2(1f, 0.5f);
                    child.anchoredPosition = new Vector2(-1.5f, 0f);
                    if (child.Find("X") is RectTransform x)
                    {
                        x.anchoredPosition = new Vector2(0f, x.anchoredPosition.y);
                    }
                }
            }
        }

        private static void Confirmation(Transform window)
        {
            var inner = window.Find("Window");
            var column = inner != null ? inner.GetComponent<VerticalLayoutGroup>() : null;
            var buttons = inner != null ? inner.Find("ButtonsPanel") : null;
            if (column == null || buttons == null)
            {
                return;
            }

            column.padding = new RectOffset(32, 32, column.padding.top, 22);
            var spacer = Ui.Rect(inner, "Spacer10");
            Ui.Element(spacer.gameObject, minHeight: 12f);
            spacer.SetSiblingIndex(buttons.GetSiblingIndex());
        }

        private static void Inspect(Transform root)
        {
            var element = root.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minWidth = Mathf.Max(element.minWidth, 800f);
                element.preferredWidth = -1f;
            }

            if (root is RectTransform rootRect && rootRect.sizeDelta.x < 800f)
            {
                rootRect.sizeDelta = new Vector2(800f, rootRect.sizeDelta.y);
            }

            var inner = root.Find("Inner");
            if (inner == null)
            {
                Interface10Plugin.Log.LogWarning("Windows: not 0.16's inspect window, left as it is");
                return;
            }

            if (inner.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup column)
            {
                column.padding = new RectOffset(2, 2, 2, 0);
                column.childAlignment = TextAnchor.UpperCenter;
            }

            // the caption over the contents, 2 in from the window's edges; the contents start under it
            if (inner.Find("Caption Panel") is RectTransform caption)
            {
                (caption.GetComponent<LayoutElement>() ?? caption.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
                Ui.Place(caption, new[] { 0f, 1f, 1f, 1f, 0f, -2f, -4f, Caption, 0.5f, 1f });
                if (caption.Find("Caption") is RectTransform title)
                {
                    Ui.Place(title, new[] { 0f, 0f, 1f, 1f, 26f, 0f, -60f, 0f, 0f, 1f });
                }

                if (caption.Find("Icon") is RectTransform icon)
                {
                    icon.anchoredPosition = new Vector2(6f, icon.anchoredPosition.y);
                }

                if (caption.Find("Close Button") is RectTransform close)
                {
                    Ui.Place(close, new[] { 1f, 0.5f, 1f, 0.5f, -2f, 0f, 26f, 16f, 1f, 0.5f });
                }
            }

            var contents = inner.Find("Contents");
            if (contents != null && contents.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup list)
            {
                list.padding = new RectOffset(list.padding.left, list.padding.right, (int)Caption, list.padding.bottom);
            }

            if (contents != null && contents.Find("InteractionButtonsPanel") is Transform actions
                                 && actions.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                row.padding = new RectOffset(10, 10, 10, 10);
                row.spacing = 10f;
            }

            foreach (var path in new[] { "Contents/DescriptionPanel/CharacteristicsPanel/Durability/Icon", "Contents/DescriptionPanel/CharacteristicsPanel/Mastering/Icon" })
            {
                if (inner.Find(path) is RectTransform icon)
                {
                    icon.sizeDelta = new Vector2(20f, 20f);
                    icon.anchoredPosition = new Vector2(13f, icon.anchoredPosition.y);
                    var size = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
                    size.preferredWidth = size.preferredHeight = 20f;
                }
            }
        }

        private static void Container(Transform root)
        {
            var frame = Ui.Image(root, "BorderSelection10", FrameColor, Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f)), Image.Type.Sliced);
            frame.fillCenter = false;
            Ui.Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(-1f, -1f);
            frame.rectTransform.offsetMax = new Vector2(1f, 1f);
            Ui.Element(frame.gameObject).ignoreLayout = true;
            var shadow = root.Find("Shadow");
            frame.transform.SetSiblingIndex(shadow != null ? shadow.GetSiblingIndex() + 1 : 0);

            if (root.Find("Caption Panel") is RectTransform caption)
            {
                caption.sizeDelta = new Vector2(caption.sizeDelta.x, Caption);
                if (caption.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
                {
                    row.padding = new RectOffset(2, 2, row.padding.top, row.padding.bottom);
                }
            }
        }
    }
}
