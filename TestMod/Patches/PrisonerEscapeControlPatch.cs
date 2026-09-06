using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// Chance to prevent a captured lord's escape attempt entirely:
    /// preventChance = min(1, PrisonerEscapeControlBonusPerPoint * CONTROL).
    ///
    /// No predecessor-mod reference exists for this effect. Found the mechanic by
    /// reflecting on `TaleWorlds.CampaignSystem.dll` for identifiers containing "Escape"
    /// (see CLAUDE.md "Architecture gotchas") - the only match in the whole assembly is
    /// `TaleWorlds.CampaignSystem.CampaignBehaviors.PrisonerReleaseCampaignBehavior.ApplyEscapeChanceToExceededPrisoners(CharacterObject, MobileParty)`,
    /// confirmed via reflection (v1.4.8): `private`, non-static, `void` - this appears to
    /// be the *only* place a captured hero's escape chance is rolled and applied at all,
    /// matching the task's own observation that only captured lords can escape.
    ///
    /// Since the method is `void` (no chance value or `ExplainedNumber` to scale), this
    /// is a Harmony **prefix** that probabilistically skips the whole call - the same
    /// "skip the original on a successful roll" pattern `SliceThroughMomentumPatch` uses
    /// on `UpdateMomentumRemaining`. On a successful prevention roll, that escape check
    /// simply doesn't happen this cycle - not a modification of an underlying chance
    /// value (there isn't one to modify), but statistically equivalent to reducing it
    /// over repeated calls.
    ///
    /// Hard player-only, no toggle to extend to lords at all - explicitly requested for
    /// this effect (see Settings/BetterAttributesSettings.cs). Gated on
    /// `capturerParty.IsMainParty`, i.e. this only ever affects prisoners held in the
    /// player's own party, not any other lord's.
    /// </summary>
    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "ApplyEscapeChanceToExceededPrisoners")]
    internal static class PrisonerEscapeControlPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CharacterObject character, MobileParty capturerParty)
        {
            try
            {
                // Cheapest checks first, before ever touching the MCM settings singleton.
                if (character == null || !character.IsHero)
                    return true;

                if (capturerParty == null || !capturerParty.IsMainParty)
                    return true;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.PrisonerEscapeControlBonusEnabled)
                    return true;

                Hero? mainHero = Hero.MainHero;
                if (mainHero == null)
                    return true;

                int control = mainHero.GetAttributeValue(DefaultCharacterAttributes.Control);
                float preventChance = settings.PrisonerEscapeControlBonusPerPoint * control;
                if (float.IsNaN(preventChance) || float.IsInfinity(preventChance) || preventChance <= 0f)
                    return true;

                preventChance = Math.Min(preventChance, 1f);
                if (MBRandom.RandomFloat < preventChance)
                {
                    // Prevented - skip the vanilla escape-chance application entirely.
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"PrisonerEscapeControlPatch.Prefix threw: {e}");
                return true; // fail safe: let vanilla run
            }
        }
    }
}
