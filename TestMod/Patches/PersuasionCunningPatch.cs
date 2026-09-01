using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// persuasionChance = baseChance * (1 + PersuasionCunningBonusPerPoint * CUNNING).
    ///
    /// Hard player-only, not a toggle - matches the predecessor mod's reference (no
    /// "Player Only" setting for this effect either) and the persuasion dialogue
    /// minigame's own nature (a player-facing conversation mechanic, not something AI
    /// heroes roll for) - see Settings/BetterAttributesSettings.cs and CLAUDE.md
    /// "Effect default scope".
    ///
    /// `DefaultPersuasionModel.GetDefaultSuccessChance(PersuasionOptionArgs, float) : float`
    /// is private and non-virtual (confirmed via reflection against the installed game,
    /// v1.4.8, matching the predecessor mod's exact signature) - Harmony can still patch
    /// it, but `nameof()` can't reference a private member from outside its declaring
    /// class, hence the string literal method name below (same reason the predecessor
    /// mod's own patch uses one).
    ///
    /// Result is clamped to [0, 1] after applying the bonus - a "success chance" above
    /// 100% or below 0% doesn't mean anything, and other code reading this value likely
    /// assumes it's already normalized.
    /// </summary>
    [HarmonyPatch(typeof(DefaultPersuasionModel), "GetDefaultSuccessChance")]
    internal static class PersuasionCunningPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref float __result)
        {
            try
            {
                Hero? mainHero = Hero.MainHero;
                if (mainHero == null)
                    return;

                var settings = BetterAttributesSettings.Instance;
                if (settings == null || !settings.PersuasionCunningBonusEnabled)
                    return;

                int cunning = mainHero.GetAttributeValue(DefaultCharacterAttributes.Cunning);
                float factor = 1f + settings.PersuasionCunningBonusPerPoint * cunning;
                if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                    return;

                float newResult = __result * factor;
                if (float.IsNaN(newResult) || float.IsInfinity(newResult))
                    return;

                __result = Math.Max(0f, Math.Min(newResult, 1f));
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"PersuasionCunningPatch.Postfix threw: {e}");
            }
        }
    }
}
