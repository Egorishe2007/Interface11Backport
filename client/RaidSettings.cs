using EFT.Settings.Sound;
using HarmonyLib;

namespace Interface10
{
    // Every offline raid marks VoIP as "unavailable in offline mode", which puts a warning over the VoIP section
    // of the sound settings. There is nobody to talk to in SPT anyway, so the raid simply keeps VoIP as it is.
    [HarmonyPatch(typeof(SoundSettingsController), nameof(SoundSettingsController.VoipDisabledInLocalGame))]
    internal static class OfflineVoipPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Interface10Plugin.HideOfflineVoipWarning.Value;
        }
    }
}
