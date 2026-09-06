using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TestMod.Settings;

namespace TestMod.Patches
{
    /// <summary>
    /// Character-sheet attribute tooltip display: appends one line per enabled,
    /// applicable attribute-scaling effect to the vanilla attribute pop-up's help
    /// text, so the player can see at a glance what each attribute point is actually
    /// doing instead of just the base-game skill list.
    ///
    /// Reference: predecessor mod's `CharacterAttributeItemVMPatch`
    /// (`getAllBonusForGivenAttribute` + `CustomAtrObject`, one hardcoded `if` block
    /// per effect). Adapted here into a small descriptor table
    /// (<see cref="EffectLine"/>) instead, so each of this mod's effects contributes
    /// one entry without duplicating the postfix's plumbing - see CLAUDE.md "Notes for
    /// Claude" (grow by adding, not editing). Display text itself lives in
    /// <see cref="EffectDisplayStrings"/>, not inline here, per explicit project-owner
    /// request for text that's easy to find and hand-edit.
    ///
    /// Target confirmed via reflection against the installed game (v1.4.8,
    /// `TaleWorlds.CampaignSystem.ViewModelCollection.dll`,
    /// `TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.CharacterAttributeItemVM`):
    /// the 5-parameter constructor (Hero, CharacterAttribute,
    /// CharacterDeveloperHeroItemVM, Action&lt;CharacterAttributeItemVM&gt;,
    /// Action&lt;CharacterAttributeItemVM&gt;) matches the reference exactly - no
    /// signature drift this time (unlike CalculateInfluenceGain/CalculateRenownGain
    /// earlier - see CLAUDE.md). `IncreaseHelpText` is a public, read/write property
    /// (also confirmed via reflection) - safe to set directly from a postfix, no
    /// private-field/Traverse access needed.
    ///
    /// `CharacterAttribute` (base type `PropertyObject`) has no overridden `Equals`/
    /// `==` - comparisons below rely on reference equality between `currAtt` and the
    /// `DefaultCharacterAttributes.X` singletons, exactly like the predecessor mod's
    /// own `GetAttributeTypeFromIndex(...) == ca` comparison; each attribute constant
    /// is a single shared instance so this holds.
    ///
    /// Scope per effect mirrors that effect's own patch exactly (see each Patches/*.cs
    /// file) - hard player-only, toggleable "Player Only" bool, or the 3-way
    /// Prisoner Recruitment dropdown - so this tooltip never claims a bonus applies to
    /// a hero it doesn't.
    /// </summary>
    [HarmonyPatch(typeof(CharacterAttributeItemVM), MethodType.Constructor)]
    [HarmonyPatch(new Type[] {
        typeof(Hero), typeof(CharacterAttribute), typeof(CharacterDeveloperHeroItemVM),
        typeof(Action<CharacterAttributeItemVM>), typeof(Action<CharacterAttributeItemVM>)
    })]
    internal static class CharacterAttributeItemVMPatch
    {
        private readonly struct EffectLine
        {
            public readonly CharacterAttribute Attribute;
            public readonly Func<bool> IsEnabled;
            public readonly Func<Hero, bool> AppliesTo;
            public readonly Func<int, string> Format;

            public EffectLine(CharacterAttribute attribute, Func<bool> isEnabled, Func<Hero, bool> appliesTo, Func<int, string> format)
            {
                Attribute = attribute;
                IsEnabled = isEnabled;
                AppliesTo = appliesTo;
                Format = format;
            }
        }

        // Shared "does this apply to the hero whose sheet we're looking at" rules -
        // match the corresponding effect patch's own scope check exactly.
        private static bool PlayerOnlyOrAllHeroes(Hero hero, bool playerOnly) => !playerOnly || hero.IsHumanPlayerCharacter;
        private static bool HardPlayerOnly(Hero hero) => hero.IsHumanPlayerCharacter;

        [HarmonyPostfix]
        public static void Postfix(CharacterAttributeItemVM __instance, Hero hero, CharacterAttribute currAtt)
        {
            try
            {
                if (__instance == null || hero == null || currAtt == null)
                    return;

                var s = BetterAttributesSettings.Instance;
                if (s == null)
                    return;

                var lines = new List<EffectLine>
                {
                    new EffectLine(DefaultCharacterAttributes.Endurance,
                        () => s.MaxHealthEnduranceBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.MaxHealthEnduranceBonusPlayerOnly),
                        v => EffectDisplayStrings.MaxHealthEndurance + (s.MaxHealthEnduranceBonusPercent * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Vigor,
                        () => s.SliceThroughEnabled,
                        HardPlayerOnly,
                        v => EffectDisplayStrings.SliceThrough + Math.Min(1f, s.SliceThroughChancePerVigor * v).ToString("P0")),

                    // Scope mirrors MeleeDamageVigorPatch's own 3-way dropdown exactly
                    // (0 = Player Only, 1 = Player's Clan, 2 = All Lords).
                    new EffectLine(DefaultCharacterAttributes.Vigor,
                        () => s.MeleeDamageVigorBonusEnabled,
                        h => s.MeleeDamageVigorScopeDropdown.SelectedIndex switch
                        {
                            0 => h.IsHumanPlayerCharacter,
                            1 => Hero.MainHero != null && h.Clan != null && h.Clan == Hero.MainHero.Clan,
                            _ => true,
                        },
                        v => EffectDisplayStrings.MeleeDamageVigor + (s.MeleeDamageVigorBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Control,
                        () => s.RangedDamageControlBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.RangedDamageControlBonusPlayerOnly),
                        v => EffectDisplayStrings.RangedDamageControl + (s.RangedDamageControlBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Social,
                        () => s.CompanionLimitSocialBonusEnabled,
                        HardPlayerOnly,
                        v => EffectDisplayStrings.CompanionLimitSocial + ((int)Math.Floor(s.CompanionLimitSocialBonusPerPoint * v))),

                    new EffectLine(DefaultCharacterAttributes.Cunning,
                        () => s.PersuasionCunningBonusEnabled,
                        HardPlayerOnly,
                        v => EffectDisplayStrings.PersuasionCunning + (s.PersuasionCunningBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Intelligence,
                        () => s.InfluenceIntelligenceBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.InfluenceIntelligenceBonusPlayerOnly),
                        v => EffectDisplayStrings.InfluenceIntelligence + (s.InfluenceIntelligenceBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Control,
                        () => s.ReloadSpeedControlBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.ReloadSpeedControlBonusPlayerOnly),
                        v => EffectDisplayStrings.ReloadSpeedControl + (s.ReloadSpeedControlBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Endurance,
                        () => s.MovementSpeedEnduranceBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.MovementSpeedEnduranceBonusPlayerOnly),
                        v => EffectDisplayStrings.MovementSpeedEndurance + (s.MovementSpeedEnduranceBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Cunning,
                        () => s.RenownCunningBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.RenownCunningBonusPlayerOnly),
                        v => EffectDisplayStrings.RenownCunning + (s.RenownCunningBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Endurance,
                        () => s.StabilityEnduranceBonusEnabled,
                        h => PlayerOnlyOrAllHeroes(h, s.StabilityEnduranceBonusPlayerOnly),
                        v => EffectDisplayStrings.StabilityEndurance + (s.StabilityEnduranceBonusPerPoint * v).ToString("P0")),

                    // Scope mirrors PrisonerRecruitmentSocialPatch's own 3-way dropdown
                    // exactly (0 = Player Only, 1 = Player's Clan, 2 = All Lords).
                    new EffectLine(DefaultCharacterAttributes.Social,
                        () => s.PrisonerRecruitmentSocialBonusEnabled,
                        h => s.PrisonerRecruitmentScopeDropdown.SelectedIndex switch
                        {
                            0 => h.IsHumanPlayerCharacter,
                            1 => Hero.MainHero != null && h.Clan != null && h.Clan == Hero.MainHero.Clan,
                            _ => true,
                        },
                        v => EffectDisplayStrings.PrisonerRecruitmentSocial + (s.PrisonerRecruitmentSocialBonusPerPoint * v).ToString("P0")),

                    new EffectLine(DefaultCharacterAttributes.Control,
                        () => s.PrisonerEscapeControlBonusEnabled,
                        HardPlayerOnly,
                        v => EffectDisplayStrings.PrisonerEscapeControl + Math.Min(1f, s.PrisonerEscapeControlBonusPerPoint * v).ToString("P0")),
                };

                List<string> applicable = lines
                    .Where(l => l.Attribute == currAtt && l.IsEnabled() && l.AppliesTo(hero))
                    .Select(l => l.Format(hero.GetAttributeValue(currAtt)))
                    .ToList();

                if (applicable.Count == 0)
                    return;

                string bonusBlock = string.Join(Environment.NewLine, applicable);
                __instance.IncreaseHelpText = string.IsNullOrEmpty(__instance.IncreaseHelpText)
                    ? bonusBlock
                    : __instance.IncreaseHelpText + Environment.NewLine + bonusBlock;
            }
            catch (Exception e)
            {
                // See CLAUDE.md "Conventions": log bugs with cause/solution in bugHistory.md.
                System.Diagnostics.Debug.WriteLine($"CharacterAttributeItemVMPatch.Postfix threw: {e}");
            }
        }
    }
}
