using System;
using HarmonyLib;
using NavalDLC.ComponentInterfaces;
using NavalDLC.GameComponents;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// maxHealth = baseGameMaxHealth + MaxHealthEnduranceBonus * ENDURANCE.
    ///
    /// The game has no single overridable "get max health" method: `GetEffectiveMaxHealth`
    /// is declared abstract on TaleWorlds.MountAndBlade.AgentStatCalculateModel and each
    /// mission type registers its own concrete implementation, so Harmony has to patch
    /// each concrete override individually:
    ///   - SandboxAgentStatCalculateModel  - land missions (base game)
    ///   - NavalAgentStatCalculateModel    - naval missions (War Sails)
    ///   - NavalCustomBattleAgentStatCalculateModel - naval custom battles (War Sails)
    /// All three were confirmed against the installed game + NavalDLC assemblies (see
    /// CLAUDE.md). War Sails is a required dependency for this mod, but land missions
    /// still use SandboxAgentStatCalculateModel even with the DLC installed, so that one
    /// is kept too.
    ///
    /// Only Heroes expose an Endurance value (Hero.GetAttributeValue); regular troops and
    /// ship crews have no individually-tracked attributes, so this bonus only applies to
    /// hero agents (player, companions, lords, ...).
    /// </summary>
    internal static class MaxHealthEndurancePatch
    {
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.GetEffectiveMaxHealth))]
        [HarmonyPostfix]
        public static void SandboxPostfix(Agent agent, ref float __result) => Apply(agent, ref __result);

        [HarmonyPatch(typeof(NavalAgentStatCalculateModel), nameof(NavalAgentStatCalculateModel.GetEffectiveMaxHealth))]
        [HarmonyPostfix]
        public static void NavalPostfix(Agent agent, ref float __result) => Apply(agent, ref __result);

        [HarmonyPatch(typeof(NavalCustomBattleAgentStatCalculateModel), nameof(NavalCustomBattleAgentStatCalculateModel.GetEffectiveMaxHealth))]
        [HarmonyPostfix]
        public static void NavalCustomBattlePostfix(Agent agent, ref float __result) => Apply(agent, ref __result);

        private static void Apply(Agent agent, ref float __result)
        {
            try
            {
                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.MaxHealthEnduranceBonusEnabled)
                    return;

                Hero? hero = (agent?.Character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return;

                if (settings.MaxHealthEnduranceBonusPlayerOnly && !hero.IsHumanPlayerCharacter)
                    return;

                int endurance = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance);
                __result += settings.MaxHealthEnduranceBonus * endurance;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"MaxHealthEndurancePatch threw: {e}");
            }
        }
    }
}
