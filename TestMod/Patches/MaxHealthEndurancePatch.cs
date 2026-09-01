using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// maxHealth = baseGameMaxHealth * (1 + MaxHealthEnduranceBonusPercent * ENDURANCE).
    ///
    /// REWRITTEN 2026-09-01 from a flat, additive bonus on the three mission-level
    /// AgentStatCalculateModel.GetEffectiveMaxHealth overrides (land/naval/naval-custom-
    /// battle - see CLAUDE.md "Architecture gotchas" for that history) to a percentage
    /// bonus on this single, campaign-level model, matching the predecessor mod's
    /// reference implementation. `DefaultCharacterStatsModel.MaxHitpoints(CharacterObject,
    /// bool) : ExplainedNumber` is the canonical source for a character's max HP - the
    /// same value shown on the character sheet and (per Bannerlord's well-documented
    /// wound/recovery mechanic, where a partially-recovered hero enters a mission below
    /// full health as a fraction of this same number) very likely what
    /// GetEffectiveMaxHealth derives its baseline from - confirmed via reflection to exist
    /// in the installed game (v1.4.8) with the predecessor mod's exact signature, but this
    /// specific relationship (does GetEffectiveMaxHealth read from MaxHitpoints, or
    /// compute independently?) could not be confirmed from metadata alone (method bodies
    /// aren't decompiled - see CLAUDE.md). If in-battle max health stops being boosted
    /// after this change while the character sheet number still goes up, that
    /// relationship was wrong and the old land/naval Harmony postfixes need to come back
    /// alongside this one, not instead of it.
    ///
    /// ExplainedNumber is a struct, and AddFactor(float, TextObject) is vanilla's own
    /// mechanism for a percentage-style modifier - using it here (rather than manually
    /// multiplying a float, as e.g. RangedDamageControlPatch does) means this bonus shows
    /// up properly in the game's own stat breakdown tooltips.
    ///
    /// Only Heroes expose an Endurance value (Hero.GetAttributeValue); regular
    /// (non-hero) characters have no equivalent accessor, so this only ever applies to
    /// hero characters (player, companions, lords, ...).
    /// </summary>
    [HarmonyPatch(typeof(DefaultCharacterStatsModel), nameof(DefaultCharacterStatsModel.MaxHitpoints))]
    internal static class MaxHealthEndurancePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref ExplainedNumber __result, CharacterObject character, bool includeDescriptions)
        {
            try
            {
                // Cheapest checks first, before ever touching the MCM settings singleton -
                // see CLAUDE.md "Architecture gotchas".
                if (character == null || !character.IsHero)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.MaxHealthEnduranceBonusEnabled)
                    return;

                if (settings.MaxHealthEnduranceBonusPlayerOnly && !character.IsPlayerCharacter)
                    return;

                Hero? hero = character.HeroObject;
                if (hero == null)
                    return;

                int endurance = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance);
                float factor = settings.MaxHealthEnduranceBonusPercent * endurance;
                if (float.IsNaN(factor) || float.IsInfinity(factor))
                    return;

                __result.AddFactor(factor, includeDescriptions ? new TextObject("Endurance Bonus") : null);
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"MaxHealthEndurancePatch threw: {e}");
            }
        }
    }
}
