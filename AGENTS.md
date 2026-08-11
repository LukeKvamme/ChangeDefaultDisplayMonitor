# AGENTS.md

Standing instructions for any agent working on this Risk of Rain 2 mod
project. Prepended to the system prompt.

Two MCP servers are available:

- **`ror2-codebase`** — what the game does. Decompiled source, IL call graph,
  verified hook signatures, offline wiki, LLM-written type cards.
- **`ror2-devloop`** — whether your code works. Build, deploy, launch, hot
  reload, in-game console, BepInEx log.

---

## 1. Grounding — the reason this project exists

Your training data about Risk of Rain 2 modding is unreliable. It contains
deprecated R2API calls, hook signatures that were never correct, and methods
that do not exist. The tools exist because you cannot tell which of your
recollections are true.

**Fetch before asserting.** If you are about to state a fact about a RoR2
type, member, or API, fetch it first. If you cannot fetch it, say you are
uncertain rather than filling the gap.

**Never emit an `On.` or `IL.` hook that did not come back from
`get_hook_signature`.** Cite the tool result — quote the `on_event` and
`orig_delegate` you were given. MonoMod disambiguates overloads by appending
parameter type names (`TakeDamageForce_DamageInfo_bool_bool`); these are
unguessable and you will get them wrong from memory.

**Never write code against a member without fetching its source via
`get_member`.** Atlas cards are pointers, not sources. A card's `summary` and
`gotchas` are LLM-written prose about the code — useful for orientation,
never a substitute for reading it.

**Distinguish "does not exist" from "not indexed."** When a tool returns
empty, call `db_status` before concluding anything. Empty `hooks`,
`callgraph`, or `atlas` means that pass has not run, not that the answer is
no.

**Check `get_changelog` before using any R2API or game API pattern you
recall from training.** The SotS → Alloy Collective cycle (1.3.7 → 1.4.1)
broke a great deal. Treat pre-1.4 patterns as suspect until verified.

### Which tool for which question

| Question | Tool |
|---|---|
| "what type handles X" | `search_atlas` |
| "where is X implemented" | `search_code` |
| "what exactly does this do" | `get_member` |
| "what can I hook here" | `get_hook_signature` |
| "where do I intercept this" | `find_callers` |
| "what does this call" | `find_callees` |
| "what subclasses this" | `find_implementors` |
| "how do modders normally do X" | `wiki_search` |
| "did this change last patch" | `diff_versions`, `get_changelog` |

`search_atlas` first for orientation, then `get_member` for truth. Do not
skip the second step.

### Overloads

Members have two names. `fqn` is human-readable and **not unique**;
`sig_fqn` includes parameter types and is unique. When `get_member` returns
`{"error": "ambiguous"}`, pick from the `overloads` list rather than
guessing.

---

## 2. Networking — the dominant failure mode

RoR2 is multiplayer. Code that works flawlessly in single-player and desyncs
in a lobby is the most common defect in mod code, and it fails silently.

**Every state mutation gets a `NetworkServer.active` guard**, unless you have
established the code path is already server-only.

**State the client/server split explicitly** for anything touching gameplay
state. Say which side runs it and what the other side sees. If you do not
know, use `get_member` and read the guards.

**Check `is_static` on any hook** — the `orig_` delegate takes a leading
`self` parameter for instance methods and does not for static ones.

**Fields marked `[SyncVar]` are server-authoritative.** Assigning the field
directly bypasses synchronisation; use the generated `Network<field>` setter
or the method that owns the mutation.

**`[Command]` runs client → server, `[ClientRpc]` runs server → clients.**
`CallCmd*` and `InvokeCmd*` members are UNet weaver plumbing — never hook or
call them directly.

**Prefer `On.` hooks. Justify `IL.` hooks explicitly** — they break on patches
in ways `On.` hooks do not.

---

## 3. Iteration loop

Two paths with very different costs. Pick deliberately.

**`hot_reload()` — seconds.** Rebuilds and reloads into the running game via
ScriptEngine. Use for hook *behaviour*: damage math, proc conditions, effect
triggers, tuning values.

**`build_deploy_run()` — a minute or two.** Full restart. **Required for any
content registration change** — `ItemCatalog`, `BuffCatalog`, `BodyCatalog`,
`SurvivorCatalog`, `SceneCatalog` and friends are sealed during startup, so a
new or modified `ItemDef`, skill, survivor, or stage will not appear on a hot
reload. It will silently show the old version, which is worse than an error.

**The console bridge is orthogonal to both.** `console(...)` drives the
in-game console in whatever session is running. For content work this is
where the time savings actually are — the restart is cheap compared to
manually starting a run, reaching the right stage, and acquiring the item.
Drive to the test state instead of playing to it.

### Standard sequence

```
build → deploy → mark_log → launch → wait_for_log(guid) → console(setup) → observe
```

**Always `mark_log()` before launching.** `LogOutput.log` accumulates across
sessions. Without a mark you will read errors from a previous run and debug a
problem that no longer exists.

**A mod that fails to load produces silence, not an error.** BepInEx catches
plugin exceptions, logs them, and continues. The game runs perfectly and your
code never executes. So:

- Confirm loading with `wait_for_log(<your plugin GUID>)` — a positive
  signal, not the absence of a negative one.
- When "nothing happens," call `get_exceptions()` first. Check
  `in_your_plugin` on each frame to tell your bug from the game's.

**Never conclude a change worked without reading the log.** "It should work
now" is not a result.

---

## 4. Plugin hygiene

**Always implement `OnDestroy` and undo everything `Awake` did.** This is not
optional once ScriptEngine is installed:

```csharp
private void Awake()
{
    On.RoR2.HealthComponent.TakeDamage += MyHook;
    Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} loaded");
}

private void OnDestroy()
{
    On.RoR2.HealthComponent.TakeDamage -= MyHook;
    Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} unloaded");
}
```

Without it, every hot reload **stacks** another copy of every hook. The old
assembly's delegates stay registered and keep firing. This presents as your
math being wrong — damage modified twice, then three times — not as an error.
Also unpatch Harmony patches, destroy created `GameObject`s, and unsubscribe
from static events.

**Log loudly at load.** A distinctive line in `Awake` is what
`wait_for_log` keys on.

**The plugin DLL lives in exactly one place** — `BepInEx/scripts/` for hot
reload, or `BepInEx/plugins/`, never both. Two copies load twice.

---

## 5. Content additions

Skins, survivors, skills, items, stages. Extra care because failures here are
quiet.

**Registration timing is load-bearing.** Content must be registered before
the relevant catalog initialises. Verify the correct hook or R2API entry
point with `get_hook_signature` and `wiki_search` — do not assume `Awake` vs
`Start` vs a catalog init hook from memory.

**Read a working vanilla example first.** For a new item, `get_type` on an
existing `ItemDef` and its registration path teaches more than any
description. Use `find_callers` to see how the game itself wires it up.

**Asset loading and Addressables keys are exact strings.** Use `grep` to find
the real key rather than constructing one that looks plausible.

**Verify in-game, not in the log.** A registered item that never drops, a
skin that shows the wrong mesh, a stage with no navigation nodes — all of
these load without complaint. Use the console to force the content to appear
and confirm visually.

**For stat modifications use `RecalculateStatsAPI`**, not direct field
assignment on `CharacterBody`. Confirm the current API shape with
`get_changelog` and `wiki_search` before writing it.

---

## 6. Communication

**Cite tool results for factual claims.** Quote the signature you were given,
name the member you fetched. If a claim came from your own knowledge rather
than a tool, mark it as unverified.

**Say when you are guessing.** An honest "I need to check X" is more useful
than a fluent wrong answer, and this domain punishes fluent wrong answers
specifically.

**The user is an experienced programmer new to C#, Unity, and this
codebase.** Do not explain language features. Do explain Unity lifecycle
semantics, RoR2 architecture, and netcode implications — those are the actual
gaps.

**Report the log, not your expectations.** When you have run something,
say what the log showed.

---

## Appendix: things learned the hard way

Add to this as you hit them. Each line here cost real debugging time.

- **A card is not a source.** An atlas card once described `DamageInfo` as a
  struct; it is a class. The model was right about everything it had been
  shown and wrong about the one thing it inferred. That is the shape of every
  error in this domain.
- **Hooks exist for private methods.** `TakeDamageProcess` is private and
  hookable, and it is where the damage math actually lives — `TakeDamage` is
  just the entry point.
- **`isHealthLow` is `(health + shield) / fullCombinedHealth <=
  lowHealthFraction`** — includes shield, excludes barrier. Items keying on
  "low health" behave counter-intuitively because of this.
- **`alive` is `health > 0f` only.** Shield and barrier do not count as life.
- **A hook with no chunk is still a valid hook.** Constructors and delegate
  members exist in IL but not in ILSpy output, so `get_hook_signature` can
  return a real hook whose source is not in the index.
- **`null` into an overloaded method is a compile error, not a runtime one.**
  `RoR2.Console.SubmitCmd` has `NetworkUser` and `CmdSender` overloads;
  passing bare `null` is CS0121. Always cast: `(NetworkUser)null`. When
  `get_member` returns `{"error": "ambiguous"}`, that ambiguity may exist
  for the compiler too, not just for the lookup.
- **`find_callers` on an API you're about to use finds the game's own
  reference implementation.** Before writing a call, look at who already
  calls it and read the closest analogue. For `Console.SubmitCmd` that was
  `RoR2.UI.ConsoleWindow.Submit` — the in-game console doing precisely what
  the DebugBridge needed to do.
- **R2Boilerplate targets an older game build.** `ItemDef._itemTierDef` is
  private on 21587608; use the public `tier` property, whose setter resolves
  through `ItemTierCatalog`. Treat any external template as an unverified
  source and check each API with `get_member` first.
- **`ItemDef.tier`'s setter silently no-ops if `ItemTierCatalog` isn't
  populated yet** — `_itemTierDef` stays null and the getter falls back to
  `deprecatedTier` (Tier1). An item appearing as the wrong tier is a
  registration-timing bug, not a value bug.
- **Never launch the game while an instance is running.** `launch_game()`
  kills the old one by default. Two instances fight over the profile's
  LogOutput.log, and the second DebugBridge cannot bind its port — so
  `console()` talks to the OLD process while you read logs from the new one.
  Nothing errors; the results are just silently wrong.
- **Kilo's MCP config key is `mcp`, not `mcpServers`.** Tool wrappers rename
  things. Read the tool's own docs rather than assuming the general
  convention applies.
  - **One deployment mechanism per project.** Either an MSBuild post-build copy
  or `deploy()`, never both — two copies of the same plugin in `plugins/` and
  `scripts/` load twice, and the symptom is every hook firing twice rather
  than an error.
- **Starting a run from the console (this profile):** `host 0` →
  `gamemode ClassicRun` → `pregame_start_run`, in that order. Recorded in
  `run_start_sequence`; call `start_run()`. Verify with `console("__run")`.
  `run_print_seed` is verification only; `app_info` is not needed.
- **When the agent doesn't know a console command, the answer is
  `console_help(filter=...)`, never a web search.** Commands come from
  whatever mods this profile has installed, so no external source can be
  authoritative and training data will be wrong or stale.
  - **`stage1_pod 0` skips the escape pod drop-in animation.** Insert after
  `host 0` in the start sequence. Saves several seconds per iteration.
- **A BepInEx install in the GAME directory shadows the r2modman profile.**
  Doorstop resolves `target_assembly` relative to the game dir, so without
  DOORSTOP_* env vars the game loads that one: its plugins, its log. Every
  devloop tool then reads a file nothing writes to — while `console()` keeps
  working, because the bridge is loaded from the shadowing install.
  `launch_game` now forces the profile via env vars; `profile_info()` reports
  `stale_bepinex_in_game_dir`.
- **When a tool reports nothing, verify the file it reads is the file being
  written.** "No output" and "reading the wrong path" are indistinguishable
  from inside the tool. Compare sizes and mtimes before debugging the code.
- **Doorstop precedence on Windows (4.0.0): ini < env < args, but env does
  NOT actually override the ini — tested.** Command-line args DO override it
  (verified: with `enabled=false` in the game-dir ini, launching with
  `--doorstop-enabled true --doorstop-target-assembly <profile preloader>`
  loaded BepInEx 5.4.21 and the profile log grew). Use `--doorstop-enabled`
  / `--doorstop-target-assembly` to point at a profile. Setting
  `enabled=false` in the game-dir ini lets Steam launch vanilla while devloop
  launches modded via args. The ini's `target_assembly` should stay empty so a
  stale profile path cannot resurface. `BepInEx.stale` in the game dir is
  inert (not named `BepInEx`, so Doorstop never resolves it) and can be
  deleted.
- **A .sln referencing no projects builds successfully and produces nothing.**
  Always set `csproj` to the .csproj file, never a directory — pointing
  `dotnet build` at a folder makes it pick up whatever .sln is there.
- **MCP servers are long-lived subprocesses; code changes need a client-side
  restart.** devloop's config hot-reloads on mtime, but edits to devloop.py
  do not take effect until the server is restarted in Kilo.
- **A UTF-8 BOM in `config.json` kills the devloop server at startup.** Some
  editors save with a BOM; `load_config()` (devloop.py:133) read with
  `encoding="utf8"`, which throws `JSONDecodeError: Unexpected UTF-8 BOM` —
  the server crashes before any tool registers, so all devloop tools
  silently disappear from the client. Now reads with `utf-8-sig` (BOM or
  not), but a config saved with a BOM is still worth stripping if the tools
  ever vanish again. Symptom: devloop tools missing while ror2-codebase
  works fine. Probe: run `devloop.py` standalone and watch it die in
  `load_config`.
- **`kill_all`/`kill`/`true_kill` cannot test on-kill effects.** They call
  `HealthComponent.Suicide` with no killer, so `DamageReport.attacker` is
  null and any `!report.attacker` guard returns early — the effect silently
  never fires, and it looks like the mod is broken. `onCharacterDeathGlobal`
  fires from `HealthComponent.TakeDamageProcess`, so a kill attributed to a
  body with inventory is the only path that exercises item logic. Have the
  player kill something, or drive damage through a console command that sets
  `DamageInfo.attacker`. Also: `spawn_ai Lemurian` spawns a **Devoted
  Lemurian** whose death controller NREs (`DevotedLemurianController.
  OnDevotedBodyDead`) — a vanilla SotS bug that aborts the death chain.
  Spawn a normal body if you need a clean death.