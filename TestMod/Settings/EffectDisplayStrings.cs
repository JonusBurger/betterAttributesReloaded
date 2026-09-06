namespace TestMod.Settings
{
    /// <summary>
    /// Plain-text descriptions shown in the character-sheet attribute tooltips (see
    /// <see cref="TestMod.Patches.CharacterAttributeItemVMPatch"/>) for every
    /// attribute-scaling effect. Kept in one place, separate from the patch logic, so
    /// the wording can be checked and hand-edited without touching (or re-reading) any
    /// patch file - edit a line here, rebuild, done.
    ///
    /// Each constant is a leading phrase; the patch appends the effect's live,
    /// formatted value (a percentage for most effects, a plain "+N" for the flat
    /// Companion Limit bonus) right after it - e.g. "Increases max hit points by " +
    /// "15%" -> "Increases max hit points by 15%". Match that "ends with a space,
    /// value comes right after" shape if you add a new one.
    ///
    /// Wording mostly follows the predecessor mod's Reference/Strings.cs (the
    /// "...BonusText" constants) where an equivalent effect exists, adjusted for
    /// effects this mod implements differently (e.g. Prisoner Recruitment/Escape have
    /// no predecessor-mod equivalent at all - see CLAUDE.md "Architecture gotchas").
    /// Deliberately plain C# constants, not TaleWorlds' `{=id}` localization strings:
    /// nothing else in this mod uses that mechanism (every MCM DisplayName/HintText in
    /// <see cref="BetterAttributesSettings"/> is a plain literal too), and a raw
    /// constant is the easiest thing to find and edit by hand.
    /// </summary>
    public static class EffectDisplayStrings
    {
        public const string MaxHealthEndurance = "Increases max hit points by ";
        public const string MeleeDamageVigor = "Increases melee damage by ";
        public const string SliceThrough = "Chance to slice through to a second target: ";
        public const string RangedDamageControl = "Increases ranged damage by ";
        public const string CompanionLimitSocial = "Increases companion limit by +";
        public const string PersuasionCunning = "Increases persuasion chance by ";
        public const string InfluenceIntelligence = "Increases influence earned from victories by ";
        public const string ReloadSpeedControl = "Increases reload speed by ";
        public const string MovementSpeedEndurance = "Increases movement speed by ";
        public const string RenownCunning = "Increases renown earned from victories by ";
        public const string StabilityEndurance = "Increases aim stability by ";
        public const string PrisonerRecruitmentSocial = "Increases prisoner recruitment rate by ";
        public const string PrisonerEscapeControl = "Reduces captured lord escape chance by ";
    }
}
