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
    /// influenceGain = baseInfluenceGain * (1 + InfluenceIntelligenceBonusPerPoint * INTELLIGENCE).
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero party leader
    /// (companions, lords, ...) - matches the predecessor mod's reference shape
    /// (Reference/MCMSettings.cs, InfluenceBonus*), which already defaulted this exact
    /// bonus to the Intelligence attribute.
    ///
    /// IMPORTANT - the predecessor mod's own reference code (from an older game version)
    /// does NOT compile against the installed game (v1.4.8) as-is:
    /// `DefaultBattleRewardModel.CalculateInfluenceGain` gained two extra parameters
    /// since then (`influenceMultiplierForWinnerSide`, `includeDescriptions`) - confirmed
    /// via reflection. Harmony postfixes can omit trailing/unused parameters by matching
    /// the ones you *do* declare by name, so this only needs `winnerParty`,
    /// `includeDescriptions`, and `__result` - but don't copy the old 3-parameter
    /// signature verbatim for a similar effect without re-checking it first (see
    /// CLAUDE.md "Architecture gotchas").
    ///
    /// Only Heroes expose an Intelligence value (Hero.GetAttributeValue); regular
    /// (non-hero) party leaders don't exist in the base game, but the null-check on
    /// `LeaderHero` covers any edge case (e.g. a leaderless party) regardless.
    /// </summary>
    [HarmonyPatch(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.CalculateInfluenceGain))]
    internal static class InfluenceIntelligencePatch
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
                if (settings == null || !settings.InfluenceIntelligenceBonusEnabled)
                    return;

                if (settings.InfluenceIntelligenceBonusPlayerOnly && !leaderHero.IsHumanPlayerCharacter)
                    return;

                int intelligence = leaderHero.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
                float factor = settings.InfluenceIntelligenceBonusPerPoint * intelligence;
                if (float.IsNaN(factor) || float.IsInfinity(factor))
                    return;

                __result.AddFactor(factor, includeDescriptions ? new TextObject("Intelligence Bonus") : null);
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"InfluenceIntelligencePatch.Postfix threw: {e}");
            }
        }
    }
}
