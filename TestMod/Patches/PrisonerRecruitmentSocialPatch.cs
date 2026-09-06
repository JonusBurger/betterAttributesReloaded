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
    /// conformityGainPerHour = baseConformityGainPerHour * (1 + PrisonerRecruitmentSocialBonusPerPoint * SOCIAL).
    ///
    /// No predecessor-mod reference exists for this effect. The task's own hint ("this
    /// may be related to prisoner conformity") was exactly right: prisoners accumulate
    /// "conformity" over time while held, and become recruitable once it reaches the
    /// amount `GetConformityNeededToRecruitPrisoner` returns - found by reflecting on
    /// `TaleWorlds.CampaignSystem.dll` for types with "Prisoner" in the name (see
    /// CLAUDE.md "Architecture gotchas" for the general technique), which turned up
    /// `TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel`.
    /// "Increase the rate at which prisoners can be recruited" = increase
    /// `GetConformityChangePerHour`'s result, confirmed via reflection (v1.4.8) to be
    /// `GetConformityChangePerHour(PartyBase party, CharacterObject troopToBoost) : ExplainedNumber`,
    /// public and virtual, with only this one concrete model implementation.
    ///
    /// "Charisma" isn't a real Bannerlord attribute (the six are Vigor/Control/Endurance/
    /// Cunning/Social/Intelligence) - confirmed with the project owner that Social (the
    /// game's actual people-skills stat) was meant.
    ///
    /// Unlike every other effect so far, scope isn't a plain "Player Only" bool - the
    /// task asked for three tiers (default player-only, extendable to the player's whole
    /// clan, extendable further to every hero-led party), so
    /// `PrisonerRecruitmentScopeDropdown` is a 3-way `Dropdown&lt;string&gt;` instead
    /// (`SelectedIndex`: 0 = player only, 1 = player's clan, 2 = all lords).
    /// </summary>
    [HarmonyPatch(typeof(DefaultPrisonerRecruitmentCalculationModel), nameof(DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour))]
    internal static class PrisonerRecruitmentSocialPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            try
            {
                Hero? leaderHero = party?.LeaderHero;
                if (leaderHero == null)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.PrisonerRecruitmentSocialBonusEnabled)
                    return;

                Hero? mainHero = Hero.MainHero;
                bool applies = settings.PrisonerRecruitmentScopeDropdown.SelectedIndex switch
                {
                    0 => leaderHero == mainHero, // Player Only
                    1 => mainHero != null && leaderHero.Clan != null && leaderHero.Clan == mainHero.Clan, // Player's Clan
                    _ => true, // All Lords
                };
                if (!applies)
                    return;

                int social = leaderHero.GetAttributeValue(DefaultCharacterAttributes.Social);
                float factor = settings.PrisonerRecruitmentSocialBonusPerPoint * social;
                if (float.IsNaN(factor) || float.IsInfinity(factor))
                    return;

                __result.AddFactor(factor, new TextObject("Social Bonus"));
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"PrisonerRecruitmentSocialPatch.Postfix threw: {e}");
            }
        }
    }
}
