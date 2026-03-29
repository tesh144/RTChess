---
name: trelloworkflow
description: >
  ClockworkCraft project workflow skill for Trello task management, Google Sheets data sync,
  and post-task code review. Use this skill at session start, before starting any Trello task,
  after completing code changes, or whenever the user mentions syncing data from sheets,
  reviewing Trello cards, checking for stale data, or doing a code review pass.
  Trigger on: "start session", "check trello", "sync sheets", "sync data", "review code",
  "what's on my board", "check for stale cards", "update the cache", "run the pipeline",
  or any reference to the ClockworkCraft Trello board, Google Sheets data, or SheetCache.
---

# ClockworkCraft Project Workflow

This skill defines the three-phase workflow for working on the ClockworkCraft project.
It covers Trello board administration, data synchronization from Google Sheets, and
post-task self-review. Follow it every session.

## Key References

| Resource | Location / ID |
|---|---|
| Google Sheets | `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0` |
| SheetCache | `Assets/Scripts/Editor/SheetCache.json` |
| SheetSyncEditor | `Assets/Scripts/Editor/SheetSyncEditor.cs` |
| Trello Board | Board ID `69bd0b7483af459744b7a24c` |
| AI Sync Log | `JAI_AI_SYNC.md` (repo root) |
| Project Docs | `RTChess.md` (repo root) |
| User Prefs | `CLAUDE_USER_JAI.md` (repo root) |

**Sheet IDs (for batch_update):**
Currencies=0, Environment & Loot=1027353443, Buildings & Production=2122729009,
Placement Costs=1854940026, Workers & Entities=1256997970, Cards & Deck=1675160473,
Map Generation=1697855788, Timers & Animations=1150895612.

**Member IDs (for routing):**
Jai = `59cc086f7d04a5eee43368a5`, Will (DMoT) = `5c4f22f59b9b8042ce53755c`

**Trello List IDs:**
Tasks (Claude) = `69bd0ca577d0a854909d54b3`,
Suggestions = `69bd0c99ae19f16e131883e2`,
To Do (Humans) = `69bd0cb20608fdb1f032a9b8`,
Ready for Review = `69bd0cb4a2c3159d3cd6b111`,
Complete = `69bd0cc0f50f36c76422af1e`

**Label IDs:**
Feature (green) = `69bd0b7583af459744b7a266`,
System (lime) = `69be3cf15271604d16b71e3c`,
Behavior (blue) = `69bd0b7583af459744b7a26b`,
Visual (sky) = `69be3cf0e207dae6cd043392`,
Bug (red) = `69bd0b7583af459744b7a269`,
Balance (orange) = `69bd0b7583af459744b7a268`,
Concept (purple) = `69bd0b7583af459744b7a26a`,
Actionable (black) = `69be3f51c29a207ffd38ca5b`,
Polish (yellow) = `69bd0b7583af459744b7a267`,
Creative (pink) = `69be3cf148182ecea3ba7c57`,
Architecture (lime) = `69be3cf2edf2ed5685d32e18`,
Blocked (red) = `69be3cf4d6bba6395e90c345`,
Discuss (purple) = `69be3cf3a3849f1196b73544`,
Fleshout (dark blue) = `69be43db9d84395cb168e1dd`,
Re-do (dark) = `69c0b76c5d224da136e44727`,
AI Instructions (dark) = `69bf7bae872ddef903539818`

---

## Agent Interaction Rules

These rules apply to ALL phases of this workflow. They override any defaults.

**Always use AskUserQuestion popups for questions.** Never type questions as plain chat
text. The popup UI is faster for the user. Use multiple choice options (2-4 choices) with
clear descriptions. One question per popup.

**Always read context before asking.** Before asking ANY questions about a card or feature:
- Read the card description AND comments via Trello API
- Search the codebase for relevant files/systems
- Your first question must show you've already read what's there.
  Never ask the user to explain what's already written down.

**Never write card descriptions or code without brainstorming first.** Elaborating on
Suggestion cards is creative/design work. Use the brainstorming skill and AskUserQuestion
to discuss intent, scope, and direction with Jai before writing anything to Trello or code.

**Never modify skills you didn't create.** The brainstorming skill, skill-creator, and
other downloaded skills belong to the user. Only modify this trelloworkflow skill and
any skills explicitly created for this project.

---

## Phase 1 — Trello Board Administration

Run this phase at the start of every session and whenever picking up new work.

### 1.1 Read the Board

1. Call `set_active_board` with board ID `69bd0b7483af459744b7a24c`.
2. Fetch cards from each key list using `get_cards_by_list_id`:
   - Tasks (Claude): `69bd0ca577d0a854909d54b3`
   - Suggestions: `69bd0c99ae19f16e131883e2`
   - Ready for Review: `69bd0cb4a2c3159d3cd6b111`
   - To Do (Humans): `69bd0cb20608fdb1f032a9b8`
3. Skip pinned description cards (first card in each list) and separator cards (`cardRole: "separator"`).

**Read card comments before starting any task.** Call `get_card_comments` on every card
you're about to work on. Comments contain the most valuable signal — previous fix attempts,
what failed, what's already been tried, and decisions made. Never start work on a card
without reading its comment history first. This has been a recurring issue where agents
repeat mistakes that were already documented in comments.

**Routing rule:** Check `idMembers` on each card:
- Contains `59cc086f7d04a5eee43368a5` (Jai) → Claude works on it
- Contains `5c4f22f59b9b8042ce53755c` (Will/DMoT) → Skip entirely, Will uses his own agents
- Empty `idMembers` → Ask Jai before starting work (use AskUserQuestion popup)

### 1.2 Tag and Clean Cards

For every card in Suggestions and Tasks (Claude):

**Label hygiene:** Check `idLabels` array. If empty, apply at least one label using
`update_card_details` with the `labels` parameter (array of label IDs). Pick the most
appropriate from the label IDs listed in Key References above. When in doubt, use multiple
labels — a card can be both "Feature" and "Behavior" for example.

**Spelling / grammar:** Check `name` and `desc` fields. If you spot typos, grammatical
errors, or garbled voice-transcription text, fix them via `update_card_details`. Don't
add a comment about this — just fix it silently. Common issues: missing spaces, run-on
words from voice dictation, inconsistent capitalization.

**Expand thin cards:** If a card has a title but empty or vague `desc`:
1. Read the codebase to understand what the card is referring to (search for mentioned
   class names, features, or systems)
2. **BRAINSTORM WITH JAI FIRST** — use the brainstorming skill or AskUserQuestion to
   discuss the card's intent, scope, and design direction before writing anything.
   Never assume what a card means or write your own interpretation. Jai's vision for
   the feature is what matters — your job is to ask the right questions, surface
   relevant codebase context, and help Jai articulate the design. Only after Jai
   confirms the direction should you write the description.
3. Write a clear description incorporating Jai's decisions: what the issue/feature is,
   which files/systems are affected, and what "done" looks like
4. Update via `update_card_details` with the new description
5. Add a comment via `add_comment`: "Expanded by Claude — [brief note of what was added]"

### 1.3 Flag Stale Cards

A card is **stale** if it references systems, files, or behaviors that no longer match
the codebase. For each card in Suggestions and Tasks:

1. Read the card description and comments for mentions of specific files, classes, methods,
   features, or behaviors
2. Search the codebase (Grep/Glob) to verify those references still exist and are current
3. Check git log if you need to know when something changed

**If stale:**
- Add a comment with ⚠️ emoji explaining what changed, with specifics (e.g. "CorruptionDatabase
  was deleted on 2026-03-24 and replaced by UnitDatabase entries of type Corruption")
- State whether the card should be updated, archived, or is now irrelevant
- If the card describes work that's already been implemented, say so with evidence
- Add the **Blocked** label (`69be3cf4d6bba6395e90c345`) if the card can't be acted on

**If NOT stale but already implemented:**
- Flag with a comment noting the feature already exists, with file paths as evidence
- Suggest moving to Complete or archiving

### 1.4 Keep Suggestions Topped Up

Count real cards in Suggestions (excluding pinned description card and separators).
If fewer than 6:

1. Scan recent code changes (`git log --oneline -20`) for areas that could use improvement
2. Check for technical debt: unused fields, TODO comments, inconsistent patterns
3. Look for missing features implied by existing systems (e.g. a database field that
   exists but isn't wired up anywhere)
4. Consider UX improvements based on the game's current state

For each suggestion, create via `add_card_to_list`:
- `listId`: `69bd0c99ae19f16e131883e2` (Suggestions)
- `name`: Clear, specific title (not vague like "improve performance")
- `description`: 2-3 sentences covering what, why, and which systems are involved
- `labels`: At least one label ID from the reference list

### 1.5 Move Approved Cards to Complete

Check Ready for Review and To Do (Humans) for cards where `dueComplete: true` (Jai
has checked them off as approved). Move these to Complete list (`69bd0cba8db7c1c6e0ff70c3`)
using `move_card`. Strip completion notes from the description — Complete is a clean log.

---

## Phase 2 — Pre-Task Data Sync

Run this phase before starting any task that touches game data (databases, balancing,
entity properties, map generation). The pipeline flows one direction:

```
Google Sheets (source of truth)
    → SheetCache.json (intermediate, written by Claude via MCP)
        → .asset databases (runtime, updated by SheetSyncEditor in Unity)
```

### 2.1 Check Cache Freshness

Read `Assets/Scripts/Editor/SheetCache.json` and check the `lastSynced` timestamp at
the top of the file.

**Staleness rule:** The cache is stale if `lastSynced` is more than **2 hours** old.
This threshold balances rapid iteration (Jai sometimes edits sheets in quick succession)
against unnecessary API calls.

**Manual refresh triggers:** Jai can ask for an immediate sync at any time with phrases
like "sync sheets", "refresh cache", "pull latest data", "update from sheets". Always
honour these regardless of cache age.

If the cache is fresh (< 2 hours) and no one has mentioned editing sheets, skip to 2.3.

### 2.2 Sync from Google Sheets

When the cache is stale or a manual refresh is requested:

1. **Determine which sheets to fetch.** Match the task to relevant sheets:
   - Working on units/workers/enemies → "Workers & Entities" (ID `1256997970`)
   - Working on buildings/production → "Buildings & Production" (ID `2122729009`)
   - Working on environment/loot → "Environment & Loot" (ID `1027353443`)
   - Working on map generation → "Map Generation" (ID `1697855788`)
   - Working on costs → "Placement Costs" (ID `1854940026`)
   - Working on cards/deck → "Cards & Deck" (ID `1675160473`)
   - Working on currencies → "Currencies" (ID `0`)
   - When in doubt, fetch all sheets the task might touch

2. **Fetch via Google Sheets MCP:** Use `get_sheet_data` with spreadsheet ID
   `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0` and the relevant sheet name.
   Use `include_grid_data: true` if you need to verify formatting or validations.

3. **Update SheetCache.json:** Write the fetched data into the cache file. Preserve the
   existing structure: each sheet has a `headers` array and a `rows` array of arrays.
   Update the top-level `lastSynced` to the current ISO 8601 timestamp.

4. **Log the sync** in `JAI_AI_SYNC.md` under Active Work: which sheets were refreshed
   and the new timestamp.

### 2.3 Verify Database Consistency

After confirming the cache is current, spot-check that .asset database files match the
cached sheet data for the columns you're about to work with.

**Database file locations:**
- `Assets/Scripts/Data/BuildingDatabase.asset`
- `Assets/Scripts/Data/EnvironmentDatabase.asset`
- `Assets/Scripts/Data/UnitDatabase.asset`
- `Assets/Scripts/Data/WorkerDatabase.asset`
- `Assets/Scripts/Data/CurrencyDatabase.asset`

**What to check:** Pick 2-3 entries relevant to your task and compare key fields:
assetName, hp, attackPower, behaviorType, isEnemy, isMapGenerated, lootResourceType,
killerAdvances, drawWeight, productionInterval, productionOutputType.

**If inconsistencies are found:**
- Flag them to Jai with specifics: entry name, field name, sheet value vs. database value
- Do NOT silently fix them. The sheet is source of truth, but sometimes sheets have errors
  that Jai needs to fix manually first
- Use AskUserQuestion popup to ask whether to update the database or fix the sheet

**Past incidents to remember:**
- Fighter was incorrectly deleted from WorkerDatabase because an agent assumed it was wrong
  without checking the sheet (2026-03-22)
- CorruptedHeart was incorrectly set to isMapGenerated=true when ALL corruption entities
  should only spawn through the corruption system (2026-03-24)
- TrainingFacility was left in BuildingDatabase after being removed from sheets (2026-03-22)

### 2.4 Column-to-Code Reference

When reading sheet data, use these mappings. RTChess.md has the full table, but the
key gotchas that trip agents up are:

**Emoji-prefixed values** (e.g. "💰 Gold", "👷 Worker"): The SheetSyncEditor has a
`StripEmoji()` helper. When reading manually, strip everything before the first ASCII
letter before parsing to an enum.

**Workers & Entities columns:**
| Sheet Column | Code Field | Gotcha |
|---|---|---|
| Entity | assetName | Strip parenthetical e.g. "Worker (Generic)" → "Worker" |
| Type | WorkerType / GameUnitType | Emoji-prefixed dropdown |
| HP | hp | int |
| Attack Power | attackPower | int |
| Movement Behavior | behaviorType | Maps to BehaviorType enum |
| Attack Behavior | → isEnemy | Hostile = true, Peaceful = false. NOT a code enum |
| Enemy | isEnemy | Explicit column, takes precedence over Attack Behavior |
| MapGenerated | isMapGenerated | Checkbox boolean |
| Killer's Behavior | killerAdvances | Advance = true, Stay = false |
| Draw Weight | drawWeight | float |
| Slot Takeable | isSlotTakeable | Checkbox boolean |

**Buildings & Production columns:**
| Sheet Column | Code Field | Gotcha |
|---|---|---|
| Building | assetName | |
| Prod. Interval (s) | productionInterval | float |
| Input | productionInputType | Emoji-prefixed dropdown |
| Output | productionOutputType | Emoji-prefixed dropdown |
| Output Amt | productionAmount | int |
| Cost Resource | productionCostResourceType | Emoji-prefixed |
| Cost Amount | productionCostAmount | int |

**Environment & Loot columns:**
| Sheet Column | Code Field | Gotcha |
|---|---|---|
| Object | assetName | |
| Drops | lootResourceType | Emoji-prefixed dropdown, StripEmoji → Enum.TryParse |
| Loot per Hit | lootYield | int |
| HP | hp | int |
| MapGenerated | isMapGenerated | Checkbox boolean |

---

## Phase 3 — Post-Task Review

Run this phase after completing any code changes, before moving the Trello card to
Ready for Review. This phase only applies to Claude Code sessions — Cowork skips 3.1
(self-review) since it can't run builds/tests, but still does 3.2 and 3.3.

### 3.1 Self-Review (Two Passes)

Do two distinct review passes over your changes. This catches issues that are easy to
miss when you're deep in implementation.

**Pass 1 — Correctness:**
1. Run `git diff` to get the full change set — read every hunk
2. For each modified file, check:
   - Logic correctness: no off-by-one errors, null checks on every GetComponent/Find call
   - Enum values match between .asset files and code
   - New fields are initialized in ALL code paths that create the object (constructors,
     factory methods, sync methods, scene spawn methods)
   - No accidental removal of existing functionality (compare before/after carefully)
3. For .asset file changes, verify values match the Google Sheet (Phase 2.3 applies)
4. Check that serialized field changes won't break existing scene references

**Pass 2 — Quality:**
1. **Comments preserved:** Jai's hard rule — never remove existing code comments. If you
   see a comment disappeared in the diff, restore it. Adding comments is fine.
2. **Naming conventions:** Match the rest of the codebase. camelCase for private fields,
   PascalCase for public properties, etc.
3. **Debug.Log format:** All log statements prefixed with `[ClassName]` per project convention.
   Example: `Debug.Log($"[CorruptionHeart] Spawned spike at ({x},{y}).");`
4. **No dead code:** Remove any TODO comments, commented-out experiments, or placeholder
   values you used during development
5. **Serialized field attributes:** New `[SerializeField]` fields need `[Tooltip("...")]`
   and appropriate `[Header("...")]` grouping
6. **DOTween preference:** If the project has DOTween (it does), use it for animations
   instead of manual Update() loops with timers

### 3.2 Update Trello Card

After review passes are complete:

1. **Add completion comment** via `add_comment`:
   ```
   ✅ Implementation complete (YYYY-MM-DD):
   - What was done (1-3 concise bullet points)
   - Files modified (list them)
   - Any caveats or things Jai should verify in-editor
   ```

2. **Uncheck the card** via `update_card_details` with `dueComplete: false` — this
   signals "work is done, ready for human review"

3. **Move to Ready for Review** via `move_card` to list `69bd0cb4a2c3159d3cd6b111`

4. **If human steps are needed** (prefab assignment, Inspector wiring, visual verification):
   - Check if the card already has a "To Do (Jai)" checklist. If not, create one via
     `create_checklist` with name "To Do (Jai)" — it must be the FIRST checklist on the card
   - Add specific, actionable items via `add_checklist_item`
   - Tell Jai in the conversation that there are human steps

### 3.3 Update AI Sync Log

Update `JAI_AI_SYNC.md`:

1. **Move task from Active Work to Completed Work** with format:
   `| YYYY-MM-DD | Co-Work | One-line summary of what was done. → Ready for Review |`

2. **Note related cards or follow-up work** discovered during implementation in the
   Notes / Flags section

3. **Update Active Work** if you're starting a new task immediately after

---

## When Picking Up a Task — Full Checklist

This is the complete sequence when starting work on a Trello card. The card stays
☐ UNCHECKED through steps 1-5 (planning). CHECK happens at step 6 (execution).

1. **Read the card** — title, description, all comments (via `get_card_comments`)
2. **Tag the card** — apply labels if missing (Phase 1.2)
3. **Fix quality issues** — spelling, grammar in title and description
4. **Add 🔍 comment** — signals to other agents that work is underway
5. **Plan first** — ask clarifying questions via AskUserQuestion if needed, document
   approach in a card comment, update JAI_AI_SYNC.md Active Work
6. **CHECK the card** — `update_card_details` with `dueComplete: true` when you start
   actual code execution
7. **Run Phase 2** if the task touches game data
8. **Implement** — update card comments with progress notes as you work
9. **Run Phase 3** — self-review, update card, update sync log
10. **UNCHECK and move** — `dueComplete: false`, then `move_card` to Ready for Review

---

## Quick Reference: When to Run Each Phase

| Trigger | Phase |
|---|---|
| Session start | Phase 1 (full board scan) |
| Picking up a new Trello card | Full checklist above → Phase 2 (if data task) |
| "Sync sheets" / "refresh cache" | Phase 2.2 |
| Before starting data-related code | Phase 2 (full) |
| After finishing any code changes | Phase 3 (full) |
| "Check for stale cards" | Phase 1.3 |
| "Top up suggestions" | Phase 1.4 |
| "Review my changes" / "do a code review" | Phase 3.1 only |
