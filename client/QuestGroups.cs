using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EFT.Quests;
using EFT.Trading;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's trader tasks: the list in groups under headers, as 1.2's TieredQuestListDisplayView has it: one group a
    // loyalty level ("LOYALTY LEVEL 1" with the level's rank plate), then the key tasks (1.2's chains), then the
    // operational (daily) ones, each header with a green mark when a task in it can be taken and a blue tick when one
    // can be handed in, collapsed and opened by a click. Two buttons at the right of the filters switch between that
    // ("By category") and the plain list ("Normal"). 0.16's rows stay as they are and are only put in order.
    //
    // 1.2's data has a tier for every task; 0.16's does not, so the tier is worked out: the trader's loyalty level the
    // task asks for, else the loyalty level the player reaches by the level the task (or the tasks before it) asks for.
    // Key tasks: the ones the data marks so, and the ones that unlock a trader (Mechanic's "Introduction" unlocks
    // Jaeger, as in 1.2's chains).
    internal static class QuestGroups10
    {
        [HarmonyPatch(typeof(QuestsListView), nameof(QuestsListView.Show))]
        internal static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestsListView __instance)
            {
                if (!Interface10Plugin.QuestGroups.Value)
                {
                    return;
                }

                try
                {
                    (__instance.GetComponent<QuestGroupsView10>() ?? __instance.gameObject.AddComponent<QuestGroupsView10>()).Shown();
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Task groups: {ex}");
                }
            }
        }

        // Whenever the game shows or hides a row (filters, a status change), the groups are made again. The row's state
        // after the game AND the other mods have decided (run last: VisitAPI hides its story tasks here) is what the
        // groups show, not a rule of our own.
        [HarmonyPatch(typeof(QuestsListView), nameof(QuestsListView.UpdateSingleQuestVisibility))]
        internal static class VisibilityPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(QuestsListView __instance, QuestListItem questView)
            {
                var groups = __instance.GetComponent<QuestGroupsView10>();
                if (groups != null)
                {
                    groups.Decided(questView);
                    groups.Dirty();
                }
            }
        }

        // 1.2's row: its state as an icon at the right (StatusIcon in the StatusBlock) instead of 0.16's words: a green
        // ring when it can be taken, a blue tick when it can be handed in, a red lock when locked; nothing while it runs
        // (the header over the description says so). Other states (failed, done) keep 0.16's words.
        [HarmonyPatch(typeof(QuestListItem), nameof(QuestListItem.UpdateView))]
        internal static class RowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestListItem __instance)
            {
                if (!Interface10Plugin.QuestGroups.Value || __instance.Quest == null)
                {
                    return;
                }

                try
                {
                    StatusIcon(__instance);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Task row: {ex.Message}");
                }
            }
        }

        private static void StatusIcon(QuestListItem row)
        {
            var block = row.transform.Find("RightBlock/StatusBlock");
            if (block == null)
            {
                return;
            }

            var icon = block.Find("StatusIcon10")?.GetComponent<Image>();
            if (icon == null)
            {
                icon = Ui.Image(block, "StatusIcon10", Color.white);
                icon.preserveAspect = true;
                Ui.Element(icon.gameObject, 26f, 26f, 26f, 26f);
            }

            var status = row.Quest.QuestStatus;
            var sprite = status == EQuestStatus.AvailableForStart ? "Status_AvailableForStart"
                : status == EQuestStatus.AvailableForFinish ? "Status_Completed"
                : status == EQuestStatus.Locked ? "Status_Locked"
                : null;
            icon.sprite = sprite != null ? Ui.FileSprite(sprite) : null;
            icon.gameObject.SetActive(sprite != null);
            var words = sprite == null && status != EQuestStatus.Started;
            if (row._status != null)
            {
                row._status.gameObject.SetActive(words);
            }

            if (row._lockedIcon != null && sprite != null)
            {
                row._lockedIcon.gameObject.SetActive(false);
            }
        }
    }

    internal class QuestGroupsView10 : MonoBehaviour
    {
        private const string ViewKey = "Interface10.TasksView";
        private const string Daily = "daily";
        private const string Key = "key";
        private static readonly Color TitleColor = new Color(0.674f, 0.71f, 0.718f, 1f);
        private static readonly Color ArrowColor = new Color(0.733f, 0.772f, 0.792f, 1f);
        private static readonly Color LineColor = new Color(0.251f, 0.267f, 0.267f, 1f);
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI" };

        // groups closed by a click, per trader, for the session
        private static readonly HashSet<string> Collapsed = new HashSet<string>();

        private readonly Dictionary<string, Header> _headers = new Dictionary<string, Header>();
        private readonly Dictionary<QuestListItem, int> _order = new Dictionary<QuestListItem, int>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        private readonly HashSet<string> _reported = new HashSet<string>();
        // each row's last verdict from UpdateSingleQuestVisibility (the game's filters, then other mods')
        private readonly Dictionary<QuestListItem, bool> _verdict = new Dictionary<QuestListItem, bool>();
        private QuestsListView _view;
        private RectTransform _container;
        private Trader _trader;
        private QuestController _controller;
        private List<EQuestStatus> _exclude;
        private ViewToggle _groupedButton;
        private ViewToggle _plainButton;
        private bool _dirty;
        private int _next;
        private int _count = -1;

        private static bool Grouped
        {
            get => PlayerPrefs.GetInt(ViewKey, 1) == 1;
            set => PlayerPrefs.SetInt(ViewKey, value ? 1 : 0);
        }

        internal void Shown()
        {
            _view = GetComponent<QuestsListView>();
            var fields = Traverse.Create(_view);
            _container = fields.Field<RectTransform>("_questListContainer").Value;
            _trader = fields.Field<Trader>("_trader").Value;
            _controller = fields.Field<QuestController>("_questController").Value;
            _exclude = fields.Field<List<EQuestStatus>>("_questListExcludeFilter").Value;
            _levels.Clear();
            Toggles();
            Dirty();
        }

        internal void Dirty() => _dirty = true;

        internal void Decided(QuestListItem row)
        {
            if (row != null)
            {
                _verdict[row] = row.gameObject.activeSelf;
            }
        }

        private void LateUpdate()
        {
            if (_container == null)
            {
                return;
            }

            // rows added or gone (a trader's list made again, an operational task replaced)
            if (!_dirty && Time.frameCount % 20 == 0 && Rows() != _count)
            {
                _dirty = true;
            }

            // a row the game switched off behind the groups' back (the operational ones refresh themselves)
            if (!_dirty && Grouped && Time.frameCount % 20 == 0)
            {
                foreach (Transform child in _container)
                {
                    var row = child.GetComponent<QuestListItem>();
                    if (row == null || row.Quest == null || !Ours(row.Quest) || row.gameObject.activeSelf || !Passes(row) || Collapsed.Contains(_trader.Id + GroupOf(row.Quest)))
                    {
                        continue;
                    }

                    if (_reported.Add(row.Quest.Id ?? row.name))
                    {
                        Interface10Plugin.Log.LogInfo($"Task groups: row '{row.Quest.Template?.Name}' ({row.Quest.GetType().Name}, {row.Quest.QuestStatus}) was hidden, shown again");
                    }

                    _dirty = true;
                }
            }

            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            try
            {
                Regroup();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Task groups: {ex.Message}");
            }
        }

        private void Regroup()
        {
            // rows of an earlier trader, destroyed since
            foreach (var gone in _verdict.Keys.Where(r => r == null).ToList())
            {
                _verdict.Remove(gone);
            }

            var rows = new List<QuestListItem>();
            foreach (Transform child in _container)
            {
                var row = child.GetComponent<QuestListItem>();
                // the rows of this trader only: the last trader's stay until the end of the frame they are destroyed in
                if (row != null && row.Quest != null && Ours(row.Quest))
                {
                    rows.Add(row);
                    if (!_order.ContainsKey(row))
                    {
                        _order[row] = _next++;
                    }
                }
            }

            rows.Sort((a, b) => _order[a].CompareTo(_order[b]));
            _count = rows.Count;
            var index = 0;
            if (!Grouped)
            {
                foreach (var header in _headers.Values)
                {
                    header.Root.gameObject.SetActive(false);
                }

                foreach (var row in rows)
                {
                    row.transform.SetSiblingIndex(index++);
                    row.gameObject.SetActive(Passes(row));
                }

                return;
            }

            var shown = new HashSet<string>();
            foreach (var group in rows.GroupBy(r => GroupOf(r.Quest)).OrderBy(g => Order(g.Key)))
            {
                var header = HeaderFor(group.Key);
                header.Root.SetSiblingIndex(index++);
                var collapsed = Collapsed.Contains(_trader.Id + group.Key);
                bool any = false, start = false, finish = false;
                foreach (var row in group)
                {
                    row.transform.SetSiblingIndex(index++);
                    var passes = Passes(row);
                    row.gameObject.SetActive(passes && !collapsed);
                    if (!passes)
                    {
                        continue;
                    }

                    any = true;
                    start |= row.Quest.QuestStatus == EQuestStatus.AvailableForStart;
                    finish |= row.Quest.QuestStatus == EQuestStatus.AvailableForFinish;
                }

                header.Show(any, start, finish, collapsed);
                shown.Add(group.Key);
            }

            foreach (var pair in _headers.Where(h => !shown.Contains(h.Key)))
            {
                pair.Value.Root.gameObject.SetActive(false);
            }
        }

        private bool Ours(Quest quest) => quest.Template == null || quest.Template.TraderId == null || quest.Template.TraderId == _trader.Id;

        private int Rows()
        {
            var count = 0;
            foreach (Transform child in _container)
            {
                var row = child.GetComponent<QuestListItem>();
                if (row != null && row.Quest != null && Ours(row.Quest))
                {
                    count++;
                }
            }

            return count;
        }

        // Whether a row is shown: the verdict of QuestsListView.UpdateSingleQuestVisibility when it has given one (other
        // mods may hide rows there, as VisitAPI does with its story tasks), else the game's own rule.
        private bool Passes(QuestListItem row) =>
            _verdict.TryGetValue(row, out var shown) ? shown
                : (_exclude == null || !_exclude.Contains(row.Quest.QuestStatus)) && row.Quest.IsVisible;

        private string GroupOf(Quest quest)
        {
            // the operational ones: dailies and weeklies (their template is a repeatable one)
            if (quest is DailyQuest || quest.Template is RepeatableQuestTemplate)
            {
                return Daily;
            }

            if (quest.Template != null && (quest.Template.KeyQuest || UnlocksTrader(quest)))
            {
                return Key;
            }

            return "tier" + Tier(quest);
        }

        private static int Order(string group) =>
            group == Key ? 100 : group == Daily ? 200 : int.Parse(group.Substring(4), CultureInfo.InvariantCulture);

        private static bool UnlocksTrader(Quest quest) =>
            quest.Rewards != null && quest.Rewards.TryGetValue(EQuestStatus.Success, out var rewards) && rewards != null &&
            rewards.Any(r => r.type == ERewardType.TraderUnlock);

        // The loyalty level the task belongs to: the one it asks of its trader, at least the one the player has by the
        // level it asks for.
        private int Tier(Quest quest)
        {
            var levels = _trader.Settings?.LoyaltyLevels;
            var count = levels != null && levels.Length > 0 ? levels.Length : 4;
            var level = RequiredLevel(quest, 0);
            var tier = 1;
            if (levels != null)
            {
                for (var i = 0; i < levels.Length; i++)
                {
                    if (levels[i].MinProfileLevel <= level)
                    {
                        tier = i + 1;
                    }
                }
            }

            foreach (var condition in StartConditions(quest))
            {
                if (condition is ConditionTraderLoyalty loyalty && loyalty.target == _trader.Id)
                {
                    tier = Math.Max(tier, (int)loyalty.value);
                }
            }

            return Mathf.Clamp(tier, 1, count);
        }

        // The player level a task asks for, with the levels of the tasks it needs done first.
        private int RequiredLevel(Quest quest, int depth)
        {
            var id = quest.Id;
            if (id != null && _levels.TryGetValue(id, out var known))
            {
                return known;
            }

            var level = 0;
            foreach (var condition in StartConditions(quest))
            {
                if (condition is ConditionLevel)
                {
                    level = Math.Max(level, (int)condition.value);
                }
                else if (condition is ConditionQuest before && depth < 30 && _controller != null)
                {
                    var previous = _controller.Quests.GetConditional(before.target);
                    if (previous != null)
                    {
                        level = Math.Max(level, RequiredLevel(previous, depth + 1));
                    }
                }
            }

            if (id != null)
            {
                _levels[id] = level;
            }

            return level;
        }

        private static IEnumerable<Condition> StartConditions(Quest quest)
        {
            if (quest.Conditions == null || !quest.Conditions.TryGetValue(EQuestStatus.AvailableForStart, out var conditions) || conditions == null)
            {
                yield break;
            }

            foreach (var condition in conditions)
            {
                yield return condition;
            }
        }

        private string Title(string group)
        {
            if (group == Key)
            {
                return Lang.Get("quests.key", "Key tasks");
            }

            if (group == Daily)
            {
                return Lang.Get("quests.daily", "Operational tasks");
            }

            return string.Format(Lang.Get("quests.tier", "Loyalty level {0}"), group.Substring(4));
        }

        private Header HeaderFor(string group)
        {
            if (_headers.TryGetValue(group, out var header) && header.Root != null)
            {
                return header;
            }

            header = new Header(_container, group, Title(group).ToUpper(CultureInfo.CurrentCulture),
                group == Key || group == Daily ? null : Roman[Math.Min(int.Parse(group.Substring(4), CultureInfo.InvariantCulture), Roman.Length) - 1],
                group == Key ? Ui.FileSprite("Quests_Chain_Icon") : group == Daily ? Ui.GameSprite("icon_timer") : null);
            header.Clicked = () =>
            {
                var id = _trader.Id + group;
                if (!Collapsed.Remove(id))
                {
                    Collapsed.Add(id);
                }

                Dirty();
            };
            _headers[group] = header;
            return header;
        }

        // 1.2's ViewSelectorToggles at the right of the filters, where 0.16 counts the completed tasks.
        private void Toggles()
        {
            if (_groupedButton != null)
            {
                return;
            }

            var filters = _view.transform.Find("Filters") as RectTransform;
            if (filters == null)
            {
                return;
            }

            filters.Find("Completed")?.gameObject.SetActive(false);
            var panel = Ui.Rect(filters, "ViewSelectorToggles10");
            Ui.Place(panel, new[] { 1f, 0f, 1f, 1f, -5f, 0f, 80f, 0f, 1f, 0.5f });
            _groupedButton = ViewToggle.Create(panel, "TierViewToggle", Ui.GameSprite("skills_list_icon"), -45f,
                Lang.Get("quests.view_grouped", "By category"), () => Switch(true));
            _plainButton = ViewToggle.Create(panel, "FlatViewToggle", Ui.GameSprite("skills_grid_icon"), 0f,
                Lang.Get("quests.view_plain", "Normal"), () => Switch(false));
            ShowView();
        }

        private void Switch(bool grouped)
        {
            Grouped = grouped;
            ShowView();
            Dirty();
        }

        private void ShowView()
        {
            _groupedButton?.Set(Grouped);
            _plainButton?.Set(!Grouped);
        }

        // A group's header: rank plate (or the chain or clock icon), title, marks, the arrow, a line under it.
        private class Header
        {
            internal readonly RectTransform Root;
            internal Action Clicked;
            private readonly Image _start;
            private readonly Image _finish;
            private readonly RectTransform _arrow;

            internal Header(RectTransform container, string group, string title, string rank, Sprite icon)
            {
                Root = Ui.Rect(container, "TaskGroup10_" + group);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = new Vector2(0f, 1f);
                Root.sizeDelta = new Vector2(container.rect.width > 1f ? container.rect.width : 554f, 51f);
                Ui.Element(Root.gameObject, -1f, 51f, -1f, 51f);
                var hit = Root.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var button = Root.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Clicked?.Invoke());

                // the title row: 40 high under 10 of space, as 1.2's Title
                var row = Ui.Rect(Root, "Title");
                Ui.Place(row, new[] { 0f, 1f, 1f, 1f, 0f, -10f, 0f, 40f, 0.5f, 1f });

                var iconPanel = Ui.Rect(row, "AccessoryIconPanel");
                Ui.Place(iconPanel, new[] { 0f, 0.5f, 0f, 0.5f, 21f, 0f, 32f, 32f, 0.5f, 0.5f });
                if (rank != null)
                {
                    var plate = Ui.Image(iconPanel, "Rank Panel", Color.white, Ui.GameSprite("bind_label"));
                    Ui.Place(plate.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 25f, 25f, 0.5f, 0.5f });
                    var number = Ui.Text(plate.transform, "Rank", 14f, Color.black);
                    Ui.Stretch(number.rectTransform);
                    number.alignment = TextAlignmentOptions.Center;
                    number.text = rank;
                }
                else if (icon != null)
                {
                    var image = Ui.Image(iconPanel, "Icon", Color.white, icon);
                    Ui.Place(image.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 28f, 28f, 0.5f, 0.5f });
                    image.preserveAspect = true;
                }

                var block = Ui.Rect(row, "TitleBlock");
                Ui.Place(block, new[] { 0f, 0.5f, 0f, 0.5f, 42f, 0f, 400f, 31f, 0f, 0.5f });
                Ui.Layout<HorizontalLayoutGroup>(block.gameObject, TextAnchor.MiddleLeft, spacing: 8f);
                var text = Ui.Text(block, "Title", 18f, TitleColor);
                text.text = title;
                _start = Mark(block, "StartMark", "Status_AvailableForStart");
                _finish = Mark(block, "FinishMark", "Status_Completed");

                var arrow = Ui.Image(row, "SelectedArrow", ArrowColor, Ui.FileSprite("Dropdown_arrow_down_new_white"));
                _arrow = arrow.rectTransform;
                Ui.Place(_arrow, new[] { 1f, 0.5f, 1f, 0.5f, -30f, 0f, 20f, 20f, 0.5f, 0.5f });
                arrow.preserveAspect = true;

                var line = Ui.Image(Root, "Line", LineColor);
                Ui.Place(line.rectTransform, new[] { 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0.5f, 0f });
            }

            private static Image Mark(RectTransform block, string name, string sprite)
            {
                var image = Ui.Image(block, name, Color.white, Ui.FileSprite(sprite));
                image.preserveAspect = true;
                Ui.Element(image.gameObject, 30f, 30f, 30f, 30f);
                return image;
            }

            internal void Show(bool visible, bool start, bool finish, bool collapsed)
            {
                Root.gameObject.SetActive(visible);
                _start.gameObject.SetActive(start);
                _finish.gameObject.SetActive(finish);
                // open: the arrow up, closed: down
                _arrow.localEulerAngles = new Vector3(0f, 0f, collapsed ? 0f : 180f);
            }
        }

        // 1.2's view button: a dark plate with a light icon, light with a dark icon when chosen, lighter on hover.
        private class ViewToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            private Image _plate;
            private Image _icon;
            private Action _click;
            private bool _on;
            private bool _hover;

            internal static ViewToggle Create(RectTransform panel, string name, Sprite icon, float x, string tooltip, Action click)
            {
                var plate = Ui.Image(panel, name, Color.black, Ui.GameSprite("option_button_on"));
                plate.raycastTarget = true;
                Ui.Place(plate.rectTransform, new[] { 1f, 0.5f, 1f, 0.5f, x, 0f, 35f, 30f, 1f, 0.5f });
                var image = Ui.Image(plate.transform, "Checkmark", Color.white, icon);
                Ui.Place(image.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 20f, 20f, 0.5f, 0.5f });
                image.preserveAspect = true;
                var toggle = plate.gameObject.AddComponent<ViewToggle>();
                toggle._plate = plate;
                toggle._icon = image;
                toggle._click = click;
                try
                {
                    plate.gameObject.AddComponent<HoverTooltipArea>().SetMessageText(tooltip, true);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Task view tooltip: {ex.Message}");
                }

                return toggle;
            }

            internal void Set(bool on)
            {
                _on = on;
                Paint();
            }

            private void Paint()
            {
                _plate.color = _on ? new Color(0.82f, 0.82f, 0.82f, 1f) : _hover ? new Color(0.25f, 0.25f, 0.25f, 1f) : Color.black;
                _icon.color = _on ? Color.black : Color.white;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _hover = true;
                Paint();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _hover = false;
                Paint();
            }

            public void OnPointerClick(PointerEventData eventData) => _click?.Invoke();
        }
    }
}
