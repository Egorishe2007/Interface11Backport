using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using UnityEngine;

namespace Interface10
{
    // Every main menu element of 0.16 is anchored to the bottom centre at a fixed height, with the beta banner between
    // the logo and the buttons. 1.0 has no banner and its buttons in a column around the middle of the screen: ESCAPE FROM
    // TARKOV 39.5 below it, the next ones 84.5 and then 82 apart, EXIT 112 under the last (MenuScreen of 1.0's
    // CommonUIScene). Other mods' buttons in the column (PitFireTeam's MY TEAM) take their place in the same rhythm.
    // PitFireTeam restores and re-lays out these buttons on every Show, so this runs last and takes whatever position
    // another mod left as the new base instead of stacking on top of its own changes.
    internal static class MenuLayout
    {
        private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        private static readonly AccessTools.FieldRef<MenuScreen, GameObject> WarningField =
            AccessTools.FieldRefAccess<MenuScreen, GameObject>("_warningGameObject");
        private static readonly AccessTools.FieldRef<MenuScreen, GameObject> BannerField =
            AccessTools.FieldRefAccess<MenuScreen, GameObject>("_alphaWarningGameObject");

        private static readonly Dictionary<RectTransform, Vector2> Bases = new Dictionary<RectTransform, Vector2>();
        private static readonly Dictionary<RectTransform, Vector2> Applied = new Dictionary<RectTransform, Vector2>();

        internal static void Apply(MenuScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            // The reconnect notice uses the banner's spot, so the buttons stay put while it is shown.
            var warning = WarningField(screen);
            var reconnect = warning != null && warning.activeSelf;
            var lift = Interface10Plugin.HideBetaBanner.Value && !reconnect ? Interface10Plugin.ButtonsLift.Value : 0f;
            var column = new List<RectTransform>();

            foreach (Transform child in screen.transform)
            {
                var rect = child as RectTransform;
                if (rect == null || rect.anchorMin != BottomCentre || rect.anchorMax != BottomCentre
                    || child.name.Contains("Warning"))
                {
                    continue;
                }

                var current = rect.anchoredPosition;
                if (!Applied.TryGetValue(rect, out var applied) || (current - applied).sqrMagnitude > 0.25f)
                {
                    Bases[rect] = current;
                }

                var target = Bases[rect] + new Vector2(0f, lift);
                rect.anchoredPosition = target;
                Applied[rect] = target;
                if (child.gameObject.activeSelf)
                {
                    column.Add(rect);
                }
            }

            if (Interface10Plugin.MenuColumn.Value && !reconnect)
            {
                // the screen's middle, in the bottom-anchored positions the buttons have
                var middle = ((RectTransform)screen.transform).rect.height / 2f;
                var y = -39.5f;
                var index = 0;
                foreach (var rect in column.OrderByDescending(r => Bases[r].y))
                {
                    if (index > 0)
                    {
                        y -= rect.name == "ExitButtonGroup" ? 112f : index == 1 ? 84.5f : 82f;
                    }

                    var target = new Vector2(Bases[rect].x, middle + y);
                    rect.anchoredPosition = target;
                    Applied[rect] = target;
                    index++;
                }
            }

            foreach (var stale in Applied.Keys.Where(key => key == null).ToList())
            {
                Applied.Remove(stale);
                Bases.Remove(stale);
            }
        }

        // For config changes while the menu is open.
        internal static void Refresh()
        {
            var screen = Object.FindObjectOfType<MenuScreen>();
            if (screen == null)
            {
                return;
            }

            var banner = BannerField(screen);
            var warning = WarningField(screen);
            if (banner != null)
            {
                banner.SetActive(!Interface10Plugin.HideBetaBanner.Value && (warning == null || !warning.activeSelf));
            }

            Apply(screen);
        }
    }

    [HarmonyPatch(typeof(MenuScreen), nameof(MenuScreen.Show),
        typeof(Profile), typeof(MatchmakerPlayersController), typeof(ESessionMode))]
    internal static class MenuShowLayoutPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MenuScreen __instance)
        {
            MenuLayout.Apply(__instance);
        }
    }

    // MenuScreen toggles the banner in ChangeToReconnectionAvailable on every main menu show:
    // _alphaWarningGameObject = !reconnect, _warningGameObject = reconnect. Only the first one is hidden.
    // The button lift is not applied here: this runs inside Show, before PitFireTeam captures the original
    // button positions, and a lift captured as "original" would be applied twice.
    [HarmonyPatch(typeof(MenuScreen), nameof(MenuScreen.ChangeToReconnectionAvailable))]
    internal static class BetaBannerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject ____alphaWarningGameObject)
        {
            if (Interface10Plugin.HideBetaBanner.Value && ____alphaWarningGameObject != null)
            {
                ____alphaWarningGameObject.SetActive(false);
            }
        }
    }

    // The main menu's background (the 3D scene behind it): 1.0 lets the player choose it in the settings; here in the
    // config. Factory is the one with the lamp over the logo.
    internal enum MenuBackground
    {
        Game,
        Factory,
        Wood,
        Laboratory,
        Unheard,
        Cyber,
    }

    [HarmonyPatch(typeof(EnvironmentUI), nameof(EnvironmentUI.SetEnvironmentAsync))]
    internal static class MenuBackgroundPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref EEnvironmentUIType environmentUiType)
        {
            switch (Interface10Plugin.Background.Value)
            {
                case MenuBackground.Factory:
                    environmentUiType = EEnvironmentUIType.FactoryEnvironmentUiType;
                    break;
                case MenuBackground.Wood:
                    environmentUiType = EEnvironmentUIType.WoodEnvironmentUiType;
                    break;
                case MenuBackground.Laboratory:
                    environmentUiType = EEnvironmentUIType.LaboratoryEnvironmentUiType;
                    break;
                case MenuBackground.Unheard:
                    environmentUiType = EEnvironmentUIType.TheUnheardEditionEnvironmentUiType;
                    break;
                case MenuBackground.Cyber:
                    environmentUiType = EEnvironmentUIType.CyberEnvironmentUiType;
                    break;
            }
        }
    }

    // 1.0's logo over the main menu: the background scenes draw it on the planes decal_plane (mm_eftlogo) and
    // decal_plane_pve; 0.16's textures say BETA TESTING over it (eft_logo_beta), 1.0's do not (eft_logo, ui/eft_logo.png).
    // Swapped on each background scene as it comes up.
    internal class LogoSwap : MonoBehaviour
    {
        private static readonly Dictionary<string, string> Logos = new Dictionary<string, string>
        {
            ["eft_logo_beta"] = "eft_logo",
            ["eft_logo_beta_ulti-edition"] = "eft_logo_ulti-edition",
        };

        private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();
        private Object _seen;

        private void Update()
        {
            if (Time.frameCount % 30 != 0 || !Interface10Plugin.Logo10.Value || !MonoBehaviourSingleton<EnvironmentUI>.Instantiated)
            {
                return;
            }

            var root = Traverse.Create(MonoBehaviourSingleton<EnvironmentUI>.Instance).Field<EnvironmentUIRoot>("_currentEnvironment").Value;
            if (root == null || root == _seen)
            {
                return;
            }

            _seen = root;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    var texture = material != null ? material.mainTexture : null;
                    if (texture != null && Logos.TryGetValue(texture.name, out var file) && Load(file) is Texture2D logo)
                    {
                        material.mainTexture = logo;
                    }
                }
            }
        }

        private static Texture2D Load(string name)
        {
            if (Loaded.TryGetValue(name, out var texture))
            {
                return texture;
            }

            var path = System.IO.Path.Combine(Interface10Plugin.Dir, "ui", name + ".png");
            if (System.IO.File.Exists(path))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
                if (!texture.LoadImage(System.IO.File.ReadAllBytes(path)))
                {
                    texture = null;
                }
            }

            Loaded[name] = texture;
            return texture;
        }
    }
}
