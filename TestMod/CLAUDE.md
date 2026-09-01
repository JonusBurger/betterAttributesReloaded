# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

This is a C# module (mod) for **Mount & Blade II: Bannerlord**, targeting the
**War Sails** DLC specifically (not just the base game - see "Dependencies" below).
It uses Bannerlord's module system and references the game's `TaleWorlds.*`
assemblies directly from the game installation.

- Module name: `TestMod` - **this is still the placeholder name from the BUTR
  project template.** Rename the folder, `.csproj`, root namespace and
  `_Module/SubModule.xml` before release; low risk since the manifest uses
  `$moduleid$`/`$modulename$` tokens rather than hardcoding the name.
- Game install path: set via the `BANNERLORD_GAME_DIR` environment variable.
- Target frameworks: `net472` and `net6` (see `TestMod.csproj`, `TargetFrameworks`).
  Build and verify **both** - they can diverge on nullable-reference warnings.
- Aim of the mod: add effects on Attribute-Points (Vigor, Control, Endurance,
  Cunning, Social, Intelligence) whose strength scales with the attribute's level.

## Dependencies

**Required** (declared in `_Module/SubModule.xml` `<DependedModules>`, referenced
in `TestMod.csproj` via `$(GameFolder)\Modules\<Name>\bin\...\*.dll`):
- `Native`, `SandBoxCore`, `Sandbox`, `StoryMode`, `CustomBattle` - base game modules.
- `Bannerlord.Harmony` (v2.3.3) - runtime patching of game code.
- `NavalDLC` (War Sails) - **this mod targets War Sails and is not guaranteed to
  work, or even load, without it.** Land missions still work normally (War Sails is
  additive), but any new effect must account for the DLC - see "Architecture
  gotchas" below.

**Optional** (declared with `optional="true"` in `DependedModuleMetadatas`; the mod
must keep working with default setting values if these aren't installed):
- `Bannerlord.MBOptionScreen` (MCM) - in-game settings UI. Compiled against the
  `Bannerlord.MCM` NuGet package (`MCM.Abstractions.*` namespaces).

**Not currently wired up - confirm before relying on them:**
- BLSE (Bannerlord Loader Extended) is mentioned in a SubModule.xml comment
  (linking to its dependency-metadata schema docs) but is **not** itself declared
  as a `DependedModule`. If the dual net472/net6 build actually needs BLSE to load,
  add it explicitly; don't assume it's present.
- ButterLib - not referenced by any code so far. Only add it if a concrete feature
  needs it.

**Game version:** the installed base game's real module version is **v1.4.8**
(check `Modules\Native\SubModule.xml` or `Modules\SandBoxCore\SubModule.xml`
`<Version>` - matches `NavalDLC`'s `RequiredBaseVersion`). The installed War Sails
module version is `v1.2.8`. `$BANNERLORD_GAME_DIR\package_info.txt`'s
`Environment: PC@v1.3.4` is a different build label - don't use it for compatibility
checks.

## Build & test

```bash
dotnet build
```

- Build **both** target frameworks before considering a change done
  (`dotnet build` with no `-f` builds both; a stray `-f net472` will hide
  net6-only warnings/errors and vice versa).
- On a successful build, a post-build step copies the output DLLs and
  `SubModule.xml` into `$(BANNERLORD_GAME_DIR)\Modules\TestMod\`. Spot-check that
  copy landed (e.g. compare the DLL's timestamp) as a quick sanity check that the
  build actually did something.
- To actually run/test the mod, launch Bannerlord via
  `TaleWorlds.MountAndBlade.Launcher.exe` (in `bin\Win64_Shipping_Client` of the
  game folder), with the mod enabled in Singleplayer > Mods.
- There is no automated test suite; verification is manual, in-game.

## Project structure

```
TestMod/
├── TestMod.csproj   # project file, references TaleWorlds.* + NavalDLC DLLs
├── SubModule.cs               # mod entry point (MBSubModuleBase); applies/unpatches Harmony
├── SubModule.xml               # module manifest read by the launcher
├── ModuleData/                # XML data (items, cultures, etc.)
├── Reference/                 # Snippets from the predecessor mod, for inspiration only -
│                               # NOT meant to compile (see "Architecture gotchas"). Excluded
│                               # from the build via <Compile Remove> in the .csproj.
├── crash folder/              # folder for storing crashes that occur using the mod
├── Patches/                   # Harmony patches, one file per effect/model
├── Settings/                  # Real, compiled MCM settings (Bannerlord.MCM package)
├── GUI/                       # UI prefabs, if applicable
├── bin/, obj/                 # build output — ignored by git
├── bugHistory.md              # Logging file for bugs (cause + solution)
```

## Architecture gotchas (verified against the installed game/DLC assemblies)

These are non-obvious API facts discovered the hard way - re-derive them from the
actual DLLs rather than trusting old assumptions if the game/DLC updates.

- **Naval missions use a different agent-stat model than land missions.** Every
  per-agent combat stat (max health, health regen, melee/ranged dmg, stagger,
  accuracy, reload, movement, ...) is produced by an override of
  `TaleWorlds.MountAndBlade.AgentStatCalculateModel`, which is abstract - each
  mission type registers its own concrete subclass:
  - `SandBox.GameComponents.SandboxAgentStatCalculateModel` - land missions
  - `NavalDLC.GameComponents.NavalAgentStatCalculateModel` - naval missions
  - `NavalDLC.ComponentInterfaces.NavalCustomBattleAgentStatCalculateModel` - naval custom battles

  Harmony can only patch concrete methods, not the abstract contract, so **every
  agent-stat effect needs a Harmony postfix on all three classes**, or it will
  silently do nothing during naval battles. Share the actual bonus logic in one
  private helper and call it from three thin `[HarmonyPatch]` postfixes (one per
  class) - don't duplicate the logic three times.
- **Only Heroes expose attribute values.** `Hero.GetAttributeValue(CharacterAttribute)`
  (e.g. `TaleWorlds.Core.DefaultCharacterAttributes.Endurance`) is the only way to
  read a Vigor/Control/Endurance/Cunning/Social/Intelligence value. Regular troops
  and ship crew have no equivalent accessor - **every attribute-scaling effect can
  only affect hero agents** (player, companions, lords), not rank-and-file
  troops/crew. Get the Hero from an `Agent` via
  `(agent.Character as CharacterObject)?.HeroObject`.
- **`Reference/*.cs` files are not meant to compile.** They're fragments of the
  predecessor mod for inspiration, missing types (a `Strings` localization class)
  and packages (`MCM.Abstractions`) on purpose. If you add a new reference snippet,
  give it a non-`.cs` extension (`.cs.txt`, matching `PatchExample.cs.txt`) or add
  an explicit `<Compile Remove>` for it immediately - don't rely on remembering to
  do it later. Verify with `dotnet build` right after adding one.
- Game assemblies aren't decompiled or dumped as source. Reflecting on the
  installed DLLs to list type/method signatures (`Assembly.LoadFrom` + an
  `AssemblyResolve` handler pointed at the game's `bin`/`Modules` folders, then
  `GetTypes()`/`GetMethods()`) is fine and the preferred way to confirm an exact
  Harmony patch target before writing one - it avoids shipping a patch that
  silently no-ops because a method name or owning type was guessed wrong.
- **Before reaching for a `MissionBehavior`, check whether a Harmony patch on an
  existing vanilla mechanic already does what the effect needs.** Slice Through's first
  implementation used a `MissionBehavior` (`OnAgentHit` + a manual
  `Mission.GetNearbyEnemyAgents` query + a manual `Agent.Health` write) to hit a *second*
  agent the game itself never calls a patchable model method for. That worked, eventually
  (see the `MissionBehaviorType` entry below), but the predecessor mod's actual code (the
  project owner found and shared it) did the same effect as a plain Harmony **prefix** on
  `TaleWorlds.MountAndBlade.MissionCombatMechanicsHelper.UpdateMomentumRemaining` (still
  present with the same signature in the installed v1.4.8, confirmed via reflection):
  returning `false` skips that call's momentum deduction, letting the game's own native
  "swing carries through to a second target" mechanic do the rest - no manual agent
  lookup, no manual damage application, and no `MissionBehavior` at all (see
  `bugHistory.md` 2026-09-01 "rewritten, not just fixed"). **Where a predecessor mod or
  `Reference/` snippet exists for an effect, check it before designing one from scratch -
  it may already show the real vanilla hook to patch, which is usually simpler and safer
  than reconstructing the behavior by hand with a `MissionBehavior`.** `MissionBehavior`
  is still the right tool when an effect genuinely has no existing method to hook (e.g. it
  needs to run every tick regardless of any single game event) - just don't reach for it
  as the default before checking for a more direct patch target.
- **If a `MissionBehavior` is genuinely needed: register it as `MissionBehaviorType.Other`
  in this project, not `Logic`.** This project's ~18-mod stack has whatever fixed-capacity
  structure the engine uses to bucket `Logic`-type `MissionBehavior`s already full;
  registering one more `Logic` behavior overflows it and crashes
  `Mission.CheckMissionEnded()` on the very next tick - reproduces in plain **land**
  battles, nothing to do with War Sails/NavalDLC despite early appearances.
  `MissionBehaviorType.Other` uses a different, non-full bucket and avoids it entirely.
  This is specific to *this* mod list/machine, not a general Bannerlord rule - re-verify
  if the installed mod list changes significantly. Full diagnostic chain (6 crashes, each
  step ruling something out - don't re-walk it) is in bugHistory.md 2026-09-01,
  "RESOLVED" entry.
- **A crash dump can be root-caused without a full debugger.** `dotnet tool install -g
  dotnet-dump`, then `dotnet-dump analyze <dump.dmp> -c "clrstack -all" -c exit` prints
  the managed call stack of every thread. The thread with a `[FaultingExceptionFrame]` is
  the one that actually crashed; its topmost IP should match `ExceptionAddress` in
  `watchdog_log_*.txt` exactly - that confirms which frame faulted even with zero PDBs.
  (`modules`/`lm` lists loaded native modules with base+size, useful for checking whether
  an address falls inside a specific DLL - if it falls in *no* module, it's JIT-generated
  code, i.e. a managed method.) All 6 of the 2026-09-01 crashes faulted at the exact same
  site this way - `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()` - which is what
  eventually pointed at the `MissionBehaviorType.Logic` bucket-overflow bug above, not at
  `MaxHealthEndurancePatch` (disabling it via MCM did *not* stop the crash, ruling it out
  despite initially looking like a better fit for the "crashes near combat start" timing).
  `MaxHealthEndurancePatch` still got a harmless defensive clamp and a reordered cheap-
  check-before-touching-the-settings-singleton pass, but neither was ever the real cause -
  don't cite them as "the fix" for this crash family.
- **Confirmed by direct A/B test (2026-09-01): the crashes are caused by this mod**, not
  by the other 18 unofficial mods active alongside it (~10 crash-free hours with the same
  mod list minus this one). Don't re-litigate "maybe it's another mod" without new
  evidence to the contrary.
- **In a per-agent Harmony postfix, check cheap agent/type conditions *before* touching
  any shared singleton (MCM settings, etc.), not after** - `MaxHealthEndurancePatch` now
  checks for a `Hero` before ever reading `BetterAttributesSettings.Instance`, so regular
  troops (the vast majority of spawning agents) never touch the settings object. Good
  hygiene, kept as-is - but disabling this effect via MCM did *not* stop the 2026-09-01
  crashes, so this was confirmed **not** to be their cause; don't re-cite it as one.
- **Check `in` vs `ref` on game method parameters before overriding/calling them -
  reflection's `ParameterInfo.ParameterType` shows both as the same `&`-suffixed
  type.** Check `ParameterInfo.IsIn`/`IsOut` too. `MissionBehavior.OnAgentHit`'s
  `Blow`/`AttackCollisionData`/`MissionWeapon` parameters and
  `Agent.RegisterBlow`'s `AttackCollisionData` parameter are all `in`, not `ref` -
  using `ref` in an override/call compiles as a *different, non-overriding*
  signature (or a hard error), not a warning.
- `Hero.GetAttributeValue`, `MBRandom.RandomFloat` (`TaleWorlds.Core`), `MBList<T>`
  and `InformationManager`/`InformationMessage` (`TaleWorlds.Library`) round out the
  toolkit for chance-based, notification-emitting effects like Slice Through.

## Conventions

- Do not modify anything under the game's own install directory
  (`$BANNERLORD_GAME_DIR`) — treat it as read-only. Only write inside this repo;
  the post-build step handles copying into `Modules/`.
- Follow existing TaleWorlds naming/style conventions when touching `SubModule.cs`
  or behaviors (PascalCase methods, override patterns from `MBSubModuleBase` /
  `CampaignBehaviorBase`, etc.). An example is provided in
  `Reference/PatchExample.cs.txt` (real, working patches - once one exists,
  `Patches/` itself is the better example to follow).
- Prefer Harmony patches over editing decompiled game code when changing native
  behavior.
- Keep `SubModule.xml` in sync with any new DLL/module dependency added to the
  `.csproj` (both the `<DependedModules>` entry and the matching
  `<DependedModuleMetadata>` entry).
- Log all bugs encountered in `bugHistory.md` with cause and solution.
- Use only small commits.
- Every change should not effect the save-file in a way that will corrupt it if the mod is disabled
  - "Corrupt" means: the save becomes unloadable, or the game errors/crashes loading it -
    not "a battle plays out differently because of a mod bonus" (that's just the mod
    working, not corruption).
  - Currently compliant by construction: no `CampaignBehaviorBase`/`SyncData` anywhere in
    this mod (that's the mechanism that would write custom data into the `.sav`), both
    effects are Harmony patches on mission-scoped values recomputed fresh every battle
    (nothing persisted), MCM settings (`BetterAttributesSettings`, `AttributeGlobalSettings<T>`)
    live in MCM's own external file, not the save, and no new items/skills/perks/troops
    exist for a save to hold a now-dangling ID for.
  - This only becomes a real design question the moment an effect wants **persistent
    custom state** (a `CampaignBehaviorBase` tracking something across sessions, or new
    content with its own IDs). At that point: prefer avoiding persistence entirely (stay
    recomputed at runtime, like every effect so far); if `SyncData` is unavoidable, read
    with a safe default when the key is missing rather than assuming it exists.

### Effect default scope (player-only vs. all heroes)

The original wording here was ambiguous/self-contradictory:

> for every effect flagged as applicable to all should be set default to
> player-only, though a setting for enabling scaling for companions or all heroes
> should be provided in MCMSettings-Mod
> every setting without this flag should be player-only

One data point confirmed since: for **Slice Through**, the project owner said the
effect "should be player-only" outright - it's implemented hard-locked to the
player agent, with **no** "Player Only" toggle in MCM at all (see
`SliceThroughMissionBehavior`, `BetterAttributesSettings`). That's the pattern for
an effect that's inherently about the player's own action (their swing), as
opposed to a passive attribute bonus a hero just "has" (like Max Health), where a
toggle to extend the bonus to companions/lords makes sense.

Still an open general policy question, not yet confirmed: for a *passive* bonus
effect (like Max Health), should "Player Only" default to `true` with an opt-out
toggle (current implementation), or should some effects default to affecting all
heroes? Ask before assuming either way for a new passive-bonus effect; for an
"active" effect representing the player's own action, default to hard player-only
(no toggle) unless told otherwise, per the Slice Through precedent.

## Notes for Claude

- The mod will grow with more effects for the different attributes, hence it
  should be implemented in a way that makes it easy to add new effects (see
  "Architecture gotchas" for the concrete pattern: shared bonus-calc helper + one
  thin Harmony postfix per concrete stat-model class).
- The concrete scaling effects should be changeable using the MCM settings mod
  in-game - implement settings for real in `Settings/` (see `BetterAttributesSettings`
  if it still exists, or `Reference/MCMSettings.cs` for the shape of individual
  settings), not as hardcoded constants.
- Since the mod is based on an old one, `Reference/MCMSettings.cs` is that old
  mod's settings file - use it as a reference for *what* should be configurable,
  not as compilable source (see "Architecture gotchas").
- Add relevant information to this CLAUDE.md file if encountered - especially
  anything discovered about the game/DLC's actual API surface (class names, method
  signatures, which mission types use which model) so it doesn't need
  re-discovering next session.

## Open items

- **Slice Through (`SliceThroughMomentumPatch`, the Harmony-patch rewrite - see
  "Architecture gotchas") is confirmed working in land combat, but still not tested in a
  naval battle.** War Sails missions weren't covered even by the `MissionBehavior`
  version this replaced, so this remains entirely unverified there. Update this entry and
  `bugHistory.md`'s 2026-09-01 "rewritten, not just fixed" entry once tested.
