using System;
using System.Collections;
using System.Globalization;
using EFT.UI.Chat;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Interface10
{
    // 1.2's date lines between the messages of different days (sharedassets44's MessageFork, against 0.16's): 1.2's
    // message cell is a column, a "Date" row (a line, the date 12 in grey, a line) over the message row. The first
    // message of each day gets it; the game's list measures each cell after showing it, so the row needs no list item.
    internal static class ChatDates10
    {
        private static readonly Color LineColor = new Color(0.1255f, 0.1294f, 0.1373f, 1f);
        private static readonly Color DateColor = new Color(0.349f, 0.3686f, 0.3804f, 1f);
        private static readonly System.Reflection.FieldInfo ListField = AccessTools.Field(typeof(MessagesContainer), "_messageList");

        // the game's languages as .NET cultures, for the date's own form
        private static string CultureName(string game)
        {
            switch (game)
            {
                case "ge": return "de-DE";
                case "cz": return "cs-CZ";
                case "po": return "pt-PT";
                case "tu": return "tr-TR";
                case "ch": return "zh-CN";
                case "jp": return "ja-JP";
                case "kr": return "ko-KR";
                case "es-mx": return "es-MX";
                default: return game;
            }
        }

        private static string Format(DateTime date)
        {
            try
            {
                return date.ToString("d", CultureInfo.GetCultureInfo(CultureName(Ui.Culture)));
            }
            catch (Exception)
            {
                return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            }
        }

        // The cell as 1.2's: its row of message views moved into "Message10", the date row over it.
        private static TextMeshProUGUI Build(MessageFork fork)
        {
            var root = fork.transform;
            if (root.Find("Date10/DateLabel")?.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI built)
            {
                return built;
            }

            if (!(fork.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup old))
            {
                return null;
            }

            var message = Ui.Rect(root, "Message10");
            var row = message.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(old.padding.left, old.padding.right, old.padding.top, old.padding.bottom);
            row.spacing = old.spacing;
            row.childAlignment = old.childAlignment;
            row.childControlWidth = old.childControlWidth;
            row.childControlHeight = old.childControlHeight;
            row.childForceExpandWidth = old.childForceExpandWidth;
            row.childForceExpandHeight = old.childForceExpandHeight;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != message)
                {
                    child.SetParent(message, false);
                    child.SetAsFirstSibling();
                }
            }

            Object.DestroyImmediate(old);
            var column = fork.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 20f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var date = Ui.Rect(root, "Date10");
            date.SetAsFirstSibling();
            var line = date.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(0, 0, 10, -5);
            line.spacing = 5f;
            line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            Line(date);
            var label = Ui.Text(date, "DateLabel", 12f, DateColor);
            label.alignment = TextAlignmentOptions.Center;
            Line(date);
            date.gameObject.SetActive(false);
            return label;
        }

        private static void Line(Transform parent)
        {
            var image = Ui.Image(parent, "Line", LineColor);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 1f;
            element.flexibleWidth = 1f;
        }

        // The first message of its day in the dialogue: no other one of that day is earlier.
        private static bool FirstOfDay(MessagesContainer container, MessageData data)
        {
            if (container == null || !(ListField?.GetValue(container) is IEnumerable list))
            {
                return false;
            }

            var day = data.LocalDateTime.Date;
            foreach (var item in list)
            {
                if (item is MessageData other && !ReferenceEquals(other, data) && other.LocalDateTime.Date == day && other.LocalDateTime < data.LocalDateTime)
                {
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(MessageFork), nameof(MessageFork.Show))]
        internal static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MessageFork __instance, MessageData data)
            {
                if (!Interface10Plugin.MessengerTabs.Value || data == null)
                {
                    return;
                }

                try
                {
                    var label = Build(__instance);
                    if (label == null)
                    {
                        return;
                    }

                    var first = FirstOfDay(__instance.GetComponentInParent<MessagesContainer>(true) ?? Current, data);
                    label.transform.parent.gameObject.SetActive(first);
                    if (first)
                    {
                        label.text = Format(data.LocalDateTime);
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Chat dates: {ex.Message}");
                }
            }
        }

        // the dialogue's container, for cells shown outside it (the list builds them in a side container)
        private static MessagesContainer Current;

        [HarmonyPatch(typeof(MessagesContainer), nameof(MessagesContainer.Show))]
        internal static class ContainerPatch
        {
            [HarmonyPrefix]
            private static void Prefix(MessagesContainer __instance) => Current = __instance;
        }
    }
}
