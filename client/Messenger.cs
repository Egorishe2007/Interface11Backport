using System;
using System.Collections.Generic;
using System.Linq;
using ChatShared;
using EFT;
using EFT.UI;
using EFT.UI.Chat;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's messenger (CommonUIScene ChatScreen, against 0.16's):
    // - the dialogues in two tabs over their list, "PMC frequency" (players, groups, the global chat) and "Special
    //   communications" (traders, the system, the flea market, insurance...): 0.16's list keeps the dialogues its
    //   converter (DialogueDataFromDialogue) says yes to, so a tab is that converter saying no to the other ones;
    // - "NO ACTIVE CHATS" in the list when a tab has none (0.16 says it in the chat part);
    // - the friends button and the members count at the foot of the list instead of in the chat's caption.
    internal static class Messenger10
    {
        internal enum Tab { Players, Traders }

        private static Tab? _tab;
        private static readonly AccessTools.FieldRef<DialoguesContainer, SocialNetwork> Social = AccessTools.FieldRefAccess<DialoguesContainer, SocialNetwork>("_social");
        private static readonly AccessTools.FieldRef<DialoguesContainer, Action<UpdatableChatDialogue>> OnTransferAll = AccessTools.FieldRefAccess<DialoguesContainer, Action<UpdatableChatDialogue>>("_onTransferAll");
        private static readonly AccessTools.FieldRef<DialoguesContainer, Action<UpdatableChatDialogue, DialogueView>> OnSelected = AccessTools.FieldRefAccess<DialoguesContainer, Action<UpdatableChatDialogue, DialogueView>>("_onSelected");
        private static readonly AccessTools.FieldRef<DialoguesContainer, Action<UpdatableChatDialogue>> OnRemoved = AccessTools.FieldRefAccess<DialoguesContainer, Action<UpdatableChatDialogue>>("_onRemoved");

        private static bool On => Interface10Plugin.MessengerTabs.Value;

        internal static Tab Of(UpdatableChatDialogue dialogue) =>
            dialogue != null && (dialogue.Type == EMessageType.UserMessage || dialogue.Type == EMessageType.GroupChatMessage || dialogue.Type == EMessageType.GlobalChat)
                ? Tab.Players
                : Tab.Traders;

        // The dialogues of the tab that is shown.
        [HarmonyPatch(typeof(DialoguesContainer), nameof(DialoguesContainer.DialogueDataFromDialogue))]
        internal static class FilterPatch
        {
            [HarmonyPostfix]
            private static void Postfix(UpdatableChatDialogue dialogue, ref bool __result)
            {
                if (On && __result && _tab.HasValue && Of(dialogue) != _tab.Value)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(DialoguesContainer), nameof(DialoguesContainer.Show))]
        internal static class ShowPatch
        {
            // the first time: the tab of the newest dialogue (traders' mail most of the time)
            [HarmonyPrefix]
            private static void Prefix(SocialNetwork social)
            {
                if (On && !_tab.HasValue)
                {
                    _tab = Newest(social);
                }
            }

            [HarmonyPostfix]
            private static void Postfix(DialoguesContainer __instance)
            {
                if (!On)
                {
                    return;
                }

                try
                {
                    var look = __instance.GetComponent<MessengerLook10>() ?? __instance.gameObject.AddComponent<MessengerLook10>();
                    look.Set(__instance);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Messenger: {ex}");
                }
            }
        }

        private static Tab Newest(SocialNetwork social)
        {
            var all = new List<UpdatableChatDialogue>();
            if (social?.PinnedDialogues != null)
            {
                all.AddRange(social.PinnedDialogues);
            }

            if (social?.UnpinnedDialogues != null)
            {
                all.AddRange(social.UnpinnedDialogues);
            }

            var newest = all.Where(d => d?.Message != null).OrderByDescending(d => d.Message.LocalDateTime).FirstOrDefault();
            return newest != null ? Of(newest) : Tab.Traders;
        }

        internal static Tab Current => _tab ?? Tab.Traders;

        internal static bool Any(DialoguesContainer container, Tab tab)
        {
            var social = Social(container);
            return (social?.PinnedDialogues?.Any(d => Of(d) == tab) ?? false) || (social?.UnpinnedDialogues?.Any(d => Of(d) == tab) ?? false);
        }

        internal static void Choose(DialoguesContainer container, Tab tab)
        {
            if (_tab == tab)
            {
                return;
            }

            _tab = tab;
            // the list again, as the chat screen shows it
            container.Show(Social(container), OnTransferAll(container), OnSelected(container), OnRemoved(container));
        }
    }

    // The tabs over the dialogue list, 1.2's empty-list text in it, the social panel at its foot.
    internal class MessengerLook10 : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.78f, 0.828f, 0.851f, 1f);
        private DialoguesContainer _container;
        private readonly Dictionary<Messenger10.Tab, Image> _backgrounds = new Dictionary<Messenger10.Tab, Image>();
        private bool _built;

        internal void Set(DialoguesContainer container)
        {
            _container = container;
            if (!_built)
            {
                _built = true;
                Build();
            }

            Refresh();
        }

        private void Build()
        {
            var part = (RectTransform)_container.transform;
            var tabs = Ui.Rect(part, "Tabs10");
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0f, 0f);
            tabs.anchoredPosition = new Vector2(0f, 3f);
            tabs.sizeDelta = new Vector2(0f, 34f);
            var row = Ui.Layout<HorizontalLayoutGroup>(tabs.gameObject, TextAnchor.LowerLeft, spacing: 4f);
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            Tab(tabs, Messenger10.Tab.Players, "Chat/PMCFrequency", "Social_Tab_Icons_0");
            Tab(tabs, Messenger10.Tab.Traders, "Chat/SpecialCommunications", "Social_Tab_Icons_1");

            // 1.2's "NO ACTIVE CHATS" in the list itself; the game shows and hides it when the list is empty or not
            var area = part.Find("ScrollArea") ?? part;
            var empty = Ui.Text(area, "NoDialogsPlaceholder10", 20f, new Color(0.784f, 0.804f, 0.812f, 1f));
            empty.text = Lang.Get("chat.no_dialogs", "No active chats").ToUpperInvariant();
            empty.alignment = TextAlignmentOptions.Center;
            empty.enableWordWrapping = true;
            Ui.Stretch(empty.rectTransform);
            var old = _container._noDialogsPlaceholder;
            if (old != null)
            {
                old.SetActive(false);
            }

            _container._noDialogsPlaceholder = empty.gameObject;
            empty.gameObject.SetActive(!Messenger10.Any(_container, Messenger10.Current));

            // the friends button and the members count at the foot of the list, left of "+"
            var chat = part.parent != null ? part.parent.Find("ChatPart/CaptionPanel/SocialPanel") as RectTransform : null;
            if (chat != null)
            {
                chat.SetParent(part, false);
                Ui.Place(chat, new[] { 0f, 0f, 0f, 0f, 10f, 19f, 186f, 18f, 0f, 0.5f });
                if (chat.Find("Group") is RectTransform group)
                {
                    Ui.Place(group, new[] { 0f, 0.5f, 0f, 0.5f, 0f, 0f, group.sizeDelta.x, group.sizeDelta.y, 0f, 0.5f });
                }
            }
        }

        private void Tab(Transform tabs, Messenger10.Tab tab, string key, string icon)
        {
            var root = Ui.Rect(tabs, tab + "Tab10");
            Ui.Element(root.gameObject, minWidth: 100f);
            var background = Ui.Image(root, "Background", Color.white, Ui.FileSprite("Social_Tab_Backgrounds_2"));
            Ui.Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(-4f, -4f);
            background.rectTransform.offsetMax = new Vector2(4f, 4f);
            background.raycastTarget = true;
            _backgrounds[tab] = background;
            var picture = Ui.Image(root, "Icon", Color.white, Ui.FileSprite(icon));
            picture.preserveAspect = true;
            Ui.Place(picture.rectTransform, new[] { 0f, 0.5f, 0f, 0.5f, 4f, 0f, 30f, 30f, 0f, 0.5f });
            var text = Ui.Text(root, "Text", 16f, TextColor);
            text.fontStyle = FontStyles.Bold;
            text.text = key.Localized();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            Ui.Place(text.rectTransform, new[] { 0f, 0f, 1f, 1f, 22f, 0f, -44f, 0f, 0.5f, 0.5f });
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                Messenger10.Choose(_container, tab);
                Refresh();
            });
        }

        private void Refresh()
        {
            foreach (var pair in _backgrounds)
            {
                pair.Value.sprite = Ui.FileSprite(pair.Key == Messenger10.Current ? "Social_Tab_Backgrounds_0" : "Social_Tab_Backgrounds_2");
            }
        }
    }
}
