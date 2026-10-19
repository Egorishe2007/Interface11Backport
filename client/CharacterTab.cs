using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arena.UI;
using Comfort.Common;
using EFT;
using EFT.Customization;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using PlayerIcons;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Interface10
{
    // 1.2's CHARACTER tab of the character screen (its CharacterCustomizationTab): the faces of the character's side in a
    // grid, the chosen one large on the right, the voice under the grid with a button that plays it. 1.2 built it from
    // the character creation screen's face selection, which 0.16 has too (EFTSideSelectionScreen/FaceSelection and its
    // FaceSelectionPreview in LoginUIScene, the FaceCardView cards): those are copied into the tab and filled the same
    // way. A face or voice clicked is applied at once, with the CustomizationSet the game sends for a voice; the server
    // part of Interface10 applies a head too (SPT does not). The tab's name is the game's own ("Character").
    internal static class CharacterTab10
    {
        internal const string TabName = "Character10";
        private const string PanelName = "CharacterCustomizationTab10";

        [HarmonyPatch]
        internal static class ShowPatch
        {
            private static MethodBase TargetMethod() =>
                AccessTools.GetDeclaredMethods(typeof(InventoryScreen)).First(m => m.Name == nameof(InventoryScreen.Show) && m.GetParameters().Length == 10);

            [HarmonyPostfix]
            private static void Postfix(InventoryScreen __instance)
            {
                if (!Interface10Plugin.CharacterTab.Value)
                {
                    return;
                }

                try
                {
                    Ensure(__instance);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Character tab: {ex}");
                }
            }
        }

        internal static Profile Profile() =>
            TarkovApplication.Exist(out var app) && app.Session is IProfileSession session ? session.Profile : null;

        internal static EftAccountSideSelectionScreen SideScreen() =>
            Resources.FindObjectsOfTypeAll<EftAccountSideSelectionScreen>().FirstOrDefault(s => s.gameObject.scene.IsValid());

        // Each tab's place in the bar is its order number (TabSizeController: the widest tab's width times it). The
        // copy of Health has Health's: ours gets the next one, and the tabs after it move one on while ours shows.
        private static readonly Dictionary<TabElement, int> Orders = new Dictionary<TabElement, int>();

        private static void Arrange(Transform tabs, Transform health, Transform ours, bool shown)
        {
            var sizes = tabs.GetComponent<TabSizeController>();
            var mine = ours.GetComponent<TabElement>();
            var after = health.GetComponent<TabElement>();
            if (sizes == null || mine == null || after == null)
            {
                return;
            }

            if (!Orders.TryGetValue(after, out var healthOrder))
            {
                Orders[after] = healthOrder = after.OrderNumber;
            }

            foreach (var element in tabs.GetComponentsInChildren<TabElement>(true))
            {
                if (element == mine)
                {
                    continue;
                }

                if (!Orders.TryGetValue(element, out var original))
                {
                    Orders[element] = original = element.OrderNumber;
                }

                element.OrderNumber = shown && original > healthOrder ? original + 1 : original;
            }

            mine.OrderNumber = healthOrder + 1;
            var list = Traverse.Create(sizes).Field<List<TabElement>>("_tabs").Value;
            if (list != null && !list.Contains(mine))
            {
                list.Add(mine);
            }

            sizes.UpdateLayout();
        }

        private static void Ensure(InventoryScreen screen)
        {
            var tabs = screen.transform.Find("Tab Bar/Tabs");
            var health = tabs != null ? tabs.Find("Health") : null;
            if (health == null)
            {
                return;
            }

            var ours = tabs.Find(TabName);
            var view = screen.transform.Find(PanelName)?.GetComponent<CharacterView10>();
            if (ours == null || view == null)
            {
                var copy = Object.Instantiate(health.gameObject, tabs, false);
                copy.name = TabName;
                ours = copy.transform;
                ours.SetSiblingIndex(health.GetSiblingIndex() + 1);
                var label = copy.GetComponent<LocalizedText>();
                if (label != null)
                {
                    label.LocalizationKey = "Character";
                }

                var icon = SideScreen()?.transform.Find("FaceSelection/LeftPanel/FaceSelector/FaceSelectorIcon")?.GetComponent<Image>()?.sprite;
                if (icon != null)
                {
                    foreach (var image in copy.GetComponentsInChildren<Image>(true).Where(i => i.name == "Icon"))
                    {
                        image.sprite = icon;
                        image.preserveAspect = true;
                    }
                }

                var panel = Ui.Rect(screen.transform, PanelName);
                Ui.Stretch(panel);
                var overall = screen.transform.Find("Overall Panel");
                if (overall != null)
                {
                    panel.SetSiblingIndex(overall.GetSiblingIndex() + 1);
                }

                panel.gameObject.SetActive(false);
                view = panel.gameObject.AddComponent<CharacterView10>();
                copy.GetComponent<Tab>().Init(view);
            }

            var tab = ours.GetComponent<Tab>();
            // this Show's tab group: ours after Health, switching with the others
            var group = Traverse.Create(screen).Field("_tabs").GetValue<TabGroup>();
            if (group != null && view.Group != group)
            {
                var field = Traverse.Create(group).Field("_tabs");
                var list = (field.GetValue<Tab[]>() ?? new Tab[0]).Where(t => t != tab).ToList();
                var after = list.IndexOf(health.GetComponent<Tab>());
                list.Insert(after >= 0 ? after + 1 : list.Count, tab);
                field.SetValue(list.ToArray());
                if (view.Group != null)
                {
                    tab.OnSelectionChanged -= view.Group.SelectionChangedHandler;
                }

                tab.OnSelectionChanged += group.SelectionChangedHandler;
                view.Group = group;
            }

            // a PMC's only and not in a raid (the screen's controller says, as it does for the Overall tab); a screen opened
            // on another tab shows that one
            var profile = Profile();
            var controller = Traverse.Create(screen).Field("ScreenController").GetValue<InventoryScreen.InventoryScreenController>();
            var shown = profile != null && profile.Info.Side != EPlayerSide.Savage && controller?.InRaid != true;
            ours.gameObject.SetActive(shown);
            Arrange(tabs, health, ours, shown);
            if (group == null || Traverse.Create(group).Field("_selectedTab").GetValue<Tab>() != tab)
            {
                view.gameObject.SetActive(false);
                tab.UpdateVisual(false, true);
            }
        }
    }

    internal class CharacterView10 : MonoBehaviour, ITabController
    {
        internal TabGroup Group;
        private readonly List<FaceCardView> _cards = new List<FaceCardView>();
        private bool _built;
        private PlayerProfilePreview _preview;
        private Transform _grid;
        private ToggleGroup _toggles;
        private ScrollRect _scroll;
        private DropDownBox _voice;
        private FaceCardView _cardPrefab;
        private List<EquipmentSlot> _hidden;
        private PlayerIconCreatorMaterialChanger _materials;
        private List<CustomizationHead> _heads = new List<CustomizationHead>();
        private List<CustomizationPlayerVoice> _voices = new List<CustomizationPlayerVoice>();
        private Profile _profile;
        private Profile _previewProfile;
        private Action _voiceUnsubscribe;
        private int _shown;

        public void Show()
        {
            gameObject.SetActive(true);
            try
            {
                Build();
                Refresh();
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Character tab: {ex}");
            }
        }

        public Task<bool> TryHide()
        {
            gameObject.SetActive(false);
            return Task.FromResult(true);
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            var side = CharacterTab10.SideScreen() ?? throw new InvalidOperationException("no character creation screen");
            var state = Traverse.Create(side).Field("_headSelectionState").GetValue<HeadSelectionState>();
            _cardPrefab = Traverse.Create(state).Field("_faceCardPrefab").GetValue<FaceCardView>();
            _hidden = Traverse.Create(state).Field("_hiddenSlots").GetValue<List<EquipmentSlot>>() ?? new List<EquipmentSlot>();
            var settings = Traverse.Create(side).Field("_materialSettings").GetValue<PlayerIconCreatorSettings>();
            if (settings != null)
            {
                _materials = new PlayerIconCreatorMaterialChanger(settings);
            }

            // the faces and the voice: 1.2's places in the character screen
            var face = Instantiate(side.transform.Find("FaceSelection").gameObject, transform, false);
            face.name = "FaceSelection";
            face.SetActive(true);
            Visible(face);
            face.transform.Find("AuthorizationText")?.gameObject.SetActive(false);
            face.transform.Find("LeftPanel/NickNamePanel")?.gameObject.SetActive(false);
            if (face.transform.Find("LeftPanel/FaceSelector") is RectTransform selector)
            {
                Ui.Place(selector, new[] { 1f, 1f, 1f, 1f, -462f, -500f, 886f, 756f, 0.5f, 0.5f });
            }

            if (face.transform.Find("LeftPanel/VoiceSelector") is RectTransform voice)
            {
                Ui.Place(voice, new[] { 1f, 1f, 1f, 1f, -905.25f, -929f, 443.67f, 100f, 0f, 1f });
            }

            _grid = face.transform.Find("LeftPanel/FaceSelector/FaceContainer/ViewPort/Grid");
            _toggles = face.transform.Find("LeftPanel/FaceSelector/FaceContainer")?.GetComponent<ToggleGroup>();
            _scroll = face.transform.Find("LeftPanel/FaceSelector/FaceContainer")?.GetComponent<ScrollRect>();
            foreach (Transform old in _grid)
            {
                Destroy(old.gameObject);
            }

            _voice = face.transform.Find("LeftPanel/VoiceSelector/VoiceSelectorDropDown")?.GetComponent<DropDownBox>();
            var play = face.transform.Find("LeftPanel/VoiceSelector/Icon");
            if (play != null)
            {
                var graphic = play.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.raycastTarget = true;
                }

                play.gameObject.AddComponent<Button>().onClick.AddListener(() => PlayVoice(VoiceIndex()));
            }

            // the faction's emblem behind the face, as 1.2 has it
            _profile = CharacterTab10.Profile();
            var emblem = side.transform.Find(_profile?.Info.Side == EPlayerSide.Usec ? "HeadSelectionUsecIcon" : "HeadSelectionBearIcon");
            if (emblem != null)
            {
                var copy = Instantiate(emblem.gameObject, face.transform, false);
                copy.transform.SetAsFirstSibling();
                copy.SetActive(true);
            }

            // the face, large
            var preview = Instantiate(side.transform.Find("PlayerModelViews/FaceSelectionPreview").gameObject, transform, false);
            preview.name = "FaceSelectionPreview";
            preview.SetActive(true);
            Visible(preview);
            // over the whole screen as at character creation: the head camera puts the face into the right half
            // (the creation screen's PreviewPosition, 1.2's too)
            Ui.Stretch((RectTransform)preview.transform);

            _preview = preview.GetComponent<PlayerProfilePreview>();
            _built = true;
        }

        private static void Visible(GameObject go)
        {
            foreach (var group in go.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        private void Refresh()
        {
            _profile = CharacterTab10.Profile();
            if (_profile == null)
            {
                return;
            }

            var side = _profile.Info.Side;
            var solver = Singleton<CustomizationSolver>.Instance;
            _heads = solver.GetAvailableHeads(side).ToList();
            _voices = solver.GetAvailableVoices(side).ToList();
            _previewProfile = _profile.Clone();
            foreach (var slot in _hidden)
            {
                _previewProfile.Inventory.Equipment.GetSlot(slot)?.RemoveItemWithoutRestrictions();
            }

            foreach (var card in _cards.Where(c => c != null))
            {
                Destroy(card.gameObject);
            }

            _cards.Clear();
            var current = _profile.Customization[EBodyModelPart.Head];
            var equipment = _previewProfile.Inventory.Equipment.CloneVisibleItem();
            for (var i = 0; i < _heads.Count; i++)
            {
                var head = _heads[i];
                var customization = new BodyCustomization(_previewProfile.Customization)
                {
                    [EBodyModelPart.Head] = head.Id
                };
                var card = Instantiate(_cardPrefab, _grid, false);
                card.gameObject.SetActive(true);
                card.Init(head.NameLocalizationKey.Localized(), equipment, customization, _toggles, i, ChooseHead, head.Id == current);
                _cards.Add(card);
            }

            if (_voice != null)
            {
                _voiceUnsubscribe?.Invoke();
                var names = _voices.Select(v => v.NameLocalizationKey.Localized()).ToList();
                _voice.Show(() => names, null);
                _voice.UpdateValue(Math.Max(VoiceIndex(), 0), false, null, null);
                _voiceUnsubscribe = _voice.OnValueChanged.Subscribe(ChooseVoice);
            }

            // a grid of 3 x 3 holds every face of a side: no scroll bar then, as in 1.2 (the scroll view lets go of it,
            // else it would show it again)
            var bar = _grid.parent?.parent?.Find("Scrollbar Vertical");
            if (bar != null && _scroll != null)
            {
                var needed = _heads.Count > 9;
                if (!needed)
                {
                    _scroll.verticalScrollbar = null;
                }

                bar.gameObject.SetActive(needed);
            }

            ShowPreview();
        }

        private int VoiceIndex() =>
            _profile == null ? -1 : _voices.FindIndex(v => v.Id == _profile.Customization[EBodyModelPart.Voice]);

        private async void ShowPreview()
        {
            if (_preview == null || _previewProfile == null)
            {
                return;
            }

            var shown = ++_shown;
            try
            {
                await _preview.Show(_previewProfile, 0f);
                if (shown != _shown || _preview == null)
                {
                    return;
                }

                _preview.ChangeCameraPosition(PlayerProfilePreview.ECameraViewType.Head);
                if (_materials != null && _preview.PlayerModelView.PlayerBody != null)
                {
                    _materials.ChangeMaterials(_preview.PlayerModelView.PlayerBody);
                }
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Character tab preview: {ex.Message}");
            }
        }

        private void ChooseHead(int index)
        {
            if (index < 0 || index >= _heads.Count || _previewProfile == null)
            {
                return;
            }

            var head = _heads[index];
            _previewProfile.Customization[EBodyModelPart.Head] = head.Id;
            ShowPreview();
            Apply(head.Id, ECustomizationType.Head, EBodyModelPart.Head);
        }

        private void ChooseVoice(int index)
        {
            if (index < 0 || index >= _voices.Count)
            {
                return;
            }

            PlayVoice(index);
            Apply(_voices[index].Id, ECustomizationType.Voice, EBodyModelPart.Voice);
        }

        private async void PlayVoice(int index)
        {
            if (index < 0 || index >= _voices.Count)
            {
                return;
            }

            try
            {
                var bank = await Singleton<PlayerVoiceLoader>.Instance.TakeVoice(_voices[index].Name, EPhraseTrigger.OnMutter);
                if (bank != null && bank.Clips != null && bank.Clips.Length > 0)
                {
                    Singleton<GUISounds>.Instance.ForcePlaySound(bank.Clips[UnityEngine.Random.Range(0, bank.Clips.Length)].Clip);
                }
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Character tab voice: {ex.Message}");
            }
        }

        // Applied as the game applies a voice (SoundSettingsTab.ChangeVoice): CustomizationSet, then the profile.
        private async void Apply(MongoID id, ECustomizationType type, EBodyModelPart part)
        {
            if (_profile == null || _profile.Customization[part] == id || !TarkovApplication.Exist(out var app) || !(app.Session is IClientSession session))
            {
                return;
            }

            try
            {
                var result = await session.ApplyCustomization(new[] { new AvailableCustomization(id, type, ECustomizationSource.UnlockedInGame) });
                if (result.Failed)
                {
                    Interface10Plugin.Log.LogWarning($"Character tab: {type} {id} not applied: {result.Error}");
                    return;
                }

                _profile.Customization[part] = id;
                Interface10Plugin.Log.LogInfo($"Character tab: {type} set to {id}");
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Character tab: {type} {id}: {ex.Message}");
            }
        }
    }
}
