using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// rangedDamage = baseRangedDamage * (1 + RangedDamageControlBonusPerPoint * CONTROL).
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero (companions,
    /// lords, ...) - see Settings/Bonuses/Ranged Damage, "Player Only". Matches the
    /// predecessor mod's reference shape (Reference/MCMSettings.cs, RngDmgBonus*), which
    /// already defaulted this exact bonus to the Control attribute.
    ///
    /// REWRITTEN 2026-09-01: the first version patched
    /// `MissionCombatMechanicsHelper.ComputeBlowDamage` (the shared final int-damage step
    /// for any hit, scoped to ranged via the hit weapon's `IsRangedWeapon`). That caused a
    /// freeze during land combat - no crash dump this time to root-cause it against, so
    /// the exact mechanism isn't confirmed. The project owner then found the predecessor
    /// mod's actual implementation, which instead patches
    /// `MissionCombatMechanicsHelper.ComputeBlowMagnitude` - confirmed via reflection to
    /// still exist in the installed game (v1.4.8) with the exact signature the old mod
    /// uses. This is an earlier stage of the same pipeline (produces the pre-armor
    /// `specialMagnitude` that `ComputeBlowMagnitudeMelee`/`ComputeBlowMagnitudeMissile`
    /// feed into `ComputeBlowDamage` with), so scaling it here is a smaller, more
    /// upstream intervention than rewriting the final integer damage. Whether that's what
    /// actually avoids the freeze is unconfirmed - this needs its own in-game test - but
    /// it's a proven, working reference implementation rather than another guess.
    ///
    /// Unlike the old mod's version, this keeps an explicit ranged-only check
    /// (`AttackerWeapon.CurrentUsageItem.IsRangedWeapon`) rather than relying on `IsHero`
    /// alone - `ComputeBlowMagnitude` has no "Melee"/"Missile" suffix, so it likely runs
    /// for both hit types (like `ComputeBlowDamage` did), and the task this effect
    /// implements is specifically ranged damage, not all damage.
    ///
    /// Only Heroes expose a Control value (Hero.GetAttributeValue); regular troops have no
    /// equivalent accessor, so this only ever applies to hero agents.
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "ComputeBlowMagnitude")]
    internal static class RangedDamageControlPatch
    {
        [HarmonyPostfix]
        public static void Postfix(in AttackInformation attackInformation, bool cancelDamage, ref float specialMagnitude)
        {
            try
            {
                // Cheapest checks first, before ever touching the MCM settings singleton -
                // this runs for every hit in a mission, melee and ranged alike, and the
                // vast majority (regular troops, melee hits) can never be affected.
                if (cancelDamage)
                    return;

                CharacterObject? attackerCharacter = attackInformation.AttackerAgentCharacter as CharacterObject;
                if (attackerCharacter == null || !attackerCharacter.IsHero)
                    return;

                WeaponComponentData? weapon = attackInformation.AttackerWeapon.CurrentUsageItem;
                if (weapon == null || !weapon.IsRangedWeapon)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.RangedDamageControlBonusEnabled)
                    return;

                if (settings.RangedDamageControlBonusPlayerOnly && attackInformation.IsAttackerAIControlled)
                    return;

                Hero? hero = attackerCharacter.HeroObject;
                if (hero == null)
                    return;

                int control = hero.GetAttributeValue(DefaultCharacterAttributes.Control);
                float factor = 1f + settings.RangedDamageControlBonusPerPoint * control;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                specialMagnitude *= factor;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"RangedDamageControlPatch.Postfix threw: {e}");
            }
        }
    }
}
