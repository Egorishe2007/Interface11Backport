using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using HarmonyLib;

namespace Interface10
{
    // 1.2's skills list on the character screen: without the skills that do nothing (0.16 lists them locked) — first aid,
    // field medicine, night and silent operations, auctions, clean operations, shadow connections, task performance.
    // SkillManager.DisplayList is only read by the skills list (SkillContainer.Show), so it is filtered once it is made.
    internal static class Skills10
    {
        private static readonly HashSet<ESkillId> Gone = new HashSet<ESkillId>
        {
            ESkillId.FirstAid, ESkillId.FieldMedicine, ESkillId.NightOps, ESkillId.SilentOps,
            ESkillId.Auctions, ESkillId.Cleanoperations, ESkillId.Shadowconnections, ESkillId.Taskperformance,
        };

        private static readonly FieldInfo List = AccessTools.Field(typeof(SkillManager), nameof(SkillManager.DisplayList));

        [HarmonyPatch]
        internal static class ManagerPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SkillManager));

            [HarmonyPostfix]
            private static void Postfix(SkillManager __instance)
            {
                if (!Interface10Plugin.SkillsList.Value || __instance.DisplayList == null)
                {
                    return;
                }

                try
                {
                    // the field is readonly: set through reflection, once per skill manager
                    List.SetValue(__instance, __instance.DisplayList.Where(s => s == null || !Gone.Contains(s.Id)).ToArray());
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Skills list: {ex}");
                }
            }
        }
    }
}
