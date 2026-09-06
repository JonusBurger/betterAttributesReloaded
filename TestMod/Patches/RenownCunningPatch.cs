using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// renownGain = baseRenownGain * (1 + RenownCunningBonusPerPoint * CUNNING).
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero party leader
    /// (companions, lords, ...) - matches the predecessor mod's reference shape
    /// (Reference/MCMSettings.cs, RenownBonus*), which already defaulted this exact
    /// bonus to the Cunning attribute.
    ///
    /// Same situation as `InfluenceIntelligencePatch`, same source file
    /// (`Reference/PatchExample.cs.txt`): the predecessor mod's reference signature for
    /// this method is stale. `DefaultBattleRewardModel.CalculateRenownGain` gained the
    /// same two extra parameters since then (`renownMultiplierForWinnerSide`,
    /// `includeDescriptions`) - confirmed via reflection against the installed game
    /// (v1.4.8) before writing this, per CLAUDE.md "Architecture gotchas" (a signature
    /// can drift on a per-method basis even within a file where other methods are
    /// still correct). Only declaring the parameters actually needed.
    ///
    /// Only one concrete `BattleRewardModel` implementation, so a single patch covers it.
    /// </summary>
    [HarmonyPatch(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.CalculateRenownGain))]
    internal static class RenownCunningPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PartyBase winnerParty, bool includeDescriptions, ref ExplainedNumber __result)
        {
            try
            {
                Hero? leaderHero = winnerParty?.LeaderHero;
                if (leaderHero == null)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.RenownCunningBonusEnabled)
                    return;

                if (settings.RenownCunningBonusPlayerOnly && !leaderHero.IsHumanPlayerCharacter)
                    return;

                int cunning = leaderHero.GetAttributeValue(DefaultCharacterAttributes.Cunning);
                float factor = settings.RenownCunningBonusPerPoint * cunning;
                if (float.IsNaN(factor) || float.IsInfinity(factor))
                    return;

                __result.AddFactor(factor, includeDescriptions ? new TextObject("Cunning Bonus") : null);
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"RenownCunningPatch.Postfix threw: {e}");
            }
        }
    }
}
