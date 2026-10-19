using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace Interface10Server;

/// <summary>
/// EFT 1.2's "Reward in the next tasks" for Interface10's task view: for every task, the item and trader unlock rewards
/// of the tasks that need it done (or failed) before they open. The client only gets the tasks the player can already
/// see, so it can't find the next ones itself. GET /interface10/quests/next answers
/// { "&lt;task id&gt;": [ rewards as in the task templates ] }.
/// </summary>
[Injectable]
public class NextRewardsRouter(JsonUtil jsonUtil, TemplateTable templates) : StaticRouter(jsonUtil,
[
    new RouteAction<EmptyRequestData>("/interface10/quests/next",
        (url, info, sessionId, output, cancellationToken) => new ValueTask<string>(Build(jsonUtil, templates))),
])
{
    // roubles, dollars, euros: shown with the task's own rewards, not as a reason to look forward
    private static readonly HashSet<string> Money = ["5449016a4bdc2d6f028b456f", "5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568"];
    private static string? _cached;

    private static string Build(JsonUtil json, TemplateTable templates)
    {
        if (_cached != null)
        {
            return _cached;
        }

        var next = new Dictionary<string, List<Reward>>();
        var quests = templates.Quests ?? new Dictionary<MongoId, Quest>();
        foreach (var quest in quests.Values)
        {
            var rewards = quest.Rewards != null && quest.Rewards.TryGetValue("Success", out var success) && success != null
                ? success.Where(Notable).ToList()
                : [];
            if (rewards.Count == 0 || quest.Conditions?.AvailableForStart == null)
            {
                continue;
            }

            foreach (var condition in quest.Conditions.AvailableForStart.Where(c => c.ConditionType == "Quest" && c.Target != null))
            {
                var targets = condition.Target!.IsList ? condition.Target.List ?? [] : [condition.Target.Item!];
                foreach (var before in targets.Where(t => !string.IsNullOrEmpty(t)))
                {
                    if (!next.TryGetValue(before, out var list))
                    {
                        next[before] = list = [];
                    }

                    list.AddRange(rewards.Where(r => !list.Contains(r)));
                }
            }
        }

        _cached = json.Serialize(next, false) ?? "{}";
        return _cached;
    }

    private static bool Notable(Reward reward)
    {
        if (reward.Unknown == true || reward.IsHidden == true)
        {
            return false;
        }

        // trader unlocks (the cart) and crafting schemes (the tools), as 1.2 shows them
        if (reward.Type is RewardType.AssortmentUnlock or RewardType.ProductionScheme)
        {
            return true;
        }

        if (reward.Type != RewardType.Item || reward.Items == null || reward.Items.Count == 0)
        {
            return false;
        }

        var root = reward.Items.FirstOrDefault(i => i.Id.ToString() == reward.Target) ?? reward.Items[0];
        return !Money.Contains(root.Template.ToString());
    }
}
