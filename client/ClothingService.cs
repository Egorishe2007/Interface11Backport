using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Customization;
using EFT.Trading;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // 1.2's header over Ragman's tactical clothing (ServicesScreen TacticalClothingView/Header, against 0.16's): the bar
    // solid dark green-grey, the title 24 and in capitals in 1.2's sand colour, the side's name replaced by 1.2's BEAR and
    // USEC toggles. The toggles show the clothing of that side: the Unheard Edition's clothing of both sides (bought and
    // worn like the character's own; needs Interface10's server part). Other editions have no toggles, as in 1.2, unless
    // the player turned both sides on with the Commando's "spt clothing on" (the server part says so).
    // After them 1.2's filter of the cards for every edition: all, bought, available, locked (the game's own words).
    internal static class ClothingService10
    {
        private const string Unheard = "unheard_edition";
        private static readonly AccessTools.FieldRef<TacticalClothingView, Trader> ViewTrader = AccessTools.FieldRefAccess<TacticalClothingView, Trader>("_trader");
        private static readonly AccessTools.FieldRef<TacticalClothingView, Profile> ViewProfile = AccessTools.FieldRefAccess<TacticalClothingView, Profile>("_profile");
        // the side Ragman's list shows (null: the character's own), and the side each trader's offers were last asked for
        private static EPlayerSide? _shown;
        private static readonly Dictionary<string, EPlayerSide> Asked = new Dictionary<string, EPlayerSide>();
        // the cards the filter shows: 0 all, 1 bought, 2 available, 3 locked
        internal static int Filter;
        [ThreadStatic] private static bool _inside;

        internal static bool Both(Profile profile) =>
            Interface10Plugin.TaskView.Value && profile?.Info != null
            && (string.Equals(profile.Info.GameVersion, Unheard, StringComparison.OrdinalIgnoreCase) || ServerBoth(false));

        private static bool _serverBoth;
        private static float _serverAskedAt = float.MinValue;

        // Interface10's server part: GET /interface10/clothing/sides, asked again on each visit to Ragman's clothing and
        // at most every 10 s otherwise (the character screen asks for the suits often).
        internal static bool ServerBoth(bool fresh)
        {
            if (!fresh && Time.realtimeSinceStartup - _serverAskedAt < 10f)
            {
                return _serverBoth;
            }

            _serverAskedAt = Time.realtimeSinceStartup;
            try
            {
                var json = SPT.Common.Http.RequestHandler.GetJson("/interface10/clothing/sides");
                _serverBoth = json != null && json.Replace(" ", "").Contains("\"both\":true");
            }
            catch (Exception)
            {
                _serverBoth = false;
            }

            return _serverBoth;
        }

        private static Profile Player => TarkovApplication.Exist(out var app) ? (app.Session as IProfileSession)?.Profile : null;

        [HarmonyPatch(typeof(TacticalClothingView), nameof(TacticalClothingView.Show))]
        internal static class ShowPatch
        {
            // every visit starts on the character's own side, all the cards shown
            [HarmonyPrefix]
            private static void Prefix()
            {
                _shown = null;
                Filter = 0;
                ServerBoth(true);
            }

            [HarmonyPostfix]
            private static void Postfix(TacticalClothingView __instance)
            {
                if (!Interface10Plugin.TaskView.Value)
                {
                    return;
                }

                try
                {
                    // 0.16 has the view on "Ragman", its header under the panel "TacticalClothingView"
                    var header = __instance.transform.Find("TacticalClothingView/Header") ?? __instance.transform.Find("Header");
                    if (header == null)
                    {
                        return;
                    }

                    var look = header.GetComponent<ClothingHeader10>();
                    if (look == null)
                    {
                        look = header.gameObject.AddComponent<ClothingHeader10>();
                        Restyle(__instance, header);
                        look.Build(__instance, header);
                    }

                    look.Refresh();
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Clothing service: {ex}");
                }
            }
        }

        // The offers of the side the toggles show; the trader's cache asked again when the side changes.
        [HarmonyPatch(typeof(Trader), nameof(Trader.GetOffers))]
        internal static class OffersPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Trader __instance, ref EPlayerSide side)
            {
                if (_shown.HasValue)
                {
                    side = _shown.Value;
                }

                if (__instance?.Id != null && Asked.TryGetValue(__instance.Id, out var before) && before != side)
                {
                    __instance._lastClothingUpdate = float.MinValue;
                    __instance._availableOffers = null;
                }

                if (__instance?.Id != null)
                {
                    Asked[__instance.Id] = side;
                }
            }
        }

        // A card shown or hidden by the filter whenever the game sets its state (when it is made, bought, worn...).
        [HarmonyPatch(typeof(ClothingItem), nameof(ClothingItem.SetItemState))]
        internal static class StatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(ClothingItem __instance, ClothingItem.EClothingItemState state)
            {
                if (Interface10Plugin.TaskView.Value)
                {
                    Show(__instance, state);
                }
            }
        }

        internal static bool Passes(ClothingItem.EClothingItemState state)
        {
            switch (Filter)
            {
                case 1:
                    return state == ClothingItem.EClothingItemState.Purchased || state == ClothingItem.EClothingItemState.Selected;
                case 2:
                    return state == ClothingItem.EClothingItemState.PurchaseAvailable;
                case 3:
                    return state == ClothingItem.EClothingItemState.Locked;
                default:
                    return true;
            }
        }

        private static void Show(ClothingItem card, ClothingItem.EClothingItemState state)
        {
            // the cards of the side shown before stay hidden
            if (card == null || card.GetComponent<SideHidden10>() != null)
            {
                return;
            }

            var show = Passes(state);
            if (card.gameObject.activeSelf != show)
            {
                // a card hidden under the mouse takes its tooltip with it (as the mouse leaving it does)
                if (!show && Traverse.Create(card).Field<bool>("_hovered").Value)
                {
                    card.OnPointerExit(null);
                }

                card.gameObject.SetActive(show);
            }
        }

        internal static void ApplyFilter(TacticalClothingView view, int filter)
        {
            Filter = filter;
            foreach (var card in Traverse.Create(view).Field("_views").GetValue<List<ClothingItem>>() ?? new List<ClothingItem>())
            {
                if (card != null)
                {
                    Show(card, view.GetState(card));
                }
            }
        }

        // The character screen's clothing: the Unheard Edition's suits of the other side too.
        [HarmonyPatch(typeof(CustomizationSolver), nameof(CustomizationSolver.GetAvailableSuites))]
        internal static class SuitesPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CustomizationSolver __instance, EPlayerSide playerSide, ref IEnumerable<CustomizationSuite> __result)
            {
                if (_inside || playerSide == EPlayerSide.Savage || !Both(Player))
                {
                    return;
                }

                _inside = true;
                try
                {
                    var other = playerSide == EPlayerSide.Bear ? EPlayerSide.Usec : EPlayerSide.Bear;
                    __result = __result.Concat(__instance.GetAvailableSuites(other)).Distinct().ToList();
                }
                finally
                {
                    _inside = false;
                }
            }
        }

        private static void Restyle(TacticalClothingView view, Transform header)
        {
            var bar = header.GetComponent<Image>();
            if (bar != null)
            {
                bar.color = new Color(0.196f, 0.208f, 0.188f, 1f);
            }

            var title = header.Find("Title")?.GetComponent<TMPro.TMP_Text>();
            if (title != null)
            {
                title.fontSize = 24f;
                title.fontStyle = TMPro.FontStyles.UpperCase;
                title.color = new Color(0.776f, 0.753f, 0.616f, 1f);
            }

            header.Find("Image")?.gameObject.SetActive(false);
            if (view._sideTitle != null)
            {
                view._sideTitle.gameObject.SetActive(false);
            }
        }

        internal static void Choose(TacticalClothingView view, EPlayerSide side)
        {
            var profile = ViewProfile(view);
            var own = profile?.Info?.Side ?? EPlayerSide.Bear;
            if (side != own && !Both(profile))
            {
                return;
            }

            var next = side == own ? (EPlayerSide?)null : side;
            if (next == _shown)
            {
                return;
            }

            var trader = ViewTrader(view);
            if (trader == null)
            {
                return;
            }

            _shown = next;
            try
            {
                // what Close does for the clothing (the cards hidden: Close disposes them with the view), then the offers
                // of the other side; closing the view itself would close Ragman's services list with it
                var look = Traverse.Create(view);
                foreach (var card in look.Field("_views").GetValue<List<ClothingItem>>() ?? new List<ClothingItem>())
                {
                    if (card != null)
                    {
                        card.gameObject.SetActive(false);
                        if (card.GetComponent<SideHidden10>() == null)
                        {
                            card.gameObject.AddComponent<SideHidden10>();
                        }
                    }
                }

                look.Field("_currentPreview").GetValue<Dictionary<EBodyModelPart, ClothingItem>>()?.Clear();
                look.Field("_currentEquipped").GetValue<Dictionary<EBodyModelPart, ClothingItem>>()?.Clear();
                look.Field("_bodyParts").GetValue<Dictionary<EBodyModelPart, ClothingItem.FullOffer[]>>()?.Clear();
                var bundles = look.Field("_bundles").GetValue();
                if (bundles != null)
                {
                    Comfort.Common.Singleton<ClothingIconCreator>.Instance?.End();
                    Traverse.Create(bundles).Method("Release").GetValue();
                    look.Field("_bundles").SetValue(null);
                }

                view.ShowOffers(trader);
            }
            catch (Exception ex)
            {
                Interface10Plugin.Log.LogWarning($"Clothing service, side {side}: {ex}");
            }
        }

        internal static EPlayerSide ShownSide(TacticalClothingView view) => _shown ?? ViewProfile(view)?.Info?.Side ?? EPlayerSide.Bear;

        internal static bool CanShow(TacticalClothingView view, EPlayerSide side) =>
            side == (ViewProfile(view)?.Info?.Side ?? EPlayerSide.Bear) || Both(ViewProfile(view));
    }

    // 1.2's BEAR and USEC toggles at the right of the clothing header (TacticalClothingSideToggle: 80x40, the faction
    // badge, a box with a light when shown), with the separator after them; none without both sides, as 1.2's Standard
    // Edition has none.
    internal class ClothingHeader10 : MonoBehaviour
    {
        private TacticalClothingView _view;
        private readonly Dictionary<EPlayerSide, Toggle10> _toggles = new Dictionary<EPlayerSide, Toggle10>();
        private GameObject _separator;

        private class Toggle10
        {
            internal GameObject Root;
            internal Image Background;
            internal Image Badge;
            internal Image Light;
        }

        internal void Build(TacticalClothingView view, Transform header)
        {
            _view = view;
            var row = Ui.Rect(header, "SideToggles10");
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0.5f);
            row.anchoredPosition = new Vector2(-10f, 0f);
            row.sizeDelta = new Vector2(392f, 40f);
            var layout = Ui.Layout<HorizontalLayoutGroup>(row.gameObject, TextAnchor.MiddleRight, spacing: 10f);
            layout.childControlWidth = layout.childControlHeight = false;
            foreach (var side in new[] { EPlayerSide.Bear, EPlayerSide.Usec })
            {
                _toggles[side] = Toggle(row, side);
            }

            var separator = Ui.Image(row, "Separator", Color.white, Ui.FileSprite("SeparatorVertical"));
            separator.rectTransform.sizeDelta = new Vector2(2f, 32f);
            _separator = separator.gameObject;
            BuildFilter(row);
        }

        // 1.2's drop-down (200 x 30): a copy of the game's own (the character creation's voice list) in 1.2's look.
        private void BuildFilter(Transform row)
        {
            var source = CharacterTab10.SideScreen()?.transform.Find("FaceSelection/LeftPanel/VoiceSelector/VoiceSelectorDropDown")?.GetComponent<DropDownBox>()
                         ?? Resources.FindObjectsOfTypeAll<DropDownBox>().FirstOrDefault(d => d.gameObject.scene.IsValid());
            if (source == null)
            {
                Interface10Plugin.Log.LogWarning("Clothing filter: no drop-down to copy");
                return;
            }

            var copy = Instantiate(source.gameObject, row, false);
            copy.name = "Dropdown10";
            copy.SetActive(true);
            _filter = copy.GetComponent<DropDownBox>();
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 30f);
            var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.preferredWidth = 200f;
            element.preferredHeight = element.minHeight = 30f;
            RaidPrep10.Dropdown(_filter);

            if (copy.transform.Find("Background")?.GetComponent<Image>() is Image back)
            {
                back.color = new Color(0.09f, 0.09f, 0.08f, 1f);
            }
        }

        private static readonly string[] FilterKeys =
            { "EArenaClothingSortingType/All", "ClothingItem/Obtained", "ClothingItem/Purchase", "ClothingItem/Unavailable" };

        private DropDownBox _filter;
        private Action _filterUnsubscribe;

        private void ShowFilter()
        {
            if (_filter == null)
            {
                return;
            }

            _filterUnsubscribe?.Invoke();
            var names = FilterKeys.Select(k => k.Localized()).ToList();
            _filter.Show(() => names, null);
            _filter.UpdateValue(ClothingService10.Filter, false, null, null);
            _filterUnsubscribe = _filter.OnValueChanged.Subscribe(index => ClothingService10.ApplyFilter(_view, index));
        }

        private void OnDestroy() => _filterUnsubscribe?.Invoke();

        private Toggle10 Toggle(Transform row, EPlayerSide side)
        {
            var root = Ui.Rect(row, side + "SideToggle10");
            root.sizeDelta = new Vector2(80f, 40f);
            var toggle = new Toggle10
            {
                Root = root.gameObject,
                Background = Ui.Image(root, "Background", Color.white),
            };
            Ui.Place(toggle.Background.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 88f, 48f, 0.5f, 0.5f });
            toggle.Badge = Ui.Image(root, "SideIcon", Color.white);
            Ui.Place(toggle.Badge.rectTransform, new[] { 0f, 0.5f, 0f, 0.5f, 5f, 0f, 42f, 42f, 0f, 0.5f });
            toggle.Badge.preserveAspect = true;
            var box = Ui.Image(root, "Checkmark", Color.white, Ui.FileSprite("EFT_Services_Outfit_Filter_Button_Toggle_1_2"));
            Ui.Place(box.rectTransform, new[] { 1f, 0.5f, 1f, 0.5f, -12f, 0f, 18f, 18f, 1f, 0.5f });
            toggle.Light = Ui.Image(box.transform, "Checkmark", Color.white, Ui.FileSprite("EFT_Services_Outfit_Filter_Button_Toggle_1_0"));
            Ui.Place(toggle.Light.rectTransform, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 48f, 48f, 0.5f, 0.5f });

            // the clicks land on the background
            toggle.Background.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = toggle.Background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                ClothingService10.Choose(_view, side);
                Refresh();
            });
            root.gameObject.AddComponent<HoverTooltipArea>().SetMessageText(() => side.ToString().ToUpperInvariant());
            return toggle;
        }

        internal void Refresh()
        {
            if (_view == null)
            {
                return;
            }

            ShowFilter();
            var shown = ClothingService10.ShownSide(_view);
            var both = ClothingService10.Both(Traverse.Create(_view).Field<Profile>("_profile").Value);
            _separator?.SetActive(both);
            foreach (var pair in _toggles)
            {
                var side = pair.Key;
                var toggle = pair.Value;
                toggle.Root.SetActive(both);
                var on = side == shown;
                var can = ClothingService10.CanShow(_view, side);
                toggle.Background.sprite = Ui.FileSprite(on ? "EFT_Services_Outfit_Filter_Button_Backgrounds_1_0" : "EFT_Services_Outfit_Filter_Button_Backgrounds_1_1");
                // the badges: dark on the light selected button, light on a dark one, grey when locked
                // Faction_0/1 BEAR/USEC dark, 2/3 light, 4/5 grey
                var badge = side == EPlayerSide.Bear ? (on ? 0 : can ? 2 : 4) : (on ? 1 : can ? 3 : 5);
                toggle.Badge.sprite = Ui.FileSprite("EFT_Services_Outfit_Filter_Icons_Faction_" + badge);
                toggle.Light.gameObject.SetActive(on);
            }
        }
    }

    // A card of the side shown before the toggles changed it: never shown again by the filter.
    internal class SideHidden10 : MonoBehaviour
    {
    }
}
