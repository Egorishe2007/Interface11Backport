using System;
using System.Linq;
using BepInEx.Bootstrap;

namespace Interface10
{
    // Other mods that bring the same screens: ours step aside for them instead of building a second copy.
    // Looked up once the game runs, when every plugin is loaded.
    internal static class Compat
    {
        private static bool? _hj;
        private static bool? _skillsExtended;

        // Hj's Improved Customization UI: its own CUSTOMIZATION tab on the character screen (face, voice, clothing) and
        // Ragman's BEAR/USEC toggles and filter. With both mods, two extra tabs broke the character screen's tab row
        // (Hj lays the tabs out itself, with the game's TabElements switched off) and Ragman's header had two of each:
        // our CHARACTER tab and Ragman's changes stay off.
        internal static bool HjCustomization => Found(ref _hj, "com.hj.improvedcustomizationui",
            "Hj's Improved Customization UI found: its CUSTOMIZATION tab and Ragman's toggles are used, ours stay off");

        // Skills Extended gives first aid, field medicine, silent operations and shadow connections a use again: the
        // skills list keeps them (Skills10).
        internal static bool SkillsExtended => Found(ref _skillsExtended, "com.cj.skillsextended",
            "Skills Extended found: first aid, field medicine, silent operations and shadow connections stay in the skills list");

        private static bool Found(ref bool? found, string guid, string message)
        {
            if (found == null)
            {
                found = Chainloader.PluginInfos.Keys.Any(k => string.Equals(k, guid, StringComparison.OrdinalIgnoreCase));
                if (found.Value)
                {
                    Interface10Plugin.Log.LogInfo(message);
                }
            }

            return found.Value;
        }
    }
}
