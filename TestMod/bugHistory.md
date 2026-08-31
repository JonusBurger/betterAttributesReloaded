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
