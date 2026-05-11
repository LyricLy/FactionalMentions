using SML;
using HarmonyLib;
using Mentions;
using Mentions.Providers;
using Server.Shared.State;
using Server.Shared.Extensions;
using Services;
using System.Collections.Generic;
using Game.Interface;
using Home.Shared;
using System;
using Mentions.UI;

namespace FactionalMentions;

[Mod.SalemMod]
public class FactionalMentions
{
    const int MaxCandidates = 75;

    public static void Start()
    {
        Harmony.CreateAndPatchAll(typeof(FactionalMentions));
    }

    [HarmonyPatch(typeof(MentionPanel), "HandleCandidatesUpdated")]
    [HarmonyPrefix]
    public static void TruncateCandidates(List<MentionInfo> candidates)
    {
        if (candidates.Count > MaxCandidates)
        {
            candidates.RemoveRange(MaxCandidates, candidates.Count - MaxCandidates);
        }
    }

    [HarmonyPatch(typeof(SharedMentionsProvider), "Build")]
    [HarmonyPostfix]
    public static void BuildAdvancedRoleMentions(SharedMentionsProvider __instance, RebuildMentionTypesFlag rebuildMentionTypesFlag)
    {
        if (!rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.ROLES))
        {
            return;
        }

        var list = Service.Game.Roles.roleInfos.ShallowCopy();
        list.Sort((RoleInfo a, RoleInfo b) => string.Compare(a.role.ToDisplayString(), b.role.ToDisplayString(), StringComparison.Ordinal));

        List<FactionType> allFactions = new((FactionType[])Enum.GetValues(typeof(FactionType)));
        if (ModStates.IsEnabled("curtis.tuba.better.tos2") && BetterTOS2.BTOSInfo.IS_MODDED)
        {
            for (int id = 33; id < 45; id++)
            {
                allFactions.Add((FactionType)id);
            }
        }

        var priority = 1000;
        foreach (RoleInfo roleInfo in list)
        {
            Role role = roleInfo.role;
            if (role.IsModifierCard() || role == Role.NONE)
            {
                continue;
            }

            var id = (int)role;
            var fullName = role.ToDisplayString();
            var shortName = role.ToShortenedDisplayString();

            foreach (FactionType faction in allFactions)
            {
                if ((faction is FactionType.NONE or FactionType.UNKNOWN or FactionType.FACTION_COUNT) || faction == role.GetFaction())
                {
                    continue;
                }
                var factionName = faction.ToDisplayString();
                var encoded = $"[[#{id},{(int)faction}]]";
                var fullMatch = $"#{fullName}-{factionName}";

                // this is how the base game creates MentionInfos for advanced mentions.
                // normally it does this lazily whenever it sees one used, but we need them all right now.
                // use the original method anyway to preserve the original behaviour, especially since FancyUI patches it.
                __instance.ProcessAdvancedRoleMention(MentionsProvider.RoleRegex.Match(encoded), encoded, encoded);

                // get the MentionInfo it just created. the method above doesn't add a humanText field, so do it ourselves
                MentionInfo mentionInfo = __instance.MentionInfos.Last();
                mentionInfo.humanText = fullMatch.ToLower();

                __instance.MentionTokens.Add(new MentionToken
                {
                    mentionTokenType = MentionToken.MentionTokenType.ROLE,
                    match = fullMatch,
                    mentionInfo = mentionInfo,
                    priority = priority,
                });
                __instance.MentionTokens.Add(new MentionToken
                {
                    mentionTokenType = MentionToken.MentionTokenType.ROLE,
                    match = $"#{shortName}-{factionName}",
                    mentionInfo = mentionInfo,
                    priority = priority,
                });
                priority++;
            }
        }
    }
}
