using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TestMod.Settings
{
    /// <summary>
    /// In-game (MCM) settings for TestMod. One property group per effect, so a new
    /// attribute-scaling effect can be added here without touching existing ones -
    /// see CLAUDE.md "Conventions".
    /// </summary>
    public class BetterAttributesSettings : AttributeGlobalSettings<BetterAttributesSettings>
    {
        [SettingPropertyGroup("Bonuses/Max Health")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Endurance grants bonus max health.")]
        public bool MaxHealthEnduranceBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Max Health")]
        [SettingPropertyBool("Player Only", Order = 1, RequireRestart = false,
            HintText = "If enabled, only the player character's max health is affected. Otherwise every hero (companions, lords, ...) benefits.")]
        public bool MaxHealthEnduranceBonusPlayerOnly { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Max Health")]
        [SettingPropertyFloatingInteger("Bonus per Endurance point", 0f, 0.2f, "0.00%", Order = 2, RequireRestart = false,
            HintText = "Max health increase per point of Endurance: maxHealth = baseGameMaxHealth * (1 + bonusPerPoint * Endurance).")]
        public float MaxHealthEnduranceBonusPercent { get; set; } = 0.05f;

        // Player-only by design, not a toggle (see CLAUDE.md) - Vigor governs how hard the
        // player swings, not how hard everyone else does.
        [SettingPropertyGroup("Bonuses/Slice Through")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Vigor grants the player a chance to slice through to a second target on a melee hit, like the Executioner Axe.")]
        public bool SliceThroughEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Slice Through")]
        [SettingPropertyFloatingInteger("Chance per Vigor point", 0f, 0.2f, "0.00%", Order = 1, RequireRestart = false,
            HintText = "Chance per point of Vigor that a melee hit also slices through to a nearby enemy: chance = chancePerVigor * Vigor (capped at 100%).")]
        public float SliceThroughChancePerVigor { get; set; } = 0.02f;

        [SettingPropertyGroup("Bonuses/Slice Through")]
        [SettingPropertyBool("Notify on proc", Order = 2, RequireRestart = false,
            HintText = "Show an on-screen message when a hit slices through.")]
        public bool SliceThroughNotify { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Ranged Damage")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Control grants bonus damage with ranged weapons.")]
        public bool RangedDamageControlBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Ranged Damage")]
        [SettingPropertyBool("Player Only", Order = 1, RequireRestart = false,
            HintText = "If enabled, only the player character's ranged damage is affected. Otherwise every hero (companions, lords, ...) benefits.")]
        public bool RangedDamageControlBonusPlayerOnly { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Ranged Damage")]
        [SettingPropertyFloatingInteger("Damage bonus per Control point", 0f, 0.2f, "0.00%", Order = 2, RequireRestart = false,
            HintText = "Ranged damage increase per point of Control: damage = baseDamage * (1 + bonusPerPoint * Control).")]
        public float RangedDamageControlBonusPerPoint { get; set; } = 0.02f;

        // Player-only by design, not a toggle - matches the predecessor mod's reference
        // (Reference/MCMSettings.cs "Companion" group has no PlayerOnly setting either;
        // it always reads Hero.MainHero directly).
        [SettingPropertyGroup("Bonuses/Companion Limit")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Social grants the player extra companion slots.")]
        public bool CompanionLimitSocialBonusEnabled { get; set; } = true;

        // SettingPropertyFloatingInteger has no step-size option (checked against
        // MCMv5.dll - only displayName/min/max/format), so exact 0.5 steps are done via a
        // dropdown of preset values rather than a free slider that could land on
        // fractional-of-0.5 values.
        [SettingPropertyGroup("Bonuses/Companion Limit")]
        [SettingPropertyDropdown("Bonus per Social point", Order = 1, RequireRestart = false,
            HintText = "Extra companion slots per point of Social (floored): companionLimit = baseLimit + floor(bonusPerPoint * Social).")]
        public Dropdown<float> CompanionLimitSocialBonusPerPointDropdown { get; set; } = new Dropdown<float>(
            new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f }, selectedIndex: 1);

        public float CompanionLimitSocialBonusPerPoint => CompanionLimitSocialBonusPerPointDropdown.SelectedValue;

        // Player-only by design, not a toggle - matches the predecessor mod's reference
        // (Reference/MCMSettings.cs "Persuasion" group has no PlayerOnly setting either;
        // it always reads Hero.MainHero directly) and the persuasion dialogue minigame's
        // own nature (a player-facing conversation mechanic, not something AI heroes roll
        // for).
        [SettingPropertyGroup("Bonuses/Persuasion")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Cunning grants the player a better persuasion success chance.")]
        public bool PersuasionCunningBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Persuasion")]
        [SettingPropertyFloatingInteger("Bonus per Cunning point", 0f, 0.2f, "0.00%", Order = 1, RequireRestart = false,
            HintText = "Persuasion success chance increase per point of Cunning: chance = baseChance * (1 + bonusPerPoint * Cunning).")]
        public float PersuasionCunningBonusPerPoint { get; set; } = 0.02f;

        [SettingPropertyGroup("Bonuses/Influence")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Intelligence grants bonus influence from battle victories.")]
        public bool InfluenceIntelligenceBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Influence")]
        [SettingPropertyBool("Player Only", Order = 1, RequireRestart = false,
            HintText = "If enabled, only the player character's influence gain is affected. Otherwise every hero party leader (companions, lords, ...) benefits.")]
        public bool InfluenceIntelligenceBonusPlayerOnly { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Influence")]
        [SettingPropertyFloatingInteger("Bonus per Intelligence point", 0f, 0.2f, "0.00%", Order = 2, RequireRestart = false,
            HintText = "Influence gain increase per point of Intelligence: influence = baseInfluence * (1 + bonusPerPoint * Intelligence).")]
        public float InfluenceIntelligenceBonusPerPoint { get; set; } = 0.02f;

        [SettingPropertyGroup("Bonuses/Reload Speed")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Control grants bonus ranged weapon reload speed.")]
        public bool ReloadSpeedControlBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Reload Speed")]
        [SettingPropertyBool("Player Only", Order = 1, RequireRestart = false,
            HintText = "If enabled, only the player character's reload speed is affected. Otherwise every hero (companions, lords, ...) benefits.")]
        public bool ReloadSpeedControlBonusPlayerOnly { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Reload Speed")]
        [SettingPropertyFloatingInteger("Bonus per Control point", 0f, 0.2f, "0.00%", Order = 2, RequireRestart = false,
            HintText = "Reload speed increase per point of Control: reloadSpeed = baseReloadSpeed * (1 + bonusPerPoint * Control).")]
        public float ReloadSpeedControlBonusPerPoint { get; set; } = 0.02f;

        [SettingPropertyGroup("Bonuses/Movement Speed")]
        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "Whether Endurance grants bonus combat movement speed.")]
        public bool MovementSpeedEnduranceBonusEnabled { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Movement Speed")]
        [SettingPropertyBool("Player Only", Order = 1, RequireRestart = false,
            HintText = "If enabled, only the player character's movement speed is affected. Otherwise every hero (companions, lords, ...) benefits.")]
        public bool MovementSpeedEnduranceBonusPlayerOnly { get; set; } = true;

        [SettingPropertyGroup("Bonuses/Movement Speed")]
        [SettingPropertyFloatingInteger("Bonus per Endurance point", 0f, 0.2f, "0.00%", Order = 2, RequireRestart = false,
            HintText = "Movement speed increase per point of Endurance: moveSpeed = baseMoveSpeed * (1 + bonusPerPoint * Endurance).")]
        public float MovementSpeedEnduranceBonusPerPoint { get; set; } = 0.02f;

        public override string Id => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string DisplayName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FolderName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FormatType => "xml";
    }
}
