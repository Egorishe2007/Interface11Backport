using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace Interface10Server;

/// <summary>
/// The Unheard Edition's clothing of both sides at Ragman (EFT 1.2's BEAR/USEC toggles in Interface10's clothing
/// screen). SPT gives a character only the suits of its side, whatever side the client asks for in
/// /client/trading/customization/&lt;trader&gt;/&lt;bear|usec&gt;/offers. For an Unheard Edition profile: the trader's suits
/// of both sides count as its offers (so they can be bought and worn), and the offers list answers the side asked for.
/// Other editions get the same with the Commando's "spt clothing on".
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 2)]
public class ClothingSides(TemplateTable templateTable, TradersTable tradersTable, ProfileHelper profileHelper, ISptLogger<ClothingSides> logger) : IOnLoad
{
    private const string Unheard = "unheard_edition";
    private static TemplateTable? _templates;
    private static TradersTable? _traders;
    private static ProfileHelper? _profiles;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _templates = templateTable;
        _traders = tradersTable;
        _profiles = profileHelper;
        var harmony = new Harmony("com.grifm0le.interface11backport.clothingsides");
        var suits = AccessTools.Method(typeof(CustomizationController), nameof(CustomizationController.GetTraderSuits));
        var offers = AccessTools.Method(typeof(CustomizationCallbacks), nameof(CustomizationCallbacks.GetTraderSuits));
        if (suits == null || offers == null)
        {
            logger.Warning("[Interface11Backport] SPT's trader clothing methods not found: no clothing of both sides for the Unheard Edition");
            return Task.CompletedTask;
        }

        harmony.Patch(suits, postfix: new HarmonyMethod(typeof(ClothingSides), nameof(SuitsPostfix)));
        harmony.Patch(offers, prefix: new HarmonyMethod(typeof(ClothingSides), nameof(OffersPrefix)));
        logger.Info("[Interface11Backport] Unheard Edition: clothing of both sides at the traders");
        return Task.CompletedTask;
    }

    // The Unheard Edition, or a character given both sides with the Commando's "spt clothing on" (ClothingCommand).
    internal static bool Both(MongoId sessionId)
    {
        var pmc = _profiles?.GetPmcProfile(sessionId);
        return string.Equals(pmc?.Info?.GameVersion, Unheard, StringComparison.OrdinalIgnoreCase) || ClothingCommand.Enabled(sessionId);
    }

    // Every suit of the trader that exists, both sides, for the Unheard Edition (buying checks the offer against these).
    private static void SuitsPostfix(MongoId traderId, MongoId sessionId, ref List<Suit> __result)
    {
        if (!Both(sessionId) || _traders == null || _templates?.Customization == null)
        {
            return;
        }

        var all = _traders.GetTrader(traderId)?.Suits;
        if (all != null)
        {
            __result = all.Where(s => _templates.Customization.ContainsKey(s.SuiteId)).ToList();
        }
    }

    // The offers list of the side in the url ("bear"/"usec"), for the Unheard Edition.
    private static bool OffersPrefix(CustomizationCallbacks __instance, string url, MongoId sessionID, ref ValueTask<string> __result)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var at = Array.IndexOf(parts, "customization");
        if (at < 0 || parts.Length < at + 4 || !Both(sessionID) || _templates?.Customization == null)
        {
            return true;
        }

        var side = parts[at + 2];
        if (!string.Equals(side, "bear", StringComparison.OrdinalIgnoreCase) && !string.Equals(side, "usec", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var controller = Traverse.Create(__instance).Field("<customizationController>P").GetValue<CustomizationController>();
        var http = Traverse.Create(__instance).Field("<httpResponseUtil>P").GetValue<HttpResponseUtil>();
        if (controller == null || http == null)
        {
            return true;
        }

        var suits = controller.GetTraderSuits((MongoId)parts[at + 1], sessionID)
            .Where(s => _templates.Customization.TryGetValue(s.SuiteId, out var item)
                        && item.Properties?.Side != null
                        && item.Properties.Side.Any(x => string.Equals(x, side, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        __result = new ValueTask<string>(http.GetBody(suits));
        return false;
    }
}
