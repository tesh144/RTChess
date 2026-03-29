# New Unreal Project Pipeline — Unified Flow

> **Purpose:** A concrete, step-by-step pipeline for launching a new Unreal Engine 5 game project, combining everything learned from ClockworkCraft with the Claude Code Game Studios framework. Written for Jai's solo-dev + AI agent workflow.

---

## The Big Picture

You have two powerful systems that need to merge, not compete:

**From ClockworkCraft (battle-tested):** Trello-driven task management, the 3-file config pattern (CLAUDE.md / CLAUDE_USER_JAI.md / JAI_AI_SYNC.md), Google Sheets as data source of truth, the trelloworkflow skill for session discipline, and hard-won rules about data integrity, pre-flight checks, and "ask before building."

**From Claude Code Game Studios (structural framework):** 48 specialized agents in a studio hierarchy, 8 automated hooks for quality gates, 11 path-scoped coding rules, 29 document templates, and engine-specific specialists for Unreal (GAS, Blueprints, Replication, UMG/CommonUI).

The goal isn't to use all 48 agents — most of them are redundant for a solo dev. The goal is to take the *structure* (hooks, rules, templates, escalation paths) and wire it into the *workflow* (Trello, sync logs, session discipline) that already works for you.

---

## Phase 0: Repository Setup (Day 1, ~2 hours)

### 0.1 — Clone Game Studios as your starting point

```bash
git clone https://github.com/Donchitos/Claude-Code-Game-Studios.git MyNewGame
cd MyNewGame
```

Then strip what you don't need and add what you do. The repo gives you the directory skeleton (`src/`, `assets/`, `design/`, `docs/`, `tests/`, `prototypes/`, `production/`) which maps well to Unreal's structure — but you'll remap `src/` to your Unreal `Source/` directory.

### 0.2 — Unreal project inside the repo

Either create the Unreal project inside the cloned repo or overlay the `.claude/` directory onto an existing Unreal project. The key files to keep:

```
.claude/
  settings.json          ← hooks config, permissions, safety rules
  agents/                ← trimmed to ~15 agents (see §1 below)
  skills/                ← all 37 + your custom skills from ClockworkCraft
  hooks/                 ← all 8 hooks (adapt paths for Unreal)
  rules/                 ← remap to Unreal directory structure (see §2 below)
  docs/templates/        ← all 29 templates, they're engine-agnostic
```

### 0.3 — Port your 3-file config

Create these at the repo root:

**CLAUDE.md** — Project entry point. Copy the structure from ClockworkCraft's CLAUDE.md but update: project name, Trello board ID (new board), Google Sheets ID (if using data pipeline), skills table, Unreal-specific task routing. Add a reference to the Game Studios agent roster so Claude knows what specialists are available.

**CLAUDE_USER_JAI.md** — Drop in as-is. This file is portable by design. Zero modifications needed.

**JAI_AI_SYNC.md** — Start fresh. Empty Active Work, empty Completed Work, no pending flags. Clean slate.

### 0.4 — Set up Trello board

This is your task tracker and the backbone of every session. Claude's `trelloworkflow` skill should guide you through this at first launch, but here's the full spec so nothing gets missed.

#### Step 1: Create the board

Create a new Trello board named after the project. Add the board ID to CLAUDE.md so every agent can find it.

#### Step 2: Create lists (in this exact order, left to right)

| # | List name | Purpose |
|---|---|---|
| 1 | **Important Documents** | Pinned reference cards — design docs, key decisions, links to external resources. Not part of task flow. |
| 2 | **Maybe Later** | Parked ideas that aren't ready for action. Low-priority holding area. |
| 3 | **Suggestions** | Claude's proactive ideas for the project. Must stay topped up (minimum 6 cards). Jai promotes good ones to Tasks. |
| 4 | **Tasks (Claude)** | Work for Claude to do. Jai adds cards here. Claude checks the card to acknowledge, works it, then moves to Ready for Review. |
| 5 | **To Do (Humans)** | Manual steps Claude can't do — playtesting, visual checks, Inspector wiring, device testing. Claude creates these. |
| 6 | **Ready for Review** | Claude's finished work awaiting Jai's review. Card is unchecked until Jai approves. |
| 7 | **Complete** | Done log. Approved work lives here. Descriptions can be stripped — this is just the record. |

#### Step 3: Create the full label set

Replicate these labels before creating any cards. Every card must have at least one.

| Label | Color | Use for |
|---|---|---|
| Feature | Green | New functionality |
| Polish | Yellow | Refinement of existing stuff |
| Bug | Red | Something broken |
| Visual | Light Blue | Art, UI, effects |
| Behavior | Blue | Gameplay logic, AI, state machines |
| System | Sky | Infrastructure, managers, pipelines |
| Balance | Orange | Tuning numbers, economy, progression |
| Creative | Pink | Design ideas, narrative, theme |
| Architecture | Lime | Code structure, refactors |
| Concept | Purple | Early-stage ideas not yet scoped |
| Discuss | Purple | Needs conversation before action |
| Blocked | Red | Can't proceed, waiting on something |
| Actionable | Black | Card is fully scoped — Claude can execute without further clarification |
| AI Instructions | Black (dark) | Cards that contain rules or context for AI agents |
| Re-do | Black (dark) | Work that needs to be redone |
| Fleshout | Blue (dark) | Card description needs expanding before it's actionable |
| UI | Pink (dark) | UI-specific work |

#### Step 4: Pin description cards

Create a description card as the **first card** in each list explaining what the list is for and how cards should flow through it. These are permanent — never move, edit, or archive them. They help new agents (and future-you) understand the board at a glance.

#### Step 5: Seed Important Documents

Create cards in Important Documents for:
- Link to this pipeline doc (`NEW_PROJECT_PIPELINE.md`)
- Link to the project's GDD (once written)
- Link to the Google Sheets data source (once created — see §0.5)
- Any external references (engine docs, art style guides, etc.)

#### Step 6: Wire it up

Add the board ID to CLAUDE.md under `## Project Config`:
```
- **Trello:** [Project Name] board — board ID `[paste ID here]`
```

The `trelloworkflow` skill reads this ID at session start. Once it's in CLAUDE.md, every agent knows where to find the board.

#### Step 7: Verify with trelloworkflow

Run the `trelloworkflow` skill. It should scan the board, confirm all 7 lists exist, check label hygiene, and report the board is clean. If it flags anything, fix it now — this is your foundation.

### 0.5 — Set up Google Sheets (when you need a data pipeline)

**Don't do this on day 1.** Wait until your game actually has data-heavy systems that benefit from a spreadsheet as source of truth (unit stats, building costs, item tables, loot drops, etc.). When that day comes, here's the setup.

#### Why Sheets?

Google Sheets gives you a collaborative, human-readable data source that's easy to tweak during playtesting. The pattern: Sheets → sync script → engine-native data format (Unreal DataTables/DataAssets). Designers edit the sheet, the pipeline pulls it into the engine.

#### Step 1: Create the spreadsheet

Create a new Google Sheet named `[ProjectName] — Game Data`. Structure it with one tab per data domain. Common tabs for a strategy/RPG:

- **Units** — name, stats, costs, behaviors, icons
- **Buildings** — name, production, requirements, costs
- **Items** — name, type, effects, rarity, value
- **Progression** — level thresholds, unlock gates, XP curves
- **Economy** — resource generation rates, sink rates, exchange ratios
- **Environment** — biome types, tile properties, spawn rules

The exact tabs depend entirely on your game — this is why you wait until you know what systems you're building.

#### Step 2: Column conventions (lessons from ClockworkCraft)

These rules are hard-won. Follow them from the start:

- **Row 1 is always headers.** Every column has a clear, unambiguous name.
- **Use header names, not column indices, in your sync script.** ClockworkCraft's biggest recurring bug was the SheetSyncEditor using hardcoded column indices — inserting a column broke everything downstream. Your sync script should find columns by header name.
- **First column is always a unique ID.** Machine-readable, never changes once assigned (e.g. `unit_fighter`, `building_barracks`). Display names go in a separate column.
- **Keep one "source of truth" per row.** Don't split a single entity across multiple tabs unless there's a strong reason.
- **Use data validation on enum columns.** Dropdown lists prevent typos (e.g. a "DamageType" column should only allow values defined in your code enum).
- **Add an "Active" boolean column.** Lets you disable rows without deleting them — critical for iteration.
- **Icon/asset columns store asset names, not paths.** Paths are engine-specific and break on refactors. Store `T_Icon_Fighter` and let the sync script resolve the path.

#### Step 3: Build the sync pipeline for Unreal

The ClockworkCraft pattern was: Google Sheets → `SheetCache.json` (local cache) → `SheetSyncEditor.cs` (Unity editor script) → `.asset` files (ScriptableObjects).

For Unreal, the equivalent is:

**Google Sheets → local JSON cache → Unreal Editor Utility → DataTable / DataAsset**

The components:

1. **Fetch script** (Python or Editor Utility Widget) — Calls the Google Sheets API, downloads all tabs, saves as `Data/SheetCache.json` in your project. This is your local snapshot of the sheet.

2. **Sync script** (Editor Utility Blueprint or C++ UEditorUtilityBase) — Reads `SheetCache.json`, maps columns to struct fields **by header name** (not index!), and populates Unreal DataTables or DataAssets. Logs every row processed and any mismatches.

3. **Validation pass** — After sync, the script compares row counts (sheet vs DataTable), checks for null/empty required fields, and flags any active rows that didn't sync. Output to the Unreal log.

Key difference from ClockworkCraft: Unreal DataTables are CSV-backed by default, so you could also export directly from Sheets to CSV and reimport. But the JSON cache approach gives you an audit trail and a diffable file in git.

#### Step 4: Wire it up

Add the Sheet ID to CLAUDE.md:
```
- **Google Sheets:** [ProjectName] Game Data — ID `[paste sheet ID here]`
```

If your `trelloworkflow` skill has a Phase 2 (data sync), update it with the new sheet ID and the paths to your sync scripts.

#### Step 5: Document the column mappings

In your project doc (equivalent of `RTChess.md`), add a section mapping each sheet tab to its code struct. For each tab, list:
- Which Unreal struct/DataTable it populates
- Column-to-field mapping (by name)
- Any transformation rules (e.g. "Card column stores display name, code field stores enum value")
- Known gotchas

This documentation saves hours of debugging when columns inevitably shift or new fields get added.

#### Standing rule for all data work

> **Sheets are the source of truth.** Never modify a DataTable/DataAsset directly if it's managed by the sync pipeline. Change the sheet, run the sync. If the sync produces wrong results, fix the sync script — don't hand-edit the output.

---

## Phase 1: Which Agents to Actually Use

The full 48-agent roster is designed for a multi-person studio. For solo dev + AI, you want roughly 15 agents across two tiers. Here's the cut:

### Keep (Tier 1 — Directors)

| Agent | Why |
|---|---|
| **creative-director** | Guards game vision. Consult before any feature that changes core gameplay feel. |
| **technical-director** | Architecture decisions, performance budgets, code standards. Your escalation point for technical disagreements. |
| **producer** | Sprint planning, scope management, cross-system coordination. Maps directly to your Trello workflow. |

### Keep (Tier 2 — Leads)

| Agent | Why |
|---|---|
| **game-designer** | Mechanics, balance, progression. The "does this actually make the game better?" check. |
| **lead-programmer** | Code review, architecture patterns, refactoring decisions. |
| **qa-lead** | Test strategy, bug triage, quality gates before releases. |

### Keep (Tier 3 — Specialists, activate when needed)

| Agent | Why |
|---|---|
| **unreal-specialist** | Your engine expert. GAS for abilities, Blueprints for rapid prototyping, Replication if multiplayer, UMG/CommonUI for UI. |
| **gameplay-programmer** | Day-to-day implementation of game mechanics. |
| **ui-programmer** | UMG/CommonUI layouts, HUD, menus. |
| **systems-designer** | Economy, progression, resource loops. |
| **economy-designer** | If your game has any resource/currency system (ClockworkCraft definitely needed this). |
| **technical-artist** | Shaders, VFX, material pipelines, performance of visual systems. |
| **tools-programmer** | Editor tools, data pipeline automation, build scripts. |
| **prototyper** | Quick throwaway tests in `/prototypes/` — isolated from production code. |
| **performance-analyst** | Profiling, optimization passes, memory budgets. |

### Remove or ignore

The remaining agents (network-programmer unless you're doing multiplayer, ai-programmer unless you have AI enemies, localization-lead, community-manager, devops-engineer, analytics-engineer, security-engineer, live-ops-designer, sound-designer, writer, world-builder, etc.) — delete their agent files or just don't invoke them. They add noise for a solo project. You can always re-add them later.

### How this maps to your real workflow

You're not spawning 15 agents per session. In practice:

- **Session start:** `trelloworkflow` skill runs (board scan, card cleanup, acknowledge tasks). This is your producer.
- **Before new features:** `brainstorm` skill activates → consults creative-director + game-designer thinking.
- **During implementation:** You're primarily working with gameplay-programmer / unreal-specialist behavior.
- **Before merging:** `code-review` skill activates → lead-programmer + technical-director review.
- **Periodically:** `tech-debt`, `scope-check`, `balance-check` for hygiene.

The agent hierarchy is a *thinking framework*, not a literal multi-process system in Co-Work. When Claude runs `brainstorm`, it channels the creative-director's perspective. When it runs `code-review`, it channels the lead-programmer's standards.

---

## Phase 2: Remap Path-Scoped Rules for Unreal

The Game Studios rules assume a generic `src/` layout. Unreal projects have a different structure. Remap like this:

| Original Path | Unreal Path | Rule |
|---|---|---|
| `src/gameplay/**` | `Source/[Project]/Gameplay/**` | Data-driven values, delta time, no UI references |
| `src/core/**` | `Source/[Project]/Core/**` | Zero allocations in hot paths, thread safety |
| `src/ai/**` | `Source/[Project]/AI/**` | Performance budgets, debuggability |
| `src/networking/**` | `Source/[Project]/Networking/**` | Server-authoritative, versioned messages |
| `src/ui/**` | `Source/[Project]/UI/**` | No game state ownership, localization-ready |
| `design/gdd/**` | `design/gdd/**` | Keep as-is (not engine-specific) |
| `tests/**` | `Tests/**` or `Source/[Project]/Tests/**` | Test naming, coverage requirements |
| `prototypes/**` | `prototypes/**` | Relaxed standards, README required |

Edit the rule files in `.claude/rules/` to update the path globs. This takes 10 minutes but means coding standards are automatically enforced for every file touch.

---

## Phase 3: Hooks — What to Keep, What to Adapt

All 8 hooks are worth keeping. Here's what needs adapting:

| Hook | Adaption needed |
|---|---|
| `validate-commit.sh` | Works as-is. Checks for hardcoded values, TODO format, JSON validity. Add: check for hardcoded asset paths (Unreal uses soft references). |
| `validate-push.sh` | Works as-is. Warns on pushes to main/master. |
| `validate-assets.sh` | **Adapt.** Change from generic asset naming to Unreal conventions (BP_ prefix for Blueprints, T_ for textures, M_ for materials, SM_ for static meshes, SK_ for skeletal meshes). |
| `session-start.sh` | **Adapt.** Add: load Trello board context, check JAI_AI_SYNC.md for conflicts. This is where `trelloworkflow` Phase 1 integrates. |
| `detect-gaps.sh` | Works as-is. Detects missing docs when code exists. |
| `pre-compact.sh` | Works as-is. Preserves session notes on context compression. |
| `session-stop.sh` | **Adapt.** Add: update JAI_AI_SYNC.md with completed work, remind about Trello card state. |
| `log-agent.sh` | Works as-is. Audit trail of subagent invocations. |

---

## Phase 4: Templates — Your Document Library

All 29 templates are engine-agnostic and valuable. The ones you'll use most:

**Immediately (Concept phase):** GDD template, creative pillars template, MDA analysis template.

**Pre-production:** ADR template (you already use architecture-decision skill), sprint plan template, economy model template, faction/unit design template.

**Production:** Bug report template (already using bug-report skill), playtest report template, tech debt register template.

**Release:** Changelog template, patch notes template, release checklist template, launch checklist template.

Store filled templates in `design/` and `production/` directories. They become living docs that any agent can reference.

---

## Phase 5: The Session Flow (Day-to-Day)

Here's what a typical work session looks like with everything wired together:

### Session Start (automatic via hooks + trelloworkflow)

1. `session-start.sh` hook fires → loads recent git activity, sprint context
2. Claude reads CLAUDE.md → CLAUDE_USER_JAI.md → JAI_AI_SYNC.md → project doc
3. `trelloworkflow` skill runs Phase 1 → board scan, card cleanup, label hygiene, staleness check
4. Claude acknowledges tasks, identifies what to work on
5. Pre-flight checklist runs for target files

### Working on a Feature

1. **Card exists in Tasks (Claude)** → read card description + comments
2. **Is it a new feature?** → `brainstorm` skill first (creative-director + game-designer thinking)
3. **Is it architectural?** → `architecture-decision` skill → write ADR before coding
4. **Is the target file >500 lines?** → `code-review` skill first to understand it
5. **Is the system undocumented?** → `reverse-document` skill to map it
6. **Check card** (mark as executing) → implement
7. **Path-scoped rules** enforce standards automatically as you edit files
8. **Commit** → `validate-commit.sh` hook catches hardcoded values, bad TODOs, invalid JSON
9. **Uncheck card** → move to Ready for Review with brief note
10. Update JAI_AI_SYNC.md

### Periodic Hygiene

- **Every few sessions:** `tech-debt` skill → audit file sizes, TODO count, debt register
- **Every sprint:** `retrospective` skill → what worked, what didn't, action items
- **Before milestones:** `scope-check` on remaining cards, `gate-check` for quality bar
- **When balance feels off:** `balance-check` skill → economy/progression analysis

### Session End

1. `session-stop.sh` hook fires → logs accomplishments
2. Update JAI_AI_SYNC.md with final state
3. Trello cards reflect accurate state (checked/unchecked per timing rules)

---

## Phase 6: What to Bring from ClockworkCraft, What to Leave Behind

### Bring (proven valuable)

- **The 3-file config pattern.** CLAUDE.md + CLAUDE_USER_JAI.md + JAI_AI_SYNC.md. This is your best invention. It separates project config from personal preferences from cross-agent state. Game Studios doesn't have this — it's your upgrade to their system.
- **Trello workflow with checkmark timing.** Game Studios has sprint-plan and milestone-review but no task tracker integration. Your Trello system is more practical for day-to-day work.
- **"Ask before building" culture.** Game Studios' collaborative protocol (Ask → Present options → You decide → Draft → Approve) aligns perfectly with your existing rule. Keep enforcing it.
- **Google Sheets as data source of truth** (if the new game has data-heavy systems). The SheetCache.json pipeline concept is solid — just rebuild it for Unreal's data table format instead of Unity ScriptableObjects.
- **Pre-flight checklist.** "Does the target exist? How is it created? Who references it?" This catches so many bugs.
- **All 28 skills already installed.** They're engine-agnostic. Keep them all.
- **Label system and card-as-source-of-truth.** Rich cards with comments, checklists, and descriptions beat any other task format.

### Leave Behind (ClockworkCraft-specific)

- **Unity-specific sync patterns.** ScriptableObject databases, Inspector wiring reminders, AddComponent patterns. These don't apply to Unreal.
- **SheetSyncEditor column index pain.** If you rebuild a data pipeline, design it with column headers instead of indices from the start.
- **The dual-layer grid system documentation.** That's ClockworkCraft's architecture, not a transferable pattern.
- **Specific pending flags** in JAI_AI_SYNC.md (POIDatabase, FurnitureObject rename, etc.). Start clean.

### Upgrade (lessons that become rules)

- **"Never delete data without checking the source sheet"** → generalize to **"Never delete data without checking the authoritative source"** (could be sheets, could be Unreal data tables, could be a design doc).
- **"Files over 500 lines get code-review before touching"** → keep this, it prevented multiple incidents.
- **"Systemic changes need an ADR"** → keep this, now backed by the architecture-decision template from Game Studios.

---

## Phase 7: What NOT to Over-Engineer on Day 1

The biggest risk is spending a week setting up infrastructure instead of making a game. Here's the minimum viable setup:

**Do on Day 1:** Clone repo, trim agents to ~15, port 3-file config, create Trello board, remap rules to Unreal paths, run `/start`.

**Do in Week 1:** Fill out GDD template, write creative pillars, set up first sprint plan, get a playable prototype in `prototypes/`.

**Do when you actually need them:** Data pipeline (wait until you have data worth syncing), economy-designer agent (wait until you have a resource system), performance-analyst (wait until you have something to profile), launch/release skills (obviously).

The framework is there when you need it. Don't activate everything at once.

---

## Appendix: Full Skill Audit — What Fits, What Doesn't

You have 28 Game Studios skills installed plus your custom `trelloworkflow`. Here's the honest breakdown after reading every one of them.

### Fits Your Workflow As-Is (keep, use regularly)

| Skill | Why it fits | When |
|---|---|---|
| **brainstorming** | Hard-gates implementation until design is approved. Directly enforces your "ask before building" rule. Already part of your CLAUDE_USER_JAI.md workflow. | Before any new feature or modification |
| **code-review** | Checks SOLID principles, cyclomatic complexity, method length, dependency direction. Enforces your "files over 500 lines get reviewed" rule. | Before touching large files, before merging |
| **tech-debt** | Scans for TODOs, FIXMEs, HACKs, god objects, long methods. Maintains a debt register. You already used this to create the 9 refactor cards. | Every few sessions |
| **architecture-decision** | Generates ADRs with problem statement, alternatives, consequences. Directly enforces your "systemic changes need design docs" rule. | Before any cross-system change |
| **bug-report** | Structured severity/priority/reproduction template. Creates consistent cards. | When logging bugs to Trello |
| **scope-check** | Compares current scope against original plan, flags additions, quantifies bloat. Critical for solo dev where scope creep is the #1 killer. | When a task feels bigger than the card describes |
| **reverse-document** | Generates design docs from existing code. You already use this for undocumented systems in ClockworkCraft. | When inheriting or mapping unfamiliar systems |
| **hotfix** | Emergency workflow with audit trail. Explicit-invocation-only, won't fire accidentally. | Urgent production bugs |
| **gate-check** | Formal PASS/CONCERNS/FAIL verdict for phase transitions. Writes stage to `production/stage.txt`. Good discipline for knowing "are we actually ready to move on." | Before advancing project phases |
| **design-review** | Checks design docs for 8 required sections (overview, player fantasy, rules, formulas, edge cases, dependencies, tuning knobs, acceptance criteria). Good quality bar. | Before handing any design to implementation |

### Fits But Needs Adapting

| Skill | What needs changing | How to adapt |
|---|---|---|
| **sprint-plan** | Reads from `production/sprints/` and `production/milestones/` directories. You use Trello for sprint tracking, not markdown files. | Two options: (1) Keep Trello as task tracker but use sprint-plan to generate a `production/sprints/sprint-N.md` summary that captures velocity and goals — treat it as a planning artifact, not a replacement for Trello. (2) Modify the skill to read Trello board state instead of markdown files. Option 1 is less work and gives you both systems. |
| **retrospective** | Same issue — reads sprint plans from `production/sprints/`. Also checks git log, which is fine. | Generate sprint-plan docs even if Trello is the day-to-day tracker. The retro skill then has something to compare against. |
| **estimate** | Reads design docs from `design/gdd/`. Produces effort estimates with confidence levels. Good process, but you'd need to actually use the `design/gdd/` directory structure. | Start storing design docs in `design/gdd/` for new projects. For ClockworkCraft, your Trello cards serve as design docs — this works but the skill can't read Trello. |
| **map-systems** | Decomposes a game concept into systems, maps dependencies, creates a systems index. Assumes a `design/` directory structure. | Use at project start to create `design/systems-index.md`. This is gold for a new project — it forces you to think about all systems before coding any of them. |
| **design-system** | Guided GDD authoring for a single system. Section-by-section with cross-referencing. Writes to `design/gdd/`. | Use after `map-systems`. The guided process is excellent — it asks the right questions. Just make sure your project uses the `design/gdd/` directory. |
| **project-stage-detect** | Diagnostic — "where are we?" Scans directories for artifacts and estimates project phase. Assumes the Game Studios directory structure (`src/`, `design/`, `production/`). | Remap to Unreal's directory structure. The concept is valuable — having an automated "are we in pre-production or production?" check prevents self-delusion about progress. |
| **asset-audit** | Checks naming conventions (`[category]_[name]_[variant]_[size]`). Assumes generic `assets/` directory. | Rewrite naming rules for Unreal conventions (BP_, T_, M_, SM_, SK_ prefixes). The audit concept is excellent for catching drift in large asset libraries. |
| **perf-profile** | Looks for `_process()`, `Update()`, `Tick()` patterns. Checks frame budgets, memory budgets. | Update function patterns for Unreal (`Tick()`, `TickComponent()`, `BeginPlay()`). Add Unreal-specific checks: Blueprint nativization candidates, GAS ability cost profiling, replication bandwidth. |
| **balance-check** | Analyzes combat DPS, economy faucets/sinks, progression curves, loot tables. Reads from `assets/data/` and `design/balance/`. | The analysis framework is excellent. Adapt data paths to wherever your Unreal project stores balance data (DataTables, DataAssets, or your equivalent of SheetCache). |

### Skip for Now (activate later if needed)

| Skill | Why skip |
|---|---|
| **localize** | You're not localizing during development. Activate when you're preparing for release and need to extract strings. |
| **patch-notes** | Player-facing release communication. Not needed until you have players. |
| **changelog** | Auto-generates from git history. Useful at release time, noise during development. |
| **release-checklist** | Pre-release validation. Not needed until you're approaching a release build. |
| **launch-checklist** | Launch day readiness. Way too early for a new project. |
| **milestone-review** | Comprehensive milestone progress report. Useful once you've defined milestones and completed at least one sprint. Skip on day 1, activate after your first milestone. |
| **playtest-report** | Structured playtest feedback template. Activate once you have something playable and are getting feedback. |
| **prototype** | Creates throwaway code in `prototypes/` with relaxed standards. The concept is good but you might prefer just prototyping directly in Unreal with Blueprints. Activate if you want to do isolated code experiments outside the main project. |

### Your Custom Skills (carry forward)

| Skill | Status |
|---|---|
| **trelloworkflow** | Your most important skill. Rewrite the project-specific references (board ID, sheet ID, SheetCache paths) but keep the 3-phase structure. This is the backbone of your session discipline and nothing in Game Studios replaces it. |
| **brainstorming** (your version) | You've already customized this. The Game Studios version is similar but yours has project-specific context. Merge the best of both — keep Game Studios' hard-gate, add your project-specific patterns. |

---

## Quick Reference: Skills by When You'll Use Them

| Phase | Skills to Activate |
|---|---|
| **Concept** | `brainstorm`, `map-systems`, `project-stage-detect`, `design-review` |
| **Pre-production** | `design-system`, `architecture-decision`, `sprint-plan`, `estimate`, `scope-check`, `prototype` |
| **Production** | `code-review`, `tech-debt`, `bug-report`, `balance-check`, `perf-profile`, `reverse-document` |
| **Always on** | `trelloworkflow`, `gate-check`, `hotfix`, `scope-check` |
| **Polish/Release** | `release-checklist`, `launch-checklist`, `changelog`, `patch-notes`, `retrospective`, `playtest-report`, `localize` |

---

*This document is the answer to "how do we start the next one." Drop it into any new repo alongside CLAUDE.md and CLAUDE_USER_JAI.md. It's the playbook.*
