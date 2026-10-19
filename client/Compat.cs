using BepInEx.Bootstrap;

namespace Interface10
{
    // Other mods that bring the same screens: ours step aside for them instead of building a second copy.
    internal static class Compat
    {
        private static bool? _hj;

        // Hj's Improved Customization UI: its own CUSTOMIZATION tab on the character screen (face, voice, clothing) and
        // Ragman's BEAR/USEC toggles and filter. With both mods, two extra tabs broke the character screen's tab row
        // (Hj lays the tabs out itself, with the game's TabElements switched off) and Ragman's header had two of each:
        // our CHARACTER tab and Ragman's changes stay off. Looked up once the game runs, when every plugin is loaded.
        internal static bool HjCustomization
        {
            get
            {
                if (_hj == null)
                {
                    _hj = Chainloader.PluginInfos.ContainsKey("com.hj.improvedcustomizationui");
                    if (_hj.Value)
                    {
                        Interface10Plugin.Log.LogInfo("Hj's Improved Customization UI found: its CUSTOMIZATION tab and Ragman's toggles are used, ours stay off");
                    }
                }

                return _hj.Value;
            }
        }
    }
}
