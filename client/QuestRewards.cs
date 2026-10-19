using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Interface10
{
    // 1.2's task view (MenuUIScene QuestView, against 0.16's):
    // - every reward in a 64x64 cell (a shadow, the grid's dark cell, the grey frame): items, trader unlocks, clothing;
    //   experience, skills and standing as 1.2's CommonRewardView, the picture in such a cell (1.2's QuestReward_*
    //   pictures), the kind small and in capitals, the value bold beside it;
    // - the rewards titled "Rewards" (Награды) instead of "Results";
    // - 1.2's "Reward in the next tasks": the items and trader unlocks of the tasks this one opens;
    // - the header's time and lock icons 28x28, the task picture in the grey frame;
    // - the achievements' rewards tooltip (TaskRewardsTooltip, its list made by the same QuestRewardList): the kind 12,
    //   the item's name 18.
    internal static class QuestRewards10
    {
        private static readonly Color CellColor = new Color(0.106f, 0.11f, 0.11f, 1f);
        private static readonly Color FrameColor = new Color(0.345f, 0.365f, 0.377f, 1f);
        private static readonly Color KindColor = new Color(0.549f, 0.541f, 0.502f, 1f);
        private static readonly HashSet<string> Money = new HashSet<string>
        {
            "5449016a4bdc2d6f028b456f", "5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568",
        };

        private static readonly AccessTools.FieldRef<QuestView, Quest> ViewQuest = AccessTools.FieldRefAccess<QuestView, Quest>("_quest");
        private static readonly AccessTools.FieldRef<QuestView, QuestController> ViewController = AccessTools.FieldRefAccess<QuestView, QuestController>("_questController");

        private static bool On => Interface10Plugin.TaskView.Value;

        // Experience, skill, standing: 1.2's cell with the picture, the kind and the value beside it.
        [HarmonyPatch(typeof(StatView), nameof(StatView.Show))]
        internal static class StatPatch
        {
            [HarmonyPostfix]
            private static void Postfix(StatView __instance, QuestReward reward)
            {
                if (On && reward != null)
                {
                    Try(() => Stat(__instance, reward), "reward");
                }
            }
        }

        // The other rewards' cells, and the list's title.
        [HarmonyPatch(typeof(QuestRewardList), nameof(QuestRewardList.Init))]
        internal static class ListPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestRewardList __instance)
            {
                if (On)
                {
                    Try(() => List(__instance), "rewards");
                }
            }
        }

        [HarmonyPatch(typeof(QuestView), nameof(QuestView.ShowRewardsBlock))]
        internal static class NextPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestView __instance, bool show)
            {
                if (On && show)
                {
                    Try(() => Next(__instance), "next tasks' rewards");
                }
                else if (__instance._rewardsContainer != null && __instance._rewardsContainer.transform.parent.Find("RewardsInfoContainer10") is Transform info)
                {
                    info.gameObject.SetActive(false);
                }
            }
        }

        [HarmonyPatch(typeof(QuestView), nameof(QuestView.Show))]
        internal static class ViewPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestView __instance)
            {
                if (On && __instance.GetComponent<TaskViewLook10>() == null)
                {
                    __instance.gameObject.AddComponent<TaskViewLook10>();
                    Try(() => Header(__instance.transform), "task view");
                }
            }
        }

        [HarmonyPatch(typeof(TaskRewardsTooltip), nameof(TaskRewardsTooltip.Show))]
        internal static class TooltipPatch
        {
            [HarmonyPostfix]
            private static void Postfix(TaskRewardsTooltip __instance)
            {
                if (On)
                {
                    Try(() =>
                    {
                        foreach (var text in __instance.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (text.name == "ItemTypeName")
                            {
                                text.fontSize = 12f;
                            }
                            else if (text.name == "ItemName")
                            {
                                text.fontSize = 18f;
                            }
                        }
                    }, "rewards tooltip");
                }
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
                Interface10Plugin.Log.LogWarning($"Task view, {what}: {ex}");
            }
        }

        private static void List(QuestRewardList list)
        {
            if (list._title != null && list._title.text == "QuestRewardsTitle".Localized())
            {
                list._title.text = "Achievements/Tooltip/Task rewards".Localized();
            }

            if (list._container == null)
            {
                return;
            }

            foreach (var back in list._container.GetComponentsInChildren<RectTransform>(true).Where(r => r.name == "IconBack"))
            {
                var image = back.GetComponent<Image>();
                if (image != null && image.enabled && image.sprite != null && image.sprite.name == "cell_full_border")
                {
                    image.enabled = false;
                    Cell(back);
                }
            }

            foreach (var unlock in list._container.GetComponentsInChildren<Image>(true).Where(i => i.name == "UnlockIcon" && i.sprite != null && i.sprite.name == "marker_locked"))
            {
                var sprite = Ui.FileSprite("EFT_UnlockTrade_QuestIcon");
                if (sprite != null)
                {
                    unlock.sprite = sprite;
                    unlock.preserveAspect = true;
                    Ui.Place(unlock.rectTransform, new[] { 0f, 0f, 0f, 0f, 6f, 3f, 28f, 28f, 0f, 0f });
                }
            }
        }

        // 1.2's reward cell under an icon: the shadow 2.5 outside, the dark grid cell, the grey frame; the icon over them.
        private static void Cell(RectTransform back)
        {
            var shadow = Ui.Image(back, "Shadow10", new Color(1f, 1f, 1f, 0.694f), Ui.FileSprite("EquipementSlot_Shadow_Big", new Vector4(9f, 9f, 9f, 9f)), Image.Type.Sliced);
            shadow.fillCenter = false;
            Ui.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-2.5f, -2.5f);
            shadow.rectTransform.offsetMax = new Vector2(2.5f, 2.5f);
            var cell = Ui.Image(back, "IconBackground10", CellColor, Ui.FileSprite("grid_cell"));
            Ui.Stretch(cell.rectTransform);
            var frame = Ui.Image(back, "IconBorder10", FrameColor, Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f)), Image.Type.Sliced);
            frame.fillCenter = false;
            Ui.Stretch(frame.rectTransform);
            foreach (var part in new Component[] { shadow, cell, frame })
            {
                Ui.Element(part.gameObject).ignoreLayout = true;
            }

            shadow.transform.SetSiblingIndex(0);
            cell.transform.SetSiblingIndex(1);
            frame.transform.SetSiblingIndex(2);
        }

        private static void Stat(StatView view, QuestReward reward)
        {
            var root = (RectTransform)view.transform;
            var icon = view._icon;
            if (icon == null || view._name == null || view._value == null)
            {
                return;
            }

            if (root.Find("IconBack") == null)
            {
                var column = root.GetComponent<VerticalLayoutGroup>();
                if (column != null)
                {
                    Object.DestroyImmediate(column);
                }

                var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(6, 10, 4, 4);
                row.spacing = 9f;
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;

                var back = Ui.Rect(root, "IconBack");
                back.SetAsFirstSibling();
                Ui.Element(back.gameObject, 64f, 64f, 64f, 64f);
                Cell(back);
                icon.transform.SetParent(back, false);
                Ui.Place(icon.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 46f, 46f, 0.5f, 0.5f });
                icon.preserveAspect = true;

                var texts = Ui.Rect(root, "BottomLayoutGroup");
                var lines = texts.gameObject.AddComponent<VerticalLayoutGroup>();
                lines.childAlignment = TextAnchor.MiddleLeft;
                lines.childControlWidth = lines.childControlHeight = true;
                lines.childForceExpandWidth = lines.childForceExpandHeight = false;
                Ui.Element(texts.gameObject).flexibleWidth = 1f;
                foreach (var text in new TMP_Text[] { view._name, view._value })
                {
                    var fitter = text.GetComponent<ContentSizeFitter>();
                    if (fitter != null)
                    {
                        Object.DestroyImmediate(fitter);
                    }

                    text.transform.SetParent(texts, false);
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                }

                // 0.16's texts size themselves to fit: 1.2's are fixed, the kind small, the value bigger
                view._name.enableAutoSizing = false;
                view._name.fontSize = 12f;
                view._name.fontStyle = FontStyles.UpperCase;
                view._name.color = KindColor;
                view._value.enableAutoSizing = false;
                view._value.fontSize = 14f;
                view._value.fontStyle = FontStyles.Bold;
            }

            var picture = reward.type == ERewardType.Experience ? "QuestReward_Exp"
                : reward.type == ERewardType.Skill ? "QuestReward_Skill"
                : reward.type == ERewardType.TraderStanding ? "QuestReward_TraderStanding"
                : reward.type == ERewardType.TraderStandingReset ? "QuestReward_TraderStandingReset"
                : reward.type == ERewardType.Pockets ? "QuestReward_Pockets"
                : null;
            var sprite = picture != null ? Ui.FileSprite(picture) : null;
            if (sprite != null)
            {
                icon.sprite = sprite;
            }

            Ui.Place(icon.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 46f, 46f, 0.5f, 0.5f });
            if (string.IsNullOrEmpty(view._name.text) && reward.type == ERewardType.Experience)
            {
                view._name.text = "QuestTypeExperience".Localized();
            }

            view._name.gameObject.SetActive(!string.IsNullOrEmpty(view._name.text));
        }

        // 1.2's "Reward in the next tasks": the items (not money) and trader unlocks of the tasks that need this one. The
        // server part knows every task; without it, only the tasks the client was sent (the ones already in sight).
        private static void Next(QuestView view)
        {
            var quest = ViewQuest(view);
            var controller = ViewController(view);
            if (quest == null || view._rewardListPrefab == null || view._rewardsContainer == null)
            {
                return;
            }

            var rewards = new List<QuestReward>();
            var fromServer = ServerNext();
            if (fromServer != null)
            {
                if (fromServer.TryGetValue(quest.Id, out var listed) && listed != null)
                {
                    rewards.AddRange(listed.Where(Notable));
                }
            }
            else if (controller?.Quests != null)
            {
                foreach (var next in controller.Quests)
                {
                    if (next == null || next == quest || !After(next, quest.Id) || next.Rewards == null
                        || !next.Rewards.TryGetValue(EQuestStatus.Success, out var list) || list == null)
                    {
                        continue;
                    }

                    rewards.AddRange(list.Where(Notable));
                }
            }

            // in a block of its own under the rewards (1.2's RewardsInfoContainer), so the task view's spacing parts them
            var info = InfoContainer(view);
            foreach (Transform old in info)
            {
                Object.Destroy(old.gameObject);
            }

            info.gameObject.SetActive(rewards.Count > 0);
            if (rewards.Count == 0)
            {
                return;
            }

            var go = Object.Instantiate(view._rewardListPrefab, info, false);
            go.name = "NextTasksRewards10";
            go.SetActive(true);
            go.GetComponent<QuestRewardList>()?.Init(Lang.Get("quests.next_rewards", "Reward in the next tasks"), rewards, false, null);
        }

        private static RectTransform InfoContainer(QuestView view)
        {
            var rewards = view._rewardsContainer.transform;
            var parent = rewards.parent;
            if (parent.Find("RewardsInfoContainer10") is RectTransform found)
            {
                return found;
            }

            var info = Ui.Rect(parent, "RewardsInfoContainer10");
            var column = info.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            info.SetSiblingIndex(rewards.GetSiblingIndex() + 1);
            return info;
        }

        // Tasks shown without rewards (not taken yet): the next tasks' rewards go with them.
        [HarmonyPatch(typeof(QuestView), nameof(QuestView.UpdateView))]
        internal static class UpdatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestView __instance)
            {
                var rewards = __instance._rewardsContainer;
                var info = rewards != null ? rewards.transform.parent.Find("RewardsInfoContainer10") : null;
                if (info != null && !rewards.activeSelf)
                {
                    info.gameObject.SetActive(false);
                }
            }
        }

        private static Dictionary<string, List<QuestReward>> _serverNext;
        private static bool _serverAsked;

        // Interface10's server part: /interface10/quests/next, read once with the game's own JSON converters.
        private static Dictionary<string, List<QuestReward>> ServerNext()
        {
            if (_serverAsked)
            {
                return _serverNext;
            }

            _serverAsked = true;
            try
            {
                var json = SPT.Common.Http.RequestHandler.GetJson("/interface10/quests/next");
                if (!string.IsNullOrEmpty(json) && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                {
                    _serverNext = JsonExtensions.ParseJsonTo<Dictionary<string, List<QuestReward>>>(json, Array.Empty<Newtonsoft.Json.JsonConverter>());
                    Interface10Plugin.Log.LogInfo($"Task view: next tasks' rewards for {_serverNext?.Count ?? 0} tasks from the server part");
                }
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Task view: no next tasks' rewards from the server part ({ex.Message}), using the tasks in sight");
                _serverNext = null;
            }

            return _serverNext;
        }

        private static bool After(Quest next, string id)
        {
            if (next.Conditions == null || !next.Conditions.TryGetValue(EQuestStatus.AvailableForStart, out var conditions) || conditions == null)
            {
                return false;
            }

            foreach (var condition in conditions)
            {
                if (condition is ConditionQuest before && before.target == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Notable(QuestReward reward)
        {
            if (reward == null || reward.unknown)
            {
                return false;
            }

            if (reward.type == ERewardType.AssortmentUnlock || reward.type == ERewardType.ProductionScheme)
            {
                return true;
            }

            if (reward.type != ERewardType.Item || reward.items == null)
            {
                return false;
            }

            var root = reward.items.FirstOrDefault(i => i._id.ToString() == reward.target) ?? reward.items.FirstOrDefault();
            return root != null && !Money.Contains(root._tpl.ToString());
        }

        private static void Header(Transform view)
        {
            foreach (var path in new[] { "Center/Header/RightBlock/TimeIcon", "Center/Header/RightBlock/LockedIcon" })
            {
                if (view.Find(path) is RectTransform icon)
                {
                    var size = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
                    size.preferredWidth = size.preferredHeight = 28f;
                    icon.sizeDelta = new Vector2(28f, 28f);
                }
            }

            if (view.Find("Center/Scrollview/Content/CenterBlock")?.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup blocks)
            {
                blocks.spacing = 20f;
            }

            var border = view.Find("Center/Scrollview/Content/CenterBlock/DescriptionBlock/Image/Border")?.GetComponent<Image>();
            if (border != null)
            {
                border.sprite = Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f));
                border.type = Image.Type.Sliced;
                border.color = new Color(0.251f, 0.267f, 0.267f, 1f);
            }
        }
    }

    // Marks a task view already brought to 1.2's look.
    internal class TaskViewLook10 : MonoBehaviour
    {
    }
}
