using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// meleeDamage = baseMeleeDamage * (1 + MeleeDamageVigorBonusPerPoint * VIGOR).
    ///
    /// A reference implementation was provided (predecessor mod's
    /// `MissionCombatMechanicsHelperPatch.ComputeBlowDamage`), but this patch targets
    /// `ComputeBlowMagnitude` instead - the same deviation `RangedDamageControlPatch`
    /// already made, for the same reason (see its own doc comment and bugHistory.md
    /// 2026-09-01 "REWRITTEN"): the *first* Ranged Damage implementation patched
    /// `ComputeBlowDamage` directly and froze the game during land combat - no crash
    /// dump existed for a hang, so the exact mechanism was never confirmed. Given that
    /// unresolved history on this exact method in this exact mod/machine, Melee Damage
    /// reuses the already-proven-safe `ComputeBlowMagnitude` hook (an earlier, upstream
    /// stage of the same damage pipeline - see CLAUDE.md "Architecture gotchas") with an
    /// explicit `IsMeleeWeapon` check instead of `RangedDamageControlPatch`'s
    /// `IsRangedWeapon` - confirmed via reflection (v1.4.8) that
    /// `WeaponComponentData.IsMeleeWeapon` exists alongside `IsRangedWeapon`, and that
    /// `ComputeBlowMagnitude`'s signature is unchanged from the version
    /// `RangedDamageControlPatch` already confirmed. (`ComputeBlowDamage` itself was
    /// also re-checked via reflection here and still matches the reference signature
    /// exactly - the freeze was never pinned on a signature mismatch, so this isn't a
    /// case of the old bug being "fixed" by verifying it; it's still avoided by not
    /// patching that method at all, out of caution.)
    ///
    /// Also deviates from the reference's damage formula: the old mod computes
    /// `dmgBonus = (int)(bonusPerPoint * VIGOR + 1)` and multiplies the *integer*
    /// inflicted damage by it. With the default 2%/point and this project's usual
    /// attribute range (up to ~30), `bonusPerPoint * VIGOR` never reaches 1.0 until the
    /// cumulative bonus hits 100%, so the `(int)` cast truncates it to 0 and the bonus
    /// silently does nothing below that threshold - this looks like an unintentional
    /// truncation bug in the old mod, not deliberate design. Every other percentage-
    /// style effect in this project (Ranged Damage, Reload Speed, Movement Speed, ...)
    /// instead scales a `float` multiplier smoothly - `specialMagnitude *= 1 +
    /// bonusPerPoint * VIGOR` - so Melee Damage follows that same established
    /// convention rather than reproducing the truncation.
    ///
    /// Scope: three-way "Applies To" dropdown (Player Only / Player's Clan / All
    /// Lords), explicitly requested for this effect - same shape and Clan-membership
    /// rule as `PrisonerRecruitmentSocialPatch`'s scope dropdown ("Player's Clan"
    /// includes companions serving under the player's clan). Checked via hero identity
    /// (`hero == Hero.MainHero`, `hero.Clan == Hero.MainHero.Clan`) rather than
    /// `AttackInformation.IsAttackerAIControlled` (which the reference implementation
    /// used for its plain "Player Only" bool) - identity is required for the
    /// clan-membership tier and is at least as correct for the player-only tier.
    ///
    /// Only Heroes expose a Vigor value (Hero.GetAttributeValue); regular troops have
    /// no equivalent accessor, so this only ever applies to hero agents.
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "ComputeBlowMagnitude")]
    internal static class MeleeDamageVigorPatch
    {
        [HarmonyPostfix]
        public static void Postfix(in AttackInformation attackInformation, bool cancelDamage, ref float specialMagnitude)
        {
            try
            {
                // Cheapest checks first, before ever touching the MCM settings singleton -
                // this runs for every hit in a mission, melee and ranged alike, and the
                // vast majority (regular troops, ranged hits) can never be affected.
                if (cancelDamage)
                    return;

                CharacterObject? attackerCharacter = attackInformation.AttackerAgentCharacter as CharacterObject;
                if (attackerCharacter == null || !attackerCharacter.IsHero)
                    return;

                WeaponComponentData? weapon = attackInformation.AttackerWeapon.CurrentUsageItem;
                if (weapon == null || !weapon.IsMeleeWeapon)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.MeleeDamageVigorBonusEnabled)
                    return;

                Hero? hero = attackerCharacter.HeroObject;
                if (hero == null)
                    return;

                Hero? mainHero = Hero.MainHero;
                bool applies = settings.MeleeDamageVigorScopeDropdown.SelectedIndex switch
                {
                    0 => hero == mainHero, // Player Only
                    1 => mainHero != null && hero.Clan != null && hero.Clan == mainHero.Clan, // Player's Clan
                    _ => true, // All Lords
                };
                if (!applies)
                    return;

                int vigor = hero.GetAttributeValue(DefaultCharacterAttributes.Vigor);
                float factor = 1f + settings.MeleeDamageVigorBonusPerPoint * vigor;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                specialMagnitude *= factor;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"MeleeDamageVigorPatch.Postfix threw: {e}");
            }
        }
    }
}
