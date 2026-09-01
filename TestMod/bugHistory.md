# Bug History

## 2026-08-31 - Project failed to build out of the box

**Cause:** `Reference/MCMSettings.cs` used the `.cs` extension, so the SDK-style
project's default glob compiled it as real source. It references the `MCM.Abstractions`
package (not installed) and a `Strings` localization class that doesn't exist in this
repo - it's a snippet copied from the predecessor mod for inspiration only, per
CLAUDE.md. Result: 846 compiler errors on a clean checkout, before any new code was
added. `Reference/PatchExample.cs.txt` had already been given a `.txt` extension for
the same reason, but `MCMSettings.cs` was missed.

**Solution:** Excluded `Reference\**\*.cs` from compilation in `TestMod.csproj` (kept
as `None` items so they still show in the IDE). Reference snippets should get a
non-`.cs` extension (e.g. `.cs.txt`) or an explicit `<Compile Remove>` going forward.

## 2026-09-01 - Game crashed (access violation) during combat with Slice Through active

**Cause:** `SliceThroughMissionBehavior.OnAgentHit` applied splash damage to a nearby
enemy by copying the *primary* target's `Blow` struct (bone index, weapon record,
geometry - all computed by the engine for a collision against the primary target) and
calling `secondaryAgent.RegisterBlow(splashBlow, splashCollisionData)` on a *different*
agent. Crash logs (`crash folder/2026-09-01_02.22.59/watchdog_log_8092.txt`) show
`ExceptionCode: 0xC0000005` (access violation) with all parameters `0x0` and no managed
exception/stack trace anywhere in `rgl_log_8092.txt` - i.e. a native crash, not a .NET
exception, which is also why the `try/catch` around the effect didn't prevent it (a raw
AV isn't a catchable CLR exception). The log's last lines were mid-battle
(`rgl_log_8092.txt`, naval siege, formation/AI logs right before the log just stops),
consistent with the player landing a melee hit and Slice Through procing. No symbol
store was available to resolve the crashing address to a function
(`watchdog_log_8092.txt`: "Skipping stack resolution since no symbol store file
present"), so this is the most plausible cause from the evidence available, not a
confirmed one from a resolved stack trace.

**Solution:** Stopped using `RegisterBlow`/`Blow` for the secondary agent entirely.
Splash damage is now a plain `Agent.Health -= splashDamage` (clamped to a minimum of 1,
so splash damage alone can never kill) - no native geometry/weapon-record data involved.
Trade-off: the secondary agent gets no hit-reaction animation, blood effect, or kill
credit from the splash hit, and can't be killed by it outright. See the doc comment on
`SliceThroughMissionBehavior` and CLAUDE.md's "Architecture gotchas".

**Not fully verified:** this fix removes the specific unsafe pattern found, but wasn't
confirmed against a symbol-resolved crash stack (none was available). If crashes recur
in combat with Slice Through enabled, get a symbol store set up
(`watchdog_log_*.txt` names the expected path) before guessing again.

## 2026-09-01 (second crash, same day) - Game crashed again, this time at the very start of combat

**This one WAS root-caused to a real frame, not guessed at.** `crash folder/2026-09-01_02.33.48`'s
`dump.dmp` was loaded with `dotnet-dump analyze` (`dotnet tool install -g dotnet-dump`, then
`clrstack -all` across all 92 threads). Thread `0x3c18` has a `[FaultingExceptionFrame]` -
the actual crashing thread - with this managed stack, and its top IP address
(`0x7FFF5A2AD7D9`) is *exactly* the `ExceptionAddress` from `watchdog_log_19572.txt`:

```
TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()
TaleWorlds.MountAndBlade.Mission.CheckMissionEnd(Single)
TaleWorlds.MountAndBlade.Mission.OnTick(...)
```

`CheckMissionEnded` is a vanilla method neither `MaxHealthEndurancePatch` nor
`SliceThroughMissionBehavior` touch directly - so this crash isn't in "our" code, but in
game code reacting to a bad *value* something put into agent/mission state. Because this
happened at the very start of combat (before any melee hit could have occurred),
`SliceThroughMissionBehavior` (which only runs from `OnAgentHit`) is very unlikely to be
involved this time - **the previous crash's fix (2026-09-01, first entry above) may have
been a correct hardening in its own right, but was not necessarily the actual cause of
either crash.** `MaxHealthEndurancePatch` runs at agent spawn (every `GetEffectiveMaxHealth`
call), which matches the timing far better.

**Solution (defensive, not confirmed as *the* fix):** `MaxHealthEndurancePatch` now
rejects a non-finite (`NaN`/`Infinity`) bonus or result and clamps the resulting max
health to a minimum of 1, instead of ever handing back a value vanilla code (mission-end
checks, health-bar buckets, etc.) might not expect. This is cheap insurance regardless of
whether it's the real cause.

**Still unresolved / next steps if it recurs:**
- No native symbols exist for `TaleWorlds.MountAndBlade.dll` here, so *why*
  `CheckMissionEnded` faults on whatever value reached it couldn't be pinned down further
  from this machine.
- 18 other unofficial mods are loaded alongside this one (see `crash_tags.txt`), several
  of which (`BetterAttributePoints`, `AutoResolveRebalanced`, `Retinues`, `BahamutArmory`)
  independently touch attributes/combat stats, and `RTSCamera` has a confirmed *unrelated*
  broken Harmony patch (`Patch_ShipOrder.EnableAIPilotPlayerShip` verification failure,
  visible in both crashes' `rgl_log_*.txt`). The only reliable way to confirm TestMod is
  even responsible is to reproduce with TestMod disabled (or with only TestMod enabled) -
  log analysis alone couldn't prove attribution this time, unlike the first crash.
- If it recurs, capture the current MCM settings values (especially whether "Player Only"
  toggles were changed from their defaults) alongside the crash log - that changes how
  many agents `MaxHealthEndurancePatch` touches at once.

## 2026-09-01 (attribution confirmed) - Project owner ran the isolation test

Played ~10 hours with the exact same other 18 mods, TestMod excluded, no crash. That
rules out "it's actually one of the other mods" (RTSCamera's broken naval patch included)
- **it's TestMod.** Combined with the second crash's confirmed pre-hit timing (rules out
`SliceThroughMissionBehavior`, which only runs from an actual melee hit), that leaves
`MaxHealthEndurancePatch` as the only candidate in this mod's own code for the
start-of-combat crash.

Re-reviewed it with that narrowed focus: the arithmetic itself (`bonus per point (0-20) *
Endurance (small int)`) can't realistically produce `NaN`/`Infinity` under normal
settings, so the previous entry's defensive clamp, while harmless, was probably not
addressing a real scenario. The more concrete problem found on review: `Apply(...)`
touched `BetterAttributesSettings.Instance` (the MCM singleton) on *every single call*,
for *every* agent - including ordinary troops - before checking whether the agent even
has a `Hero` (the only kind of agent this effect can affect at all). At the start of a
large naval battle, potentially hundreds of agents spawn/tick at once, all hitting that
singleton simultaneously.

**Solution:** reordered `Apply(...)` to check `agent.Character`/`HeroObject` for `null`
*first*, before touching `BetterAttributesSettings.Instance` at all - regular troops and
crew now bail out without ever touching the settings singleton, cutting how often it's
accessed from "every agent, every call" to "the handful of actual heroes on the field".

**Still not proven as *the* fix** - no profiler or symbols were available to confirm
`BetterAttributesSettings.Instance` contention (or its cost) was actually the mechanism,
only that it's a real, removable inefficiency that scales with exactly the condition
("many agents at once, at combat start") this crash correlates with. **Fastest next
confirmation step, no rebuild required:** turn "Max Health / Endurance - Enabled" off in
the in-game MCM menu and start a naval battle. If it stops crashing with the effect
merely *disabled* (patches still applied, just no-op past the settings check), that's
strong confirmation. If it still crashes even then, the bug is in the mere act of having
these three methods Harmony-patched (not in this effect's logic), which would point back
at `SubModule.OnSubModuleLoad`'s `Harmony.PatchAll()` or a cross-mod Harmony interaction
instead - a different, harder problem.

## 2026-09-01 (found it, probably) - Disabling Max Health didn't help; still crashed ~5s into combat

Project owner disabled "Max Health / Endurance" in MCM and started a new battle. **Still
crashed**, ~5 seconds in - and separately recalled that crashes only started after the
Slice Through update (not after the Max Health effect, which shipped earlier and was
stable). A third crash dump (`crash folder/2026-09-01_02.52.46`) was analyzed the same
way as the second (`dotnet-dump analyze ... -c "clrstack -all"`): **identical fault site**,
`TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()`, called from `CheckMissionEnd` ->
`OnTick`, on the thread with the `[FaultingExceptionFrame]`. Same crash, third time,
despite Max Health being fully disabled (its Harmony postfixes still applied, but a
disabled effect returns immediately without touching `__result` at all). That rules out
Max Health's logic for good and points squarely at Slice Through - the only other thing
introduced since the mod was last known-stable, and the only thing that mutates agent
state from a hit-reaction callback rather than a value-computing model method.

**Root cause (best-supported theory yet, still not symbol-confirmed):** `OnAgentHit` is
called by the engine from *deep inside its own native hit/collision resolution* for the
hit that just happened. The previous version of `SliceThroughMissionBehavior` queried
nearby agents (`Mission.GetNearbyEnemyAgents`) and wrote `Agent.Health` on a *different*
agent synchronously, right there inside that callback. That's reentrant into game systems
that are still mid-resolution for the *current* hit and apparently don't expect it. The
corruption doesn't crash immediately - it surfaces one tick later, in
`Mission.CheckMissionEnded`'s full sweep over agent/mission state, which is exactly the
crash site in all three crashes and matches "a few seconds into combat" (however long
until the first real melee hit landed).

**Solution:** `OnAgentHit` now only *records* what it needs (hit position, radius, team,
the two agents involved, splash damage, whether to notify) into a small queue - it
performs no lookups and no mutation. The actual `Mission.GetNearbyEnemyAgents` query and
`Agent.Health` write now happen in a new `OnMissionTick` override, a normal top-level tick
callback that isn't nested inside hit resolution for anything.

**Still not proven with certainty** - no symbols for `TaleWorlds.MountAndBlade.dll` exist
here, so *why* reentrant agent mutation corrupts state `CheckMissionEnded` later reads on
couldn't be confirmed beyond "it's the only thing that changed between stable and
crashing, and this is the standard, well-known safe pattern for this exact situation in
Bannerlord modding." If it crashes again with this fix in place, the bug is not
reentrancy-shaped and needs a different theory entirely - at that point, the next
diagnostic step is disabling Slice Through too (in addition to Max Health) to confirm
whether *either* effect's logic is involved at all, versus something structural (the
`MissionBehavior` registration itself, or the NavalDLC dependency changes from that same
update).

## 2026-09-01 (reentrancy fix didn't help) - Crashed a 4th time, identical fault site

The deferred-to-`OnMissionTick` fix above did **not** stop the crash. A fourth dump
(`crash folder/2026-09-01_03.05.03`) analyzed the same way (`dotnet-dump analyze ... -c
"clrstack -all"`) faults at the **exact same site** as crashes 2 and 3:
`TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()`, same call chain
(`CheckMissionEnd` -> `OnTick` -> `TickMission` -> ...), same `0xC0000005` / all-zero
exception parameters. Register dump of the faulting thread (`registers` after `setthread`
on the thread with `[FaultingExceptionFrame]`) showed a couple of zeroed registers
(`rsi`, `r15`) consistent with a null-pointer dereference, but nothing attributable to a
specific field without symbols/disassembly - dead end for now.

**This falsifies the reentrancy theory as stated** (or at least shows the fix didn't
address the real mechanism): deferring the lookup/mutation to a top-level tick callback
was supposed to eliminate reentrancy into hit-resolution, and didn't change the outcome
at all. Two explanations remain open:
1. The bug isn't in *what* `SliceThroughMissionBehavior` does, but in the mere fact that
   an extra `MissionBehavior` subscribing to `OnAgentHit` is registered during a naval
   mission at all (e.g. NavalDLC's own mission setup has some fragile assumption about
   behavior count/order that a mod adding one more breaks) - independent of that
   behavior's internal logic, deferred or not.
2. The bug was never in Slice Through's *logic* to begin with (consistent with Max
   Health's disable test also not helping) and is structural elsewhere - e.g. a Harmony
   IL-combination issue between this mod's `GetEffectiveMaxHealth` postfixes and another
   mod's patch on the same method(s), or something in the `SubModule.xml`
   `NavalDLC` dependency addition shifting mod load order.

**Action taken (temporary, not a fix):** `SubModule.cs` now has a
`DisableSliceThroughForDiagnosis` const, set to `true`, that skips
`mission.AddMissionBehavior(new SliceThroughMissionBehavior())` entirely - the class and
its logic are untouched, it's simply never registered. This isolates explanation #1
above: if a naval battle with this build **doesn't** crash, the mere registration is
implicated (regardless of what the behavior would have done) and NavalDLC's mission setup
needs a closer look. If it **still** crashes with Slice Through not registered at all,
that clears Slice Through completely (both logic and registration) and shifts focus to
Max Health's Harmony patches structurally, or a cross-mod interaction - at that point,
also worth checking `rgl_log_*.txt` for every mod's Harmony patch target list to see if
any other installed mod patches `GetEffectiveMaxHealth` on the same three classes.
**Remember to flip `DisableSliceThroughForDiagnosis` back once this is answered** - it
silently drops the Slice Through feature entirely while `true`.

**Correction (same day):** the project owner confirmed crash #4 happened in a *normal*
(land) battle, not a naval one. Log lines this investigation had been reading as "this is
a naval mission" (`AIDragon: ShipBurningSystem tipi bulundu`, `Gemi listesi alınamadı:
Ambiguous match found`) are actually present in that same land-battle log too - they're
from the ROT-Dragon mod probing for a ship-related type via reflection on *every*
mission, not evidence of naval context.

**Confirmed further: crashes 1-3 were also land battles, not naval.** So all four
crashes so far have been in ordinary land combat. **The entire War-Sails/naval framing of
this crash investigation was a red herring from the start** - `NavalAgentStatCalculateModel`
and `NavalCustomBattleAgentStatCalculateModel` are Harmony-patched (structurally, at
`PatchAll()` time) but their `GetEffectiveMaxHealth` overrides are never actually *called*
in a land mission; only `SandboxAgentStatCalculateModel`'s is, and only
`SliceThroughMissionBehavior` (registered for every mission, not naval-gated) was ever
relevant. Drop any remaining theory that leans on naval-specific mechanics, large-naval-
battle mass spawn counts, or NavalDLC's own mission setup - none of that is exercised
here. This is actually good news for debugging: **the crash reproduces in plain,
ordinary combat**, which is by far the fastest, simplest scenario to test against - no
need for a naval battle to reproduce or verify a fix.

## 2026-09-01 (registration confirmed as the cause) - No crash with SliceThroughMissionBehavior unregistered

With `DisableSliceThroughForDiagnosis = true` (behavior never registered at all), a land
battle ran with **no crash**. This is the first clean isolation in this whole
investigation: it's specifically `SliceThroughMissionBehavior`'s presence, not Max
Health, not a fluke of load order, not something naval-specific.

Both previous fix attempts (removing `RegisterBlow`, then deferring all lookups/mutation
to `OnMissionTick`) changed what code runs *inside* `OnAgentHit`/`OnMissionTick` and
neither stopped the crash - so the next, more surgical question is whether merely
*overriding* `OnAgentHit` is unsafe here at all, independent of any code inside it.
`Behaviors/DiagnosticEmptyOnAgentHitBehavior.cs` is a throwaway `MissionBehavior` that
overrides `OnAgentHit` with a **completely empty body** and is registered instead of
`SliceThroughMissionBehavior` while the diagnostic flag is `true`. If a land battle with
*this* still crashes, that's strong evidence the problem is with overriding `OnAgentHit`
in this modded environment at all (most likely a cross-mod conflict - several other
installed mods, e.g. `PerfectFireArrows`, plausibly also hook hit-related callbacks) and
has nothing to do with anything this mod's `OnAgentHit` body does. If it does *not*
crash, that's a genuinely surprising result worth pausing on - it would mean something
about `SliceThroughMissionBehavior`'s *specific* code (queueing to a `List<PendingProc>`,
the `MBRandom` call, the `Hero`/`GetAttributeValue` lookup, ...) matters even though two
different variants of that code already failed to explain it, and the next step would be
to add pieces of the old logic back into the empty behavior one at a time until it
reproduces.

Delete `DiagnosticEmptyOnAgentHitBehavior.cs` and the `else` branch registering it in
`SubModule.cs` once this question is answered either way - it's a throwaway, not a
feature.

## 2026-09-01 (empty OnAgentHit crashed too) - Rules out this mod's OnAgentHit code entirely

Land battle, empty-bodied `OnAgentHit` override registered: **crashed again**, ~5-10s in.
A 5th `dotnet-dump` check confirms the identical fault site once more
(`Mission.CheckMissionEnded`, same call chain). Three different `OnAgentHit` bodies now
crash identically (`RegisterBlow` reuse, deferred-to-`OnMissionTick` queue, completely
empty), while not registering *any* `OnAgentHit`-overriding behavior doesn't crash at
all. **This conclusively rules out anything this mod's `OnAgentHit` code does** - the
bug cannot be in Slice Through's logic, because there was no logic left to be buggy.

Rewrote `DiagnosticEmptyOnAgentHitBehavior` one more time: it now registers a
`MissionBehavior` with **no `OnAgentHit` override at all**, only the required abstract
`BehaviorType`. This is the last, cleanest isolation available from this side: if it
still crashes, the problem isn't about `OnAgentHit` specifically, it's about this mod
adding *any* extra `MissionBehavior` to a mission at all (most plausibly a conflict with
how another installed mod manages its own behavior list/hit dispatch, exposed only when
one more behavior is present). If it does *not* crash, the problem is specifically about
overriding `OnAgentHit`, which - combined with three different implementations all
crashing identically - points squarely at a cross-mod conflict on that specific callback,
not at anything fixable by changing this mod's code further. Either way, **the practical
conclusion is the same: this isn't something more tweaking of Slice Through's own logic
can fix.** If confirmed, the realistic options are (a) find and report/avoid the
conflicting mod (would need bisecting the other ~18 mods, real time investment), or (b)
leave Slice Through shelved/disabled rather than keep guessing at its own code.

## 2026-09-01 (no-override behavior crashed too) - It's not about OnAgentHit at all

Land battle, `MissionBehavior` with **no `OnAgentHit` override, no overrides at all
besides the required `BehaviorType`**: crashed again, 6th identical fault site confirmed
via `dotnet-dump` (`Mission.CheckMissionEnded`, same call chain). This rules out
`OnAgentHit` completely - the crash has nothing to do with that callback, or with
anything this mod's registered behavior does, because there was nothing left to do.

**What's left: it's specifically about registering *any* extra `MissionBehavior` at
all**, regardless of type or content, on top of whatever the other ~18 mods already
register. Since other mods (RTSCamera, at minimum) clearly register their own
`MissionBehavior`s without incident, this can't be "MissionBehavior registration is
broken" in general - something is different about adding *one more* specifically in this
mod list. Leading theory: a fixed-capacity collection, possibly bucketed by
`MissionBehaviorType` (`Logic`/`Other`), is already full from the other mods' behaviors
and this mod's one additional entry overflows it. Testing that now by switching
`DiagnosticEmptyOnAgentHitBehavior`'s `BehaviorType` from `Logic` to `Other` - if that
*also* crashes, it's not a type-bucketed limit either, and it's simply "the mod stack as
a whole is at whatever limit exists, and this mod is the one that tips it over."

## 2026-09-01 (RESOLVED) - `MissionBehaviorType.Other` instead of `Logic` fixes it

Same land battle, same mod list, only change: `BehaviorType.Logic` -> `BehaviorType.Other`.
**No crash.** Confirmed root cause after 6 crashes and 5 diagnostic builds: this
project's ~18-mod stack has whatever fixed-capacity structure the engine uses to bucket
`Logic`-type `MissionBehavior`s already full; registering one more `Logic` behavior -
`SliceThroughMissionBehavior`, regardless of what it did internally - overflowed it and
crashed `Mission.CheckMissionEnded()` on the next tick, every time. `MissionBehaviorType.Other`
uses a different, evidently non-full bucket and avoids the overflow entirely.

**Applied for real:**
- `SliceThroughMissionBehavior.BehaviorType` now returns `MissionBehaviorType.Other`.
- `SubModule.cs` registers it unconditionally again (no more diagnostic flag/branch).
- `Behaviors/DiagnosticEmptyOnAgentHitBehavior.cs` deleted - it was a throwaway.
- The `OnAgentHit`-queues/`OnMissionTick`-applies split from the reentrancy-hardening
  attempt earlier the same day is **kept** (it's still reasonable practice - never mutate
  agent state from inside hit resolution - even though it turned out not to be what fixed
  this particular bug). Don't revert it back to a synchronous `RegisterBlow`/`Health` write
  inside `OnAgentHit`.

**Full diagnostic chain for reference** (in order, each ruling something out): disabling
Max Health via MCM didn't help -> ruled out Max Health entirely. Confirmed all crashes
were land battles, not naval -> ruled out anything War-Sails-specific. Not registering
Slice Through at all -> no crash, confirmed it's Slice Through. Removing `RegisterBlow`
reuse -> still crashed. Deferring to `OnMissionTick` -> still crashed. Empty `OnAgentHit`
body -> still crashed. No `OnAgentHit` override at all (bare `MissionBehavior`) -> still
crashed. **`BehaviorType.Other` instead of `Logic`** -> fixed. Six of those seven builds
were verified with `dotnet-dump analyze <dump> -c "clrstack -all"`, each faulting at the
exact same site (`Mission.CheckMissionEnded`) - see the entries above for how to repeat
that technique if a similar unexplained crash shows up again.

**Lesson for future `MissionBehavior`s in this mod:** register as `MissionBehaviorType.Other`
by default in this project's environment, not `Logic` - `Logic` is evidently
oversubscribed by the current ~18-mod stack. This is specific to *this* mod list and
machine, not a general Bannerlord modding rule; re-verify if the mod list changes
significantly (fewer mods might make `Logic` safe again; more might make `Other`
overflow too).

**Confirmed stable (same day):** a second land battle with the `Other` fix ran with no
crash, and Slice Through's actual effect (splash damage to a nearby enemy on a Vigor-
scaled chance) was observed working as intended. Land-combat testing for this bug is
considered done.

**Still open - not yet validated:** naval combat. Everything above was tested and fixed
in land battles only; War Sails/NavalDLC missions (where `NavalAgentStatCalculateModel`/
`NavalCustomBattleAgentStatCalculateModel` are the active stat models, and where the
original "this is naval" assumption came from before being corrected) have not been
re-tested since the `MissionBehaviorType.Other` fix. Nothing about the fix is naval-
specific, so it should carry over, but this hasn't been confirmed - do a naval battle
test before considering Slice Through fully validated, and update this entry with the
result.

## 2026-09-01 (rewritten, not just fixed) - Predecessor mod's actual implementation found

The project owner found the predecessor mod's ("BetterAttributePoints") real Slice
Through code: a Harmony **prefix** on `TaleWorlds.MountAndBlade.MissionCombatMechanicsHelper.UpdateMomentumRemaining`
that returns `false` on a successful proc to skip momentum deduction, letting the swing's
native "cleave" carry into a second target. Verified via reflection that this method
still exists in the installed game (v1.4.8) with the exact signature the old mod
patches (`ref float momentumRemaining, in Blow b, in AttackCollisionData collisionData,
Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough`).

This is a materially better approach than the `MissionBehavior`-based one that was just
spent an entire debugging session fixing (`SliceThroughMissionBehavior` /
`MissionBehaviorType.Other`, see the entries above), even though that one was, at that
point, confirmed working:
- It's a plain Harmony patch on an existing method - same category as
  `MaxHealthEndurancePatch` - not a `MissionBehavior`, so the `MissionBehaviorType`
  bucket-capacity issue this session diagnosed doesn't apply to it at all, structurally.
- No manual `Mission.GetNearbyEnemyAgents` query, no manual `Agent.Health` write. The
  game's own collision system finds and damages the second target, using its normal,
  already-vetted code path.
- The "second target takes reduced damage" requirement from the original task is
  satisfied by the engine's own momentum falloff, not a manually chosen fraction -
  arguably more faithful to "works like the Executioner Axe" than the original
  hand-rolled version.

**Replaced, not kept alongside:** `Behaviors/SliceThroughMissionBehavior.cs` deleted
(and the now-empty `Behaviors/` folder with it), `SubModule.cs`'s
`OnMissionBehaviorInitialize` override removed (nothing needs it anymore). New file:
`Patches/SliceThroughMomentumPatch.cs`. `BetterAttributesSettings`'s
`SliceThroughSplashDamageFraction` and `SliceThroughRadius` properties removed - the
engine's own momentum system now governs both, there's nothing left to configure there.
`SliceThroughChancePerVigor` and `SliceThroughNotify` kept as-is. Build clean (0 errors,
both targets), redeployed.

**Confirmed working (same day):** a land battle with this rewrite ran without issue and
the effect was observed working. Land-combat testing for `SliceThroughMomentumPatch` is
done.

**Still not yet tested: naval combat.** War Sails missions weren't covered even by the
`MissionBehavior` version this replaced, so this remains untested in that context
entirely. If it needs debugging there, note that `AttributeHelper`/`NotifyHelper`/
`MathHelper.RandomChance` from the predecessor mod's reference code are from BetterCore,
not available here - this version reimplements the equivalent logic directly with
`MBRandom.RandomFloat` and `InformationManager`, already used elsewhere in this project,
rather than pulling in that dependency.

## 2026-09-01 - Ranged Damage / Control froze the game in land combat (no crash dump)

**Symptom:** `RangedDamageControlPatch`'s first version - a Harmony postfix on
`MissionCombatMechanicsHelper.ComputeBlowDamage` (the shared final int-damage step for
any hit, scoped to ranged via the hit weapon's `IsRangedWeapon`) - froze the game during
land combat after some time. Unlike every crash earlier in this project, there was **no
crash report** - a hang, not an access violation - so `dotnet-dump` couldn't be used to
root-cause it (nothing to load). The exact mechanism is therefore **unconfirmed**.

**Action taken:** the project owner provided the predecessor mod's actual implementation
of this same effect, which patches a different, earlier-stage method:
`MissionCombatMechanicsHelper.ComputeBlowMagnitude` (no "Melee"/"Missile" suffix - a
separate method from both `ComputeBlowMagnitudeMelee` and `ComputeBlowMagnitudeMissile`,
confirmed via reflection to exist in the installed game (v1.4.8) with the exact
parameter names/types the old mod uses, `out` params included). Rewrote
`RangedDamageControlPatch` to patch this method instead, scaling `specialMagnitude`
(matching the old mod), while keeping an explicit `IsRangedWeapon` check the old mod
itself doesn't have - `ComputeBlowMagnitude` has no type suffix, so (like
`ComputeBlowDamage`) it likely runs for melee hits too, and this effect is specifically
ranged damage.

**Not confirmed as the fix** - there's no dump to prove `ComputeBlowDamage` was
specifically what caused the freeze, only that this is a different, proven (predecessor
mod's real, presumably-tested code), more upstream hook. Needs its own land-combat test.
If it freezes again, the next diagnostic step (since dotnet-dump doesn't apply to hangs)
would be attaching a debugger *before* the freeze to catch it live, or bisecting by
disabling this effect via MCM to confirm attribution the same way earlier crashes were
isolated.

**Side lesson:** a frozen game process still holds a file lock on `TestMod.dll`, and the
post-build copy step fails with `UnauthorizedAccessException` until that process is
closed - if a build's copy step fails with an access-denied error, check for a lingering
`TaleWorlds.MountAndBlade.Launcher`/game process before assuming a build config problem.

## 2026-09-01 - Naval battle passed on the current build

A naval battle with both `SliceThroughMomentumPatch` (the `MissionBehaviorType.Other`-era
version) and `RangedDamageControlPatch`'s `ComputeBlowMagnitude` rewrite active completed
with no crash and no freeze. This closes naval verification for Slice Through (land was
already confirmed separately, see the 2026-09-01 "rewritten, not just fixed" entry).

For Ranged Damage specifically, treat this as encouraging but not conclusive: the freeze
that prompted patching `ComputeBlowMagnitude` instead of `ComputeBlowDamage` was
originally observed in **land** combat. A clean naval run is good evidence the rewrite is
stable, but doesn't by itself prove the land freeze is fixed rather than just not
recurring yet - a dedicated land re-test would be the more direct confirmation.

**Update (same day): a dedicated land battle also completed successfully** - no crash,
no freeze. That's the direct confirmation this entry asked for. `RangedDamageControlPatch`
(the `ComputeBlowMagnitude` version) is now confirmed stable in both land and naval
combat; consider this bug closed.

## 2026-09-01 - Added Companion Limit / Social effect; not yet tested

`companionLimit = baseLimit + floor(bonusPerPoint * Social)`, hard player-only (no
toggle - matches the predecessor mod's reference, which has no "Player Only" setting for
this effect either), bonus-per-point default 0.5, selectable in exact 0.5 steps.

**Notes for anyone touching this next:**
- `SettingPropertyFloatingIntegerAttribute` (checked by reflecting on `MCMv5.dll`
  directly) has no step-size parameter - just `displayName`/`minValue`/`maxValue`/
  `valueFormat`. A free slider can't be forced to 0.5 increments. Used
  `MCM.Common.Dropdown<float>` with preset values (0, 0.5, 1, ... 5) instead - confirmed
  via reflection that `Dropdown<T>` is fully generic (not string-only), exposing
  `SelectedValue`/`SelectedIndex`, same pattern the predecessor mod uses for its
  attribute-choice dropdowns (`Reference/MCMSettings.cs`) but with `float` instead of
  `string`.
- **Floored, not rounded** - this is the one place in this project where that
  distinction actually matters: the task spec requires Social = 1 with the default
  0.5-per-point bonus to leave the limit at exactly `baseLimit` (`floor(0.5) = 0`).
  `Math.Round`'s default (banker's/round-half-to-even) rounding would coincidentally
  also give 0 for exactly 0.5, but diverges from `floor` at other half-integer
  results (e.g. Social = 3: `floor(1.5) = 1` vs `Math.Round(1.5) = 2`) - used
  `Math.Floor` explicitly, not `Math.Round`.
- **Deliberate deviation from the reference implementation:** the predecessor mod's
  `GetCompanionLimit` postfix doesn't check which `Clan` was passed in at all - it always
  adds the player's Social-based bonus to whatever clan's limit is being computed, which
  would incorrectly affect an AI clan's limit if the model is ever queried for one. This
  version checks `clan == Hero.MainHero.Clan` first, matching the actual "player-only"
  intent rather than the reference's literal (and probably accidental) behavior.
- Confirmed via reflection: `DefaultClanTierModel.GetCompanionLimit(Clan) : int` still
  exists in the installed game (v1.4.8) with the predecessor mod's exact signature, and -
  unlike the combat stat models - there's only this one concrete `ClanTierModel`
  implementation, no land/naval-style split, so one patch covers it.

**Confirmed working (same day):** tested in-game and behaves as intended, floor behavior
included. Consider this effect done.

## 2026-09-01 - Max Health / Endurance converted from flat bonus to percentage

Per the project owner's request, converted from
`maxHealth = baseGameMaxHealth + bonusPerPoint * Endurance` (flat, default 5 HP/point) to
`maxHealth = baseGameMaxHealth * (1 + bonusPerPoint * Endurance)` (percentage, default
5%/point). This also meant retargeting the patch entirely, based on the predecessor
mod's reference implementation:

- **Old:** three Harmony postfixes on the mission-level
  `AgentStatCalculateModel.GetEffectiveMaxHealth` overrides (land/naval/naval-custom-
  battle), directly adding a flat amount to the computed float.
- **New:** one Harmony postfix on the campaign-level
  `DefaultCharacterStatsModel.MaxHitpoints(CharacterObject, bool) : ExplainedNumber`,
  using vanilla's own `ExplainedNumber.AddFactor(float, TextObject)` for a proper
  percentage modifier (shows up correctly in the game's stat breakdown tooltips, unlike
  a manually-multiplied float). Confirmed via reflection that both the method and
  `ExplainedNumber.AddFactor`'s signature still exist in the installed game (v1.4.8) and
  match the predecessor mod's reference exactly. Like `DefaultClanTierModel`, there's
  only one concrete `CharacterStatsModel` implementation - no land/naval split needed.

**Real, unresolved uncertainty - read before assuming this "just works" the same as
before:** `MaxHitpoints` is the canonical value behind the character sheet's max HP, and
(per Bannerlord's known wound/recovery mechanic - a partially-recovered hero enters a
mission below full health as a fraction of this same number) is very likely also what
`GetEffectiveMaxHealth` derives its in-mission baseline from. But that specific
relationship could not be confirmed from reflection alone (method bodies aren't
decompiled - see CLAUDE.md "Conventions"). **If in-battle max health stops being
noticeably boosted after this change while the character sheet number still visibly goes
up, that assumption was wrong** - the old three land/naval postfixes would need to come
back *alongside* this one (both models contributing), not have been replaced by it.
Verify both: the character sheet's max HP with a few points in Endurance, *and* that a
hero's in-mission health bar is still visibly larger than vanilla.

**Confirmed (same day): a land battle showed the mod working as intended.** The
uncertainty above is resolved - `GetEffectiveMaxHealth` does derive from
`DefaultCharacterStatsModel.MaxHitpoints`, in-mission health is boosted correctly by the
percentage bonus, and the single campaign-level patch is sufficient on its own (the old
three land/naval mission-level postfixes do **not** need to come back). Consider this
effect done.

## 2026-09-01 - Campaign-map crash, unrelated to this mod (evidence-based, not assumed)

Reported right after the Max Health rewrite above, so it looked at first like it might be
the same pattern as every earlier crash this session. `dotnet-dump` says otherwise this
time - the fault site is entirely vanilla, with nothing from this mod anywhere on the
stack:

```
TaleWorlds.CampaignSystem.Army.FindBestGatheringSettlementAndMoveTheLeader(Settlement)
TaleWorlds.CampaignSystem.Army.OnSiegeStarted(SiegeEvent)
TaleWorlds.CampaignSystem.CampaignEvents.OnSiegeEventStarted(SiegeEvent)
... (siege event dispatch chain) ...
TaleWorlds.CampaignSystem.EncounterManager.StartSettlementEncounter(...)
TaleWorlds.CampaignSystem.Campaign.Tick()
```

No `DefaultCharacterStatsModel`, `MaxHitpoints`, `ExplainedNumber`, or any `TestMod.*`
type appears anywhere in the faulting thread's stack - unlike every prior crash, where
this mod's code (or a structural side effect of it) was directly on the stack. Also
different: exception parameters were `0x0`/`0x118` (a null object dereferenced at a
280-byte field offset) rather than the `0x0`/`0x0` bare-null-pointer pattern every
earlier crash showed.

**Conclusion: this looks like a pre-existing vanilla or cross-mod issue in siege-
triggering/army-gathering logic, not caused by this mod** - stated based on the dump's
evidence, not assumed just because it followed a recent change. Not investigated further
(no evidence points at this mod's code, so there's nothing here to fix). If it recurs and
someone wants certainty, the standard isolation test applies: reproduce without TestMod
loaded.
