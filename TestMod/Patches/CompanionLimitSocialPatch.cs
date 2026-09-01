using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// companionLimit = baseLimit + floor(CompanionLimitSocialBonusPerPoint * SOCIAL).
    /// Floored, not rounded: per the task spec, Social = 1 with the default 0.5-per-point
    /// bonus must leave the limit at baseLimit (floor(0.5) = 0), not round up to +1.
    ///
    /// Hard player-only, not a toggle - see Settings/BetterAttributesSettings.cs and
    /// CLAUDE.md "Effect default scope": this reads Hero.MainHero directly, matching the
    /// predecessor mod's reference implementation, which has no "Player Only" setting for
    /// this effect either.
    ///
    /// `TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.GetCompanionLimit(Clan)`
    /// confirmed via reflection against the installed game (v1.4.8) - unlike the combat
    /// stat models (see CLAUDE.md "Architecture gotchas"), there is only this one concrete
    /// `ClanTierModel` implementation, no land/naval-style split, so a single patch covers
    /// it.
    ///
    /// Deviation from the predecessor mod's reference: that version doesn't check which
    /// `clan` was passed in at all - it always adds the player's Social-based bonus
    /// regardless of whose companion limit is being queried, which would incorrectly
    /// affect an AI clan's limit if this model is ever queried for one. This version
    /// checks `clan == Hero.MainHero.Clan` first, so the bonus only ever applies to the
    /// player's own clan - matching the actual intent ("player-only") rather than the
    /// reference's literal behavior.
    /// </summary>
    [HarmonyPatch(typeof(DefaultClanTierModel), nameof(DefaultClanTierModel.GetCompanionLimit))]
    internal static class CompanionLimitSocialPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Clan clan, ref int __result)
        {
            try
            {
                Hero? mainHero = Hero.MainHero;
                if (mainHero == null || clan == null || clan != mainHero.Clan)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.CompanionLimitSocialBonusEnabled)
                    return;

                int social = mainHero.GetAttributeValue(DefaultCharacterAttributes.Social);
                float bonusPerPoint = settings.CompanionLimitSocialBonusPerPoint;
                int bonus = (int)Math.Floor(bonusPerPoint * social);

                __result += bonus;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"CompanionLimitSocialPatch.Postfix threw: {e}");
            }
        }
    }
}
