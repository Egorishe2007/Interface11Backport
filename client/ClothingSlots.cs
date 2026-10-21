using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Comfort.Common;
using Diz.DependencyManager;
using Diz.Resources;
using EFT;
using EFT.Customization;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's clothing on the character screen's left side (its InventoryCustomizationSelector) instead of 0.16's two
    // drop-downs: for the top and for the bottom a box with the suite's name and, over it at the panel's edge, a 100 x 100
    // cell with the clothing's picture (rendered by the game's ClothingIconCreator, as Ragman's clothing list has them)
    // and the part's mark in its corner. The arrow strip over the cell opens the part's other suites as a column of
    // such cells above it; one clicked is put on as the drop-down would (the game's own handler). The top's cell is at
    // the left edge, the bottom's at the right one, the name and experience between them as before.
    internal static class ClothingSlots10
    {
        [HarmonyPatch(typeof(InventoryClothingSelectionPanel), nameof(InventoryClothingSelectionPanel.Show))]
        internal static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryClothingSelectionPanel __instance, List<CustomizationSuite> upperSuites,
                CustomizationSuite selectedUpperSuite, List<CustomizationSuite> lowerSuites, CustomizationSuite selectedLowerSuite,
                bool isInMatching, Action<CustomizationSuite> onSuiteSelected)
            {
                if (!Interface10Plugin.ClothingSlots.Value)
                {
                    return;
                }

                try
                {
                    Side(__instance.transform, "Upper", true, upperSuites, selectedUpperSuite, isInMatching, onSuiteSelected);
                    Side(__instance.transform, "Lower", false, lowerSuites, selectedLowerSuite, isInMatching, onSuiteSelected);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Clothing slots: {ex}");
                }
            }
        }

        private static void Side(Transform panel, string name, bool upper, List<CustomizationSuite> suites, CustomizationSuite selected,
            bool matching, Action<CustomizationSuite> onSelected)
        {
            var side = panel.Find(name);
            if (side == null)
            {
                return;
            }

            var slot = side.GetComponentInChildren<ClothingSlot10>(true);
            if (slot == null)
            {
                // 0.16's icon and drop-down make way
                side.Find("Icon")?.gameObject.SetActive(false);
                side.Find("ClothingSelector")?.gameObject.SetActive(false);
                var rect = Ui.Rect(side, "InventoryCustomizationSelector10");
                // 1.2: 21 from the panel's edge, 11 from its middle, the name box 30 high
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(upper ? 21f : 11f, 35f);
                rect.offsetMax = new Vector2(upper ? -11f : -21f, -35f);
                slot = rect.gameObject.AddComponent<ClothingSlot10>();
                slot.Build(upper);
            }

            slot.Show(suites, selected, matching, onSelected);
        }

        // WTT Head and Voice Selector copies the Overall tab's clothing panel once, after the game's Show, as
        // LeftSide/NewHeadVoiceCustomizationDrawers 50 lower, and makes its two drop-downs choose the head and the voice.
        // The panel has our cells by then, so the copy brought a second pair of cells and names over ours (a click on our
        // top's cell hit the copy's arrow strip) and its own drop-downs stayed hidden: it gets them back, in 1.2's look,
        // and loses our cells. Runs after that mod's postfix.
        [HarmonyPatch(typeof(OverallScreen), nameof(OverallScreen.Show))]
        internal static class HeadVoicePatch
        {
            private static bool _logged;

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(OverallScreen __instance)
            {
                try
                {
                    var copy = __instance.transform.Find("LeftSide/NewHeadVoiceCustomizationDrawers");
                    var ours = copy == null ? null : copy.GetComponentsInChildren<ClothingSlot10>(true);
                    if (ours == null || ours.Length == 0)
                    {
                        return;
                    }

                    foreach (var slot in ours)
                    {
                        slot.gameObject.SetActive(false);
                        UnityEngine.Object.Destroy(slot.gameObject);
                    }

                    foreach (Transform side in copy)
                    {
                        side.Find("Icon")?.gameObject.SetActive(true);
                        foreach (var box in side.GetComponentsInChildren<DropDownBox>(true).Where(b => b.transform.parent == side))
                        {
                            RaidPrep10.Dropdown(box);
                            box.gameObject.SetActive(true);
                        }
                    }

                    if (!_logged)
                    {
                        _logged = true;
                        Interface10Plugin.Log.LogInfo("WTT Head and Voice Selector's copy of the clothing panel: our clothing cells taken out of it, its head and voice drop-downs shown");
                    }
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Clothing slots, head and voice copy: {ex}");
                }
            }
        }
    }

    // The clothing's bundles for the pictures, as Ragman's clothing list has them: retained, the game's icon creator
    // started, and both let go once no cell is shown any more (the character screen closed).
    internal static class ClothingPictures10
    {
        private static readonly HashSet<ClothingSlot10> Users = new HashSet<ClothingSlot10>();
        private static readonly HashSet<string> Loaded = new HashSet<string>();
        private static readonly List<DependencyGraph<IEasyBundle>.Token> Tokens = new List<DependencyGraph<IEasyBundle>.Token>();
        private static bool _begun;
        private static int _waiting;

        internal static bool Ready => _begun && _waiting == 0;

        // the game's icon creator has its model holder (Begin was called and End not since, by us or another screen)
        internal static bool CreatorReady => Singleton<ClothingIconCreator>.Instantiated && Singleton<ClothingIconCreator>.Instance._clothingMeshContainer != null;

        internal static void EnsureBegun()
        {
            if (Singleton<ClothingIconCreator>.Instantiated && !CreatorReady)
            {
                Singleton<ClothingIconCreator>.Instance.Begin();
                _begun = true;
            }
        }

        internal static void Load(ClothingSlot10 user, IEnumerable<CustomizationSuite> suites, Action onReady)
        {
            if (!Singleton<CustomizationSolver>.Instantiated || !Singleton<ClothingIconCreator>.Instantiated || !Singleton<IEasyAssets>.Instantiated)
            {
                return;
            }

            Users.Add(user);
            if (!_begun)
            {
                Singleton<ClothingIconCreator>.Instance.Begin();
                _begun = true;
            }

            var solver = Singleton<CustomizationSolver>.Instance;
            var keys = suites.Where(s => s != null)
                .Select(s => solver.GetItem(s.MainBodyPartItem))
                .Where(c => c != null)
                .SelectMany(c => c.AllResources)
                .Select(r => r?.path)
                .Where(p => !string.IsNullOrEmpty(p) && !Loaded.Contains(p))
                .Distinct()
                .ToList();
            if (keys.Count == 0)
            {
                if (_waiting == 0)
                {
                    onReady();
                }
                else
                {
                    Pending += onReady;
                }

                return;
            }

            foreach (var key in keys)
            {
                Loaded.Add(key);
            }

            _waiting++;
            Pending += onReady;
            var token = EasyAssetsExtensions.Retain(Singleton<IEasyAssets>.Instance, keys, null, CancellationToken.None);
            Tokens.Add(token);
            EasyAssetsExtensions.WaitForAllBundles(token, () =>
            {
                _waiting--;
                if (_waiting == 0)
                {
                    var ready = Pending;
                    Pending = null;
                    try
                    {
                        ready?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Interface10Plugin.Log.LogWarning($"Clothing pictures: {ex.Message}");
                    }
                }
            }, CancellationToken.None, null);
        }

        private static event Action Pending;

        internal static void Done(ClothingSlot10 user)
        {
            if (!Users.Remove(user) || Users.Count > 0)
            {
                return;
            }

            Pending = null;
            if (_begun && Singleton<ClothingIconCreator>.Instantiated)
            {
                Singleton<ClothingIconCreator>.Instance.End();
            }

            _begun = false;
            foreach (var token in Tokens)
            {
                token?.Release();
            }

            Tokens.Clear();
            Loaded.Clear();
            _waiting = 0;
        }
    }

    internal class ClothingSlot10 : MonoBehaviour
    {
        private static readonly Color BorderColor = new Color(0.3608f, 0.3765f, 0.3804f, 1f);
        private static readonly Color CellColor = new Color(0.2706f, 0.2902f, 0.302f, 1f);
        private static readonly Color NameColor = new Color(0.7412f, 0.7294f, 0.6784f, 1f);
        private const float Cell = 100f;

        private bool _upper;
        private TextMeshProUGUI _name;
        private Image _picture;
        private ClothingIcon _icon;
        private Button _toggle;
        private RectTransform _list;
        private RectTransform _content;
        private readonly List<(Image Image, ClothingIcon Icon)> _pending = new List<(Image, ClothingIcon)>();
        private List<CustomizationSuite> _suites = new List<CustomizationSuite>();
        private CustomizationSuite _selected;
        private Action<CustomizationSuite> _onSelected;

        internal void Build(bool upper)
        {
            _upper = upper;

            // the name box; a click on it opens the other suites too (it is where 0.16's drop-down was)
            var box = Ui.Rect(transform, "Label");
            Ui.Stretch(box);
            var back = Ui.Image(box, "Background", new Color(0f, 0f, 0f, 0.8f));
            back.raycastTarget = true;
            Ui.Stretch(back.rectTransform);
            var label = box.gameObject.AddComponent<Button>();
            label.targetGraphic = back;
            label.transition = Selectable.Transition.None;
            label.onClick.AddListener(Toggle);
            // as 1.2's: 5 left of the middle and 2 lower, the font down to 10 for a long name
            _name = Ui.Text(box, "Text", 15f, NameColor);
            _name.alignment = TextAlignmentOptions.Center;
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 10f;
            _name.fontSizeMax = 15f;
            _name.enableWordWrapping = true;
            _name.overflowMode = TextOverflowModes.Ellipsis;
            _name.margin = new Vector4(0f, -4f, 0f, 0f);
            var text = _name.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(20f, 2f);
            text.offsetMax = new Vector2(-30f, -6f);
            Frame(box);

            // the cell over the box, at the panel's edge: 10 above it, the arrow strip 10 above the cell
            var part = Ui.Rect(transform, "UpperPart");
            part.anchorMin = part.anchorMax = new Vector2(_upper ? 0f : 1f, 1f);
            part.pivot = new Vector2(_upper ? 0f : 1f, 0f);
            part.anchoredPosition = new Vector2(0f, 10f);
            part.sizeDelta = new Vector2(Cell, Cell + 25f);

            var cell = CellAt(part, "InventoryCustomizationSelectorButton", out _picture);
            var mark = Ui.Image(cell, "Icon", Color.white, Ui.FileSprite(_upper ? "upper_body_icon" : "lower_body_icon"));
            var markRect = mark.rectTransform;
            markRect.anchorMin = markRect.anchorMax = markRect.pivot = Vector2.one;
            markRect.anchoredPosition = new Vector2(-5f, -5f);
            markRect.sizeDelta = new Vector2(26f, 26f);
            cell.GetComponent<Button>().onClick.AddListener(Toggle);

            var strip = Ui.Image(part, "ToggleButton", new Color(1f, 1f, 1f, 0.3569f), Ui.FileSprite("EquipementPresets_PlayerPanel_Outfit_Button_0"));
            strip.raycastTarget = true;
            var stripRect = strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.pivot = new Vector2(0.5f, 0f);
            stripRect.anchoredPosition = new Vector2(0f, Cell + 10f);
            stripRect.sizeDelta = new Vector2(0f, 15f);
            var arrow = Ui.Image(strip.transform, "Arrow", Color.white, Ui.FileSprite("Outfit_Arrow"));
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(18f, 11f);
            _toggle = strip.gameObject.AddComponent<Button>();
            _toggle.targetGraphic = strip;
            _toggle.onClick.AddListener(Toggle);

            // the other suites: a column of cells over the strip, scrolled when it is higher than the space
            _list = Ui.Rect(part, "ButtonsRoot");
            _list.anchorMin = _list.anchorMax = new Vector2(0.5f, 0f);
            _list.pivot = new Vector2(0.5f, 0f);
            _list.anchoredPosition = new Vector2(0f, Cell + 31f);
            _list.sizeDelta = new Vector2(Cell + 12f, 0f);
            Ui.Stretch(Ui.Image(_list, "Background", Color.black).rectTransform);
            var view = Ui.Rect(_list, "ScrollRect");
            Ui.Stretch(view);
            view.gameObject.AddComponent<RectMask2D>();
            // the whole list takes the wheel, the gaps between the cells too: only the cells did, and between them it
            // went to whatever lay under the list
            var wheel = view.gameObject.AddComponent<Image>();
            wheel.color = Color.clear;
            wheel.raycastTarget = true;
            _content = Ui.Rect(view, "Content");
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 0f);
            _content.pivot = new Vector2(0.5f, 0f);
            var column = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(6, 6, 6, 6);
            column.spacing = 6f;
            column.childAlignment = TextAnchor.LowerCenter;
            column.childControlWidth = column.childControlHeight = false;
            column.childForceExpandWidth = column.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            Frame(_list);
            _list.gameObject.SetActive(false);
        }

        internal void Show(List<CustomizationSuite> suites, CustomizationSuite selected, bool matching, Action<CustomizationSuite> onSelected)
        {
            _suites = suites ?? new List<CustomizationSuite>();
            _selected = selected;
            _onSelected = onSelected;
            _released = false;
            _name.text = selected != null ? selected.NameLocalizationKey.Localized() : "";
            _picture.enabled = false;
            // the pictures once the clothing's bundles are in (Ragman's list loads them the same way)
            ClothingPictures10.Load(this, _suites, () => _icon = IconOf(_selected, _picture));
            // as the drop-down: nothing to change during matching or with a single suite
            _toggle.interactable = !matching && _suites.Count > 1;
            Close();
        }

        private void Toggle()
        {
            if (_list.gameObject.activeSelf)
            {
                Close();
                return;
            }

            if (!_toggle.interactable)
            {
                return;
            }

            foreach (Transform child in _content)
            {
                Destroy(child.gameObject);
            }

            var shown = 0;
            foreach (var suite in _suites)
            {
                if (suite == null || suite == _selected)
                {
                    continue;
                }

                var cell = CellAt(_content, "Option", out var picture);
                cell.sizeDelta = new Vector2(Cell, Cell);
                _pending.Add((picture, ClothingPictures10.Ready ? IconOf(suite, picture) : null));
                var chosen = suite;
                cell.GetComponent<Button>().onClick.AddListener(() => Choose(chosen));
                shown++;
            }

            if (!ClothingPictures10.Ready)
            {
                ClothingPictures10.Load(this, _suites, RequestPending);
            }

            // up to the space over the strip on the screen (the character's panel), at most six cells
            var height = Mathf.Min(shown, 6) * (Cell + 6f) + 6f;
            _list.sizeDelta = new Vector2(Cell + 12f, height);
            _list.gameObject.SetActive(shown > 0);
        }

        // the options' pictures asked for once the bundles are in
        private void RequestPending()
        {
            var suites = _suites.Where(s => s != null && s != _selected).ToList();
            for (var i = 0; i < _pending.Count && i < suites.Count; i++)
            {
                if (_pending[i].Icon == null && _pending[i].Image != null)
                {
                    _pending[i] = (_pending[i].Image, IconOf(suites[i], _pending[i].Image));
                }
            }
        }

        private void Close()
        {
            if (_list != null)
            {
                _list.gameObject.SetActive(false);
            }

            _pending.Clear();
        }

        private void Choose(CustomizationSuite suite)
        {
            Close();
            try
            {
                _onSelected?.Invoke(suite);
                _selected = suite;
                _name.text = suite.NameLocalizationKey.Localized();
                _icon = ClothingPictures10.Ready ? IconOf(suite, _picture) : null;
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Clothing not changed: {ex.Message}");
            }
        }

        // The picture of a suite's clothing: the game's icon of it, filled in once rendered.
        private static ClothingIcon IconOf(CustomizationSuite suite, Image picture)
        {
            picture.enabled = false;
            if (suite == null || !Singleton<CustomizationSolver>.Instantiated || !Singleton<ClothingIconCreator>.Instantiated)
            {
                return null;
            }

            var clothing = Singleton<CustomizationSolver>.Instance.GetItem(suite.MainBodyPartItem);
            if (clothing == null)
            {
                return null;
            }

            // rendered a little larger than the cell for screens over 1080p
            var icon = Singleton<ClothingIconCreator>.Instance.GetIcon(clothing, new IntVec2(128, 128));
            Fill(picture, icon);
            return icon;
        }

        // Also when the cell already has this icon's sprite: the panel shown again (back from the Prestige tab) hides the
        // picture first and gets the same cached icon.
        private static void Fill(Image picture, ClothingIcon icon)
        {
            if (icon?.Sprite == null)
            {
                return;
            }

            if (picture.sprite != icon.Sprite)
            {
                picture.sprite = icon.Sprite;
                picture.preserveAspect = true;
            }

            picture.enabled = true;
        }

        private void Update()
        {
            if (_icon != null && _picture != null && !_picture.enabled)
            {
                Fill(_picture, _icon);
            }

            foreach (var (image, icon) in _pending)
            {
                if (image != null && !image.enabled)
                {
                    Fill(image, icon);
                }
            }

            Heal();

            // a click elsewhere closes the column
            if (_list != null && _list.gameObject.activeSelf && Input.GetMouseButtonDown(0) && EventSystem.current != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(transform))
                {
                    Close();
                }
            }
        }

        private void OnDisable()
        {
            Close();
            ClothingPictures10.Done(this);
            _released = _picture != null;
        }

        // Shown again without a new Show (back from the Prestige tab, which hides the character's panel): the pictures
        // were let go with the bundles when the cells hid, so they are made again.
        private void OnEnable()
        {
            if (!_released || _selected == null)
            {
                return;
            }

            _released = false;
            _icon = null;
            _picture.enabled = false;
            ClothingPictures10.Load(this, _suites, () => _icon = IconOf(_selected, _picture));
        }

        private bool _released;
        private float _healAt = -1f;
        private bool _healLogged;

        // The cell shown without its picture for a while (back from the Prestige tab, whose screen hides the character's
        // panel): the picture asked for again, the game's cached icon rendered anew when its sprite is gone.
        private void Heal()
        {
            if (_selected == null || _picture == null || (_picture.enabled && _picture.sprite != null))
            {
                _healAt = -1f;
                return;
            }

            if (_healAt < 0f)
            {
                _healAt = Time.unscaledTime + 1.5f;
                return;
            }

            if (Time.unscaledTime < _healAt)
            {
                return;
            }

            _healAt = Time.unscaledTime + 3f;
            var dead = _icon != null && !ReferenceEquals(_icon._sprite, null) && _icon._sprite == null;
            if (!_healLogged)
            {
                _healLogged = true;
                Interface10Plugin.Log.LogInfo($"Clothing cell ({(_upper ? "top" : "bottom")}): picture lost (icon " +
                    $"{(_icon == null ? "none" : dead ? "destroyed" : _icon._sprite == null ? "not rendered" : "ok")}, bundles ready " +
                    $"{ClothingPictures10.Ready}, icon creator {(ClothingPictures10.CreatorReady ? "begun" : "ended")}), asked again");
            }

            var icon = _icon;
            var suite = _selected;
            ClothingPictures10.Load(this, _suites, () =>
            {
                ClothingPictures10.EnsureBegun();
                var clothing = Singleton<CustomizationSolver>.Instantiated ? Singleton<CustomizationSolver>.Instance.GetItem(suite.MainBodyPartItem) : null;
                if (dead && icon != null && clothing != null && Singleton<ClothingIconCreator>.Instantiated)
                {
                    _ = Singleton<ClothingIconCreator>.Instance.FillIconWithNewSpriteAsync(icon, clothing, new IntVec2(128, 128), false, false);
                }
                else
                {
                    _icon = IconOf(suite, _picture);
                }
            });
        }

        // A 100 x 100 cell: grey ground, the picture inset 3, 1.2's thin border; a button.
        private static RectTransform CellAt(Transform parent, string name, out Image picture)
        {
            var cell = Ui.Rect(parent, name);
            cell.anchorMin = new Vector2(0f, 0f);
            cell.anchorMax = new Vector2(1f, 0f);
            cell.pivot = new Vector2(0.5f, 0f);
            cell.sizeDelta = new Vector2(0f, Cell);
            var ground = Ui.Image(cell, "Background", CellColor);
            ground.raycastTarget = true;
            Inset(ground.rectTransform, 3f);
            picture = Ui.Image(cell, "Image", Color.white);
            Inset(picture.rectTransform, 3f);
            Frame(cell);
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = ground;
            button.transition = Selectable.Transition.None;
            return cell;
        }

        // 1.2's frame: Allscreens_Borders_Default (sliced, grey) and a soft border_shadow 4 outside.
        private static void Frame(Transform target)
        {
            var shadow = Ui.Image(target, "Shadow", new Color(1f, 1f, 1f, 0.3412f), Ui.FileSprite("border_shadow", new Vector4(5f, 5f, 5f, 5f)), Image.Type.Sliced);
            shadow.fillCenter = false;
            Ui.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-4f, -4f);
            shadow.rectTransform.offsetMax = new Vector2(4f, 4f);
            var border = Ui.Image(target, "Border", BorderColor, Ui.FileSprite("Allscreens_Borders_Default", new Vector4(1f, 1f, 1f, 1f)), Image.Type.Sliced);
            border.fillCenter = false;
            Ui.Stretch(border.rectTransform);
        }

        private static void Inset(RectTransform rect, float by)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(by, by);
            rect.offsetMax = new Vector2(-by, -by);
        }
    }
}
