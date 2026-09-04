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
    /// reloadSpeed = baseReloadSpeed * (1 + ReloadSpeedControlBonusPerPoint * CONTROL).
    ///
    /// Default player-only, with an MCM toggle to extend it to every hero (companions,
    /// lords, ...) - matches the predecessor mod's reference shape
    /// (Reference/MCMSettings.cs, ReloadBonus*) for the Enabled/Player Only/Bonus
    /// settings and scaling value (2% per point). The attribute is Control here per this
    /// session's explicit instruction - the reference file's own default for this group
    /// is actually Vigor (`selectedIndex: 0`), not Control; don't copy that default back
    /// for this effect.
    ///
    /// `UpdateAgentStats(Agent, AgentDrivenProperties)` is declared on the same abstract
    /// `AgentStatCalculateModel` as `GetEffectiveMaxHealth` was originally (see CLAUDE.md
    /// "Architecture gotchas") and - confirmed via reflection - is present with the
    /// identical signature on all three concrete mission-type implementations, so all
    /// three need a postfix or the bonus silently does nothing in naval battles, the same
    /// trap the very first Max Health implementation had to learn the hard way:
    ///   - SandboxAgentStatCalculateModel  - land missions (base game)
    ///   - NavalAgentStatCalculateModel    - naval missions (War Sails)
    ///   - NavalCustomBattleAgentStatCalculateModel - naval custom battles (War Sails)
    /// The predecessor mod's reference only patches the Sandbox (land) one.
    ///
    /// Only Heroes expose a Control value (Hero.GetAttributeValue); regular troops and
    /// ship crews have no equivalent accessor, so this only applies to hero agents.
    /// </summary>
    internal static class ReloadSpeedControlPatch
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
                if (settings == null || !settings.ReloadSpeedControlBonusEnabled)
                    return;

                if (settings.ReloadSpeedControlBonusPlayerOnly && !agent.IsMainAgent)
                    return;

                Hero? hero = (agent.Character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return;

                int control = hero.GetAttributeValue(DefaultCharacterAttributes.Control);
                float factor = 1f + settings.ReloadSpeedControlBonusPerPoint * control;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                agentDrivenProperties.ReloadSpeed *= factor;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"ReloadSpeedControlPatch.Apply threw: {e}");
            }
        }
    }
}
