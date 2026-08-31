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

        public override string Id => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string DisplayName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FolderName => base.GetType().Assembly.GetName().Name ?? nameof(TestMod);
        public override string FormatType => "xml";
    }
}
