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
    /// unsteadyBeginTime = baseUnsteadyBeginTime * (1 + StabilityEnduranceBonusPerPoint * ENDURANCE).
    /// A larger `WeaponUnsteadyBeginTime` means aim stays steady longer before starting to
    /// sway, i.e. more stability - matches the predecessor mod's target property exactly.
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero (companions,
    /// lords, ...) - matches the predecessor mod's reference shape
    /// (Reference/MCMSettings.cs, StabilityBonus*), which already defaulted this exact
    /// bonus to the Endurance attribute (no attribute-default deviation needed here,
    /// unlike Reload Speed/Movement Speed).
    ///
    /// `UpdateAgentStats(Agent, AgentDrivenProperties)` - the same method
    /// `ReloadSpeedControlPatch`/`MovementSpeedEndurancePatch` already patch, confirmed
    /// via reflection to exist with an identical signature on all three concrete
    /// `AgentStatCalculateModel` implementations (see CLAUDE.md "Architecture gotchas"),
    /// so this patches all three too - unlike the predecessor mod's reference, which only
    /// patches the land (Sandbox) one.
    ///
    /// Only Heroes expose an Endurance value (Hero.GetAttributeValue); regular troops and
    /// ship crews have no equivalent accessor, so this only applies to hero agents.
    /// </summary>
    internal static class StabilityEndurancePatch
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
                if (settings == null || !settings.StabilityEnduranceBonusEnabled)
                    return;

                if (settings.StabilityEnduranceBonusPlayerOnly && !agent.IsMainAgent)
                    return;

                Hero? hero = (agent.Character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return;

                int endurance = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance);
                float factor = 1f + settings.StabilityEnduranceBonusPerPoint * endurance;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                agentDrivenProperties.WeaponUnsteadyBeginTime *= factor;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"StabilityEndurancePatch.Apply threw: {e}");
            }
        }
    }
}
