using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Customization;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace Interface10Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.grifm0le.interface11backport";
    public string Name { get; init; } = "Grifm0le-Interface11Backport";
    public string Author { get; init; } = "Grifm0le";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

/// <summary>
/// The server side of Interface10's CHARACTER tab (EFT 1.2's face selection): the client changes the character's face
/// the way it changes the voice, with a CustomizationSet item event, here of the type "head". SPT handles dogTag, suite
/// and voice there and logs "Unhandled customisation type" for a head; this sets the profile's head instead, when the
/// head is one a character of that side can have (a player head of the side, as at character creation).
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class HeadCustomization(TemplateTable templateTable, ISptLogger<HeadCustomization> logger) : IOnLoad
{
    private static TemplateTable? _templates;
    private static ISptLogger<HeadCustomization>? _log;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _templates = templateTable;
        _log = logger;
        var target = AccessTools.Method(typeof(CustomizationController), nameof(CustomizationController.SetCustomisation));
        if (target == null)
        {
            logger.Warning("[Interface11Backport] CustomizationController.SetCustomisation not found: the face cannot be changed");
            return Task.CompletedTask;
        }

        new Harmony("com.grifm0le.interface11backport").Patch(target, prefix: new HarmonyMethod(typeof(HeadCustomization), nameof(Prefix)));
        logger.Info("[Interface11Backport] the character's face can be changed (CHARACTER tab)");
        return Task.CompletedTask;
    }

    // Takes the head options out of the request (SPT would only log them as unhandled) and applies them.
    private static void Prefix(CustomizationSetRequest request, PmcData pmcData)
    {
        var options = request?.Customizations;
        if (options == null || pmcData?.Customization == null)
        {
            return;
        }

        foreach (var option in options.Where(o => string.Equals(o.Type, "head", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            options.Remove(option);
            if (IsPlayerHead(option.Id, pmcData.Info?.Side))
            {
                pmcData.Customization.Head = option.Id;
                _log?.Info($"[Interface11Backport] face of {pmcData.Info?.Nickname} set to {option.Id}");
            }
            else
            {
                _log?.Warning($"[Interface11Backport] {option.Id} is not a face for {pmcData.Info?.Side}, not set");
            }
        }
    }

    private static bool IsPlayerHead(MongoId id, string? side)
    {
        if (_templates?.Customization == null || !_templates.Customization.TryGetValue(id, out var template) || template.Properties == null)
        {
            return false;
        }

        var properties = template.Properties;
        return properties.BodyPart == "Head" && properties.AvailableAsDefault &&
               side != null && properties.Side != null && properties.Side.Contains(side, StringComparer.OrdinalIgnoreCase);
    }
}
