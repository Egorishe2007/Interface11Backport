using System;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Interface10
{
    // 1.2's "NOT FOR SALE ON THE FLEA MARKET" cell on an item's description (inspect window and Handbook, both are
    // ItemSpecificationPanel): one of the compact characteristics, the flea market's two arrows and the text, no value.
    // 0.16 has no such attribute: one is put in front of the item's attributes while the panel builds its cells, so the
    // game makes the cell itself (and drops it with the others), and taken out again right after.
    internal static class NotForFlea10
    {
        // an id no attribute of the game has: no icon, no name, no compare partner of its own
        private const EItemAttributeId Id = (EItemAttributeId)7001;
        private static readonly AccessTools.FieldRef<ItemSpecificationPanel, Item> PanelItem =
            AccessTools.FieldRefAccess<ItemSpecificationPanel, Item>("_item");
        private static readonly AccessTools.FieldRef<CompactCharacteristicPanel, ItemAttribute> Attribute =
            AccessTools.FieldRefAccess<CompactCharacteristicPanel, ItemAttribute>("ItemAttribute");

        [HarmonyPatch(typeof(ItemSpecificationPanel), nameof(ItemSpecificationPanel.RecreateAttributeBars))]
        internal static class BarsPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ItemSpecificationPanel __instance, out ItemAttribute __state)
            {
                __state = null;
                if (!Interface10Plugin.NotForFlea.Value)
                {
                    return;
                }

                var item = PanelItem(__instance);
                if (item?.Template == null || item.Template.CanSellOnRagfair || item.Attributes == null)
                {
                    return;
                }

                __state = new ItemAttribute(Id)
                {
                    Name = "ragfair/NotForSell",
                    DisplayNameFunc = () => Lang.Get("item.not_for_flea", "Cannot be listed on Flea Market"),
                    DisplayType = () => EItemAttributeDisplayType.Compact,
                    IsTextValueDisplayable = false,
                };
                item.Attributes.Insert(0, __state);
            }

            [HarmonyFinalizer]
            private static void Finalizer(ItemSpecificationPanel __instance, ItemAttribute __state)
            {
                if (__state != null)
                {
                    PanelItem(__instance)?.Attributes?.Remove(__state);
                }
            }
        }

        // The game's icon lookup: the flea market's arrows for this id (else it logs "There is no sprite with id 7001").
        [HarmonyPatch(typeof(StaticIcons), nameof(StaticIcons.GetAttributeIcon))]
        internal static class LookupPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Enum id, ref Sprite __result)
            {
                if (!(id is EItemAttributeId attribute) || attribute != Id)
                {
                    return true;
                }

                __result = Ui.FileSprite("icon_flea_market");
                return false;
            }
        }

        // The cell's icon: 1.2's flea market arrows at 1.2's size (native size would be 28x28).
        [HarmonyPatch(typeof(CompactCharacteristicPanel), nameof(CompactCharacteristicPanel.Show))]
        internal static class IconPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CompactCharacteristicPanel __instance)
            {
                if (__instance.Icon == null || !(Attribute(__instance)?.Id is EItemAttributeId id) || id != Id)
                {
                    return;
                }

                try
                {
                    __instance.Icon.sprite = Ui.FileSprite("icon_flea_market");
                    __instance.Icon.rectTransform.sizeDelta = new Vector2(14f, 14f);
                    __instance.Icon.gameObject.SetActive(__instance.Icon.sprite != null);
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Not for flea icon: {ex}");
                }
            }
        }
    }
}
