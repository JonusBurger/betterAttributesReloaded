using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// Vigor-scaled "Slice Through": on a successful player melee hit, a chance
    /// (SliceThroughChancePerVigor * Vigor, capped at 100%) that the swing carries through
    /// to a second target - like a heavy axe (e.g. the Executioner Axe) cutting through
    /// more than one enemy.
    ///
    /// REWRITTEN 2026-09-01 from a MissionBehavior-based implementation (manual nearby-
    /// agent query + Agent.Health write) to this Harmony patch, based on how the
    /// predecessor mod ("BetterAttributePoints") actually implemented the same effect - see
    /// bugHistory.md 2026-09-01 for the full story and why the old approach was replaced,
    /// not just why this one was chosen.
    ///
    /// `MissionCombatMechanicsHelper.UpdateMomentumRemaining` is the real vanilla mechanic
    /// behind "heavy weapons cleave through multiple enemies": a swing carries momentum,
    /// each hit consumes some of it via this method, and once it's exhausted the swing
    /// can't reach a further target. Skipping this method (Harmony prefix returning
    /// `false`) on a successful proc leaves the swing's momentum untouched by the hit that
    /// just landed, letting the game's own collision system carry it into a second target -
    /// with its own, separately momentum-reduced damage calculation. That's the "reduced
    /// damage on the second hit" requirement, handled by the engine itself rather than a
    /// manually computed fraction.
    ///
    /// This is a plain Harmony patch on an existing method, not a MissionBehavior - no
    /// nearby-agent query, no manual Agent.Health write, no MissionBehaviorType bucket
    /// concern (see CLAUDE.md "Architecture gotchas"). Verified via reflection against the
    /// installed game (v1.4.8) that this method's signature still matches what the
    /// predecessor mod patched.
    ///
    /// Only Heroes expose an Endurance/Vigor value (Hero.GetAttributeValue); regular troops
    /// have no equivalent accessor, so this only applies to hero agents - and per this
    /// project's explicit decision, hard-locked to the player specifically (Agent.IsMainAgent),
    /// not a "Player Only" toggle (see CLAUDE.md "Effect default scope").
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "UpdateMomentumRemaining")]
    internal static class SliceThroughMomentumPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float momentumRemaining, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
        {
            try
            {
                if (attacker == null || !attacker.IsHero || !attacker.IsMainAgent)
                    return true; // run the original method normally

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.SliceThroughEnabled)
                    return true;

                // Only cutting melee hits "slice through" - not blunt/pierce.
                if (b.DamageType != DamageTypes.Cut)
                    return true;

                Hero? hero = (attacker.Character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return true;

                int vigor = hero.GetAttributeValue(DefaultCharacterAttributes.Vigor);
                float chance = Math.Min(settings.SliceThroughChancePerVigor * vigor, 1f);
                if (chance <= 0f || MBRandom.RandomFloat > chance)
                    return true;

                if (settings.SliceThroughNotify)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Your blow sliced through!"));
                }

                // Skip the original: momentumRemaining is left as-is, so this hit doesn't
                // cost the swing anything and it can carry through to another target.
                return false;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"SliceThroughMomentumPatch.Prefix threw: {e}");
                return true; // fail safe: let the original method run
            }
        }
    }
}
