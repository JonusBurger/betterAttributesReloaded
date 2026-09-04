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
    /// moveSpeed = baseMoveSpeed * (1 + MovementSpeedEnduranceBonusPerPoint * ENDURANCE).
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero (companions,
    /// lords, ...) - matches the predecessor mod's reference shape
    /// (Reference/MCMSettings.cs, MovementBonus*) for the Enabled/Player Only/Bonus
    /// settings and the 2%-per-point scaling value. The attribute is Endurance here per
    /// this session's explicit instruction - the reference file's own default for this
    /// group is actually Control (`selectedIndex: 1`), not Endurance; don't copy that
    /// default back for this effect (same situation as `ReloadSpeedControlPatch` before
    /// it, where the reference's own default attribute didn't match what was asked for).
    ///
    /// `UpdateAgentStats(Agent, AgentDrivenProperties)` - same method
    /// `ReloadSpeedControlPatch` patches, already confirmed via reflection to exist with
    /// an identical signature on all three concrete `AgentStatCalculateModel`
    /// implementations (see CLAUDE.md "Architecture gotchas"), so this patches all three
    /// too, unlike the predecessor mod's reference (land/Sandbox only).
    ///
    /// Only Heroes expose an Endurance value (Hero.GetAttributeValue); regular troops and
    /// ship crews have no equivalent accessor, so this only applies to hero agents.
    /// </summary>
    internal static class MovementSpeedEndurancePatch
    {
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.UpdateAgentStats))]
        [HarmonyPostfix]
        public static void SandboxPostfix(Agent agent, AgentDrivenProperties agentDrivenProperties) => Apply(agent, agentDrivenProperties);

        [HarmonyPatch(typeof(NavalAgentStatCalculateModel), nameof(NavalAgentStatCalculateModel.UpdateAgentStats))]
        [HarmonyPostfix]
        public static void NavalPostfix(Agent agent, AgentDrivenProperties agentDrivenProperties) => Apply(agent, agentDrivenProperties);

        [HarmonyPatch(typeof(NavalCustomBattleAgentStatCalculateModel), nameof(NavalCustomBattleAgentStatCalculateModel.UpdateAgentStats))]
        [HarmonyPostfix]
        public static void NavalCustomBattlePostfix(Agent agent, AgentDrivenProperties agentDrivenProperties) => Apply(agent, agentDrivenProperties);

        private static void Apply(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            try
            {
                // Cheapest checks first, before ever touching the MCM settings singleton -
                // this runs for every agent, every stat update, and only Heroes are
                // affected. See CLAUDE.md "Architecture gotchas".
                if (agent == null || agentDrivenProperties == null || !agent.IsHero)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.MovementSpeedEnduranceBonusEnabled)
                    return;

                if (settings.MovementSpeedEnduranceBonusPlayerOnly && !agent.IsMainAgent)
                    return;

                Hero? hero = (agent.Character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return;

                int endurance = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance);
                float factor = 1f + settings.MovementSpeedEnduranceBonusPerPoint * endurance;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                agentDrivenProperties.MaxSpeedMultiplier *= factor;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"MovementSpeedEndurancePatch.Apply threw: {e}");
            }
        }
    }
}
