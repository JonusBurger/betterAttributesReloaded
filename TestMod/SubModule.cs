using HarmonyLib;
using TaleWorlds.MountAndBlade;


namespace TestMod
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyDomain = "TestMod";
        private Harmony? _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _harmony = new Harmony(HarmonyDomain);
            _harmony.PatchAll();
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            _harmony?.UnpatchAll(HarmonyDomain);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

        }
    }
}