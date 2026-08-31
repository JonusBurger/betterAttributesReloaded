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

### Effect default scope (player-only vs. all heroes)

**Needs confirming with the project owner - the current wording is ambiguous/self-
contradictory and should not be implemented as-is:**

> for every effect flagged as applicable to all should be set default to
> player-only, though a setting for enabling scaling for companions or all heroes
> should be provided in MCMSettings-Mod
> every setting without this flag should be player-only

Best guess at the intent, to confirm: *every* effect defaults to "Player Only" =
`true`. Effects that are designed to be able to reach beyond the player (companions,
lords) additionally expose a "Player Only" MCM toggle so the user can opt into
broader scope; effects that are inherently player-only don't need that toggle
surfaced at all (or it's always locked to `true`). If that's right, replace the
quoted bullets with something like:

- Every effect's `XxxBonusEnabled`/bonus settings default to affecting the player
  only. Only expose a "Player Only" toggle in MCM for effects where broader scope
  (companions/all heroes) is a real option; when exposed, it still defaults to `true`.

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
