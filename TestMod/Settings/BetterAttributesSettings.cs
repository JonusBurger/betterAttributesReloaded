using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

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
        [SettingPropertyFloatingInteger("Bonus per Endurance point", 0f, 20f, "0.0", Order = 2, RequireRestart = false,
            HintText = "Max health added per point of Endurance: maxHealth = baseGameMaxHealth + bonus * Endurance.")]
        public float MaxHealthEnduranceBonus { get; set; } = 5f;

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

        public override string Id => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string DisplayName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FolderName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FormatType => "xml";
    }
}
