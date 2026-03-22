# Jai's AI Agent Sync Log

> **Owner:** Jai
> **Purpose:** Keeps all AI agents (Claude Code, Claude Co-Work, etc.) aligned on what's happening across sessions. Every agent must read this at session start and update it when making changes.

---

## Agent Rules

1. **Read this file at the start of every session** before doing any work.
2. **Update "Active Work" when you start a task** — note the agent name, branch, and what you're doing.
3. **Update "Completed Work" when you finish a task** — move it from Active with a one-line summary of what was done.
4. **Check for conflicts** — before starting work on a file or system, check if another agent is already working on it. If so, flag it to Jai.
5. **Keep it concise** — one line per item. This is a coordination log, not a diary.
6. **Never delete another agent's entries** — only Jai clears this file.

---

## Active Work

| Agent | Task | Status |
|-------|------|--------|

---

## Completed Work

| Date | Agent | Summary |
|------|-------|---------|
| 2026-03-20 | Co-Work | Set up CLAUDE_USER_JAI.md and JAI_AI_SYNC.md for ClockworkCraft project |
| 2026-03-20 | Co-Work | Created gameplay-replay-analyzer skill (video frame extraction + sequential analysis) |
| 2026-03-20 | Co-Work | Set up Kings Renewal repo clone, CLAUDE.md, project docs |
| 2026-03-21 | Co-Work | Task 1 (Torch): Data-driven fog reveal radius — fogRevealRadius on BuildingData, Torch=2, wired through UnitStats→FurnitureObject |
| 2026-03-21 | Co-Work | Task 2 (Slots): isSlotTakeable on GridEntityHealth — workers don't advance into mobile unit cells on kill |
| 2026-03-21 | Co-Work | Task 3 (Training): Input-triggered production — ProductionInputType enum, waitingForInput state, DragDropHandler feed-building path with green highlight |
| 2026-03-21 | Co-Work | Task 4 (Kitchen/Meal): MealBuffSource marker, meal buff on GridEntityActor (8 ticks), smart skip, Kitchen produces Meal cards, Meal HP=3 |
| 2026-03-21 | Co-Work | BuildingProductionManager: Fighter + Meal output cases, CollectMealReward, FindMealCard, waitingForInput reset on collect |
| 2026-03-21 | Co-Work | RaritySystem: added FindByName() for named card lookup |
| 2026-03-21 | Co-Work | Trello: 4 task cards → Ready for Review, 4 human To Do cards created, Suggestions card "Beats in a Bar" created |
| 2026-03-21 | Co-Work | Google Sheets: Fixed Placement Costs Torch/Statue section merges and column widths |
| 2026-03-21 | Co-Work | Sheet Sync Tool: Created SheetSyncEditor.cs + SheetCache.json for Google Sheets → ScriptableObject sync |
| 2026-03-21 | Co-Work | Sheet Sync Tool: Added UnitDatabase sync, StripEmoji helper, fixed column name mismatches |
| 2026-03-21 | Co-Work | Google Sheets audit: Stripped all borders, applied dark headers, frozen rows, alternating stripes on all sheets |
| 2026-03-21 | Co-Work | Google Sheets: Added data validation dropdowns (enum columns), checkboxes (booleans), emoji-prefixed values |
| 2026-03-21 | Co-Work | Google Sheets: Added Icon/Is Enemy→Attack Behavior/Slot Takeable/Type dropdown to Workers & Entities |
| 2026-03-21 | Co-Work | CLOCKWORK.md: Added comprehensive Google Sheets formatting rules, sheet IDs, column-to-code mappings |
| 2026-03-21 | Co-Work | Data verification: Compared all 3 synced sheets against 4 .asset databases, fixed mismatches |
| 2026-03-21 | Co-Work | Workers & Entities: Re-added Entity column (was accidentally deleted), fixed Dinosaur/Mammoth Attack Behavior → Hostile |
| 2026-03-21 | Co-Work | UnitDatabase.asset: Added isSlotTakeable=1 for Dinosaur and Mammoth |
| 2026-03-21 | Co-Work | SheetCache.json: Updated to reflect all final sheet values including Entity column and Attack Behavior fixes |
| 2026-03-21 | Co-Work | Google Sheets: Fixed misplaced validations (Type dropdown leaked to Entity column, BOOLEAN on Start Amt column) |
| 2026-03-21 | Co-Work | Google Sheets: Fixed None grey text — TEXT_EQ silently fails, switched to CUSTOM_FORMULA. Rule index matters (cosmetic rules go last) |
| 2026-03-21 | Co-Work | Google Sheets: Removed broken conditional format on Currencies D column (green-bold NUMBER_GREATER rule) |
| 2026-03-21 | Co-Work | Documentation: Updated CLOCKWORK.md gotchas (5→10 rules), Trello Google Sheets card, CLAUDE_USER_JAI.md with hard-won Sheets API lessons |
| 2026-03-21 | Co-Work | Full sheet→asset sync: Fixed Torch fogRevealRadius (1→2), TrainingFacility productionInputType (None→Worker), Feast isMealSource (0→1). All 7 sheets verified against code. |
| 2026-03-21 | Co-Work | Killer's Behavior: Added killerAdvances bool to BuildingData, EnvironmentData, WorkerData, UnitData, UnitStats. Wired through AttachComponents→GridEntityHealth. All .asset files updated (Buildings: Advance except Feast=Stay; Environment: all Advance; Workers/Units: all Stay). SheetSyncEditor updated. |
| 2026-03-21 | Co-Work | Trello: Created 8 Suggestion cards from voice transcript — Meal Buff Visual, Feast Degradation, Buff Duration, Feast Exclusivity, Wild Animal Feast, InteractionRegistry, Random Building Pool Verify, Water Tile Visuals |
| 2026-03-21 | Co-Work | Trello: Created 6 Suggestion cards from temple/tiers transcript — Building Tiers System, Button Level-Up (every 5 draws), Temple Tier-Gate Structures, Statue Pool Restriction, Button Price Lock, [V2] Roguelike Choice on Level-Up |
| 2026-03-21 | Co-Work | #45 Feast Exclusivity: Added isRandomBuilding to BuildingData/UnitStats, set Feast=false in .asset, filtered in RaritySystem.DrawRandomUnit(), synced in SheetSyncEditor |
| 2026-03-21 | Co-Work | #47 InteractionRegistry: Added per-faction flags (allyInteractible, enemyInteractible, wildAnimalInteractible), InteractorType enum, CanInteract() method, PopulateFromBuildingDatabase(), BuildingDatabase reference |
| 2026-03-21 | Co-Work | #46 Wild Animals + Feast: Modified GridEntityActor.TryMoveForward() — wild animals now check InteractionRegistry.CanInteract(WildAnimal) on occupied cells and attack interactible targets (e.g. Feast) instead of bumping |
| 2026-03-21 | Co-Work | Environment interaction columns: Added allyInteractible/enemyInteractible/wildAnimalInteractible to EnvironmentData.cs, .asset (all: ally=true, enemy=false, wild=false), SheetSyncEditor, InteractionRegistry.PopulateFromDatabases() |
| 2026-03-22 | Co-Work | Bug fix: DockBarManager.cs FloatAndFadePopup — added null guards for destroyed RectTransform/TextMeshPro/GameObject mid-animation (9 MissingReferenceExceptions) |
| 2026-03-22 | Co-Work | Safety fix: InteractionRegistry.CanInteract() — unknown entries now default deny for WildAnimal (was default allow for all). Prevents wild animals attacking unregistered objects |
| 2026-03-22 | Co-Work | Removed Grave system: deleted EnvironmentDatabase.asset entry, GridEntityManager corpse-spawning code (SpawnCorpseDelayed + fields), SheetCache.json entry, Google Sheet row. Archived Trello card #62. Grave.prefab left in Prefabs/PEPO/ (manual delete in Unity) |
| 2026-03-22 | Co-Work | #59 v1: Added slot reservation system. v2 FIX: BuildingProductionManager now uses dock.IsHandFull (includes reservedSlots) instead of GetCardCount()>=MAX. SpawnWorkerFly/SpawnCardFly return bool — CollectReward only consumes entry if reservation succeeded. No more lost cards |
| 2026-03-22 | Co-Work | #58 v1: Added GetNextSlotWorldPosition() with manual math. v2 FIX: Replaced manual math with layout-aware placeholder approach — adds temporary RectTransforms to container, forces LayoutRebuilder, reads real position. Now matches HorizontalLayoutGroup alignment exactly |
| 2026-03-22 | Co-Work | #60 Building HP damage text: GridEntityHPBar auto-enables showDamagePopup for allied non-environment entities. Shows remaining HP (not damage dealt) in starvation-countdown red style. Excludes goldmines/trees |
| 2026-03-22 | Co-Work | #61 v1: Tuned debounce/burst params. v2 FIX: COIN_BURST_RESET_TIME 0.5→1.5s (particles arrive over ~1s, was resetting mid-batch). Debounce 30→15ms. Pitch ramp 0.08→0.12/step starting at 0.8. Volume swell 1.0-1.3→0.85-1.6x. Burst cap 10→15 |
| 2026-03-22 | Co-Work | #36 Card #36 (New Object Pipeline: Kitchen + Meal): Meal popup icon fix (SpawnPopup missing ProductionOutputType.Meal case), MealBuffSource radius behavior (3-cell aura, periodic scan, grants buff to nearby workers), MealDegradation component (1 HP every 5s). → Ready for Review |
| 2026-03-22 | Co-Work | #27 Worker Modification / Training System: Added TrainingFacility to BuildingDatabase.asset (productionInputType: Worker, productionInterval: 30s, productionOutputType: Fighter). Added TrainingFacility to Google Sheets Buildings & Production. Verified DragDropHandler feed-building and BuildingProductionManager input-triggered production fully support the feature. Created To Do (Jai) checklist for prefab/icon creation and placement cost config. → Ready for Review |
| 2026-03-22 | Co-Work | #61 Multi-pickup SFX: Verified feature fully implemented in GameSFXManager.cs with v2 params (pitch 0.8+0.12/step, volume 0.85-1.6x, debounce 15ms, burst reset 1.5s, cap 15). Integrated in ResourceLootFX.cs line 245 on loot particle arrival. Documented implementation status and troubleshooting steps. → Ready for Review |
| 2026-03-22 | Co-Work | Synced Feast data from Google Sheets to BuildingDatabase.asset (hp: 5→10, drawWeight: 0→0.5, isRandomBuilding: 0→1, productionInterval: 0→30, productionIntervalBonus: 0→15, productionAmount: 0→1). Fixed Barracks/Kitchen drawWeight: 1→0.5 in asset. |
| 2026-03-22 | Co-Work | #88 Bug Fix: Removed Fighter from WorkerDatabase — was causing PickRandomWorker() to randomly select Fighter. ConeTent now correctly produces Workers. Renamed ConeTent→Home in BuildingDatabase.asset, Google Sheets (Buildings & Production), and code comments in BuildingData.cs. |
| 2026-03-22 | Co-Work | #89 Resolved: TrainingFacility removed from BuildingDatabase.asset and Google Sheets. Feast entity confirmed present and correctly configured. Barracks is the training facility (consumes Workers, produces Fighters). |
| 2026-03-22 | Claude Code | #90 RandomTier0-3 System: Tier field on BuildingData/UnitStats, RandomTier0-3 enums added, RaritySystem.DrawRandomUnitByTier() implemented, gacha defaults to RandomTier0, all .asset files synced, SheetSyncEditor syncs tier field. → Ready for Review |

---

## Recent Decisions

_Decisions made during sessions that the other agent should know about._

- 2026-03-20: PlacementCostDisplay redesigned from orbiting to vertical column layout (v6).
- 2026-03-20: ValidatePlacement split into IsCellValid (cell only) and ValidatePlacement (cell + afford). Cost display uses cell validity only.
- 2026-03-20: Workers only advance into killed target's cell if target is environment (no GridEntityActor). Moving units don't trigger advance.
- 2026-03-20: Trello board "Auto RTS" is the task board for ClockworkCraft. Google Sheets balancing doc linked in CLAUDE_USER_JAI.md.
- 2026-03-21: Slot-takeable now uses explicit `isSlotTakeable` field (auto-derived from `!isActive`) instead of GridEntityActor heuristic.
- 2026-03-21: Input-triggered production: buildings with `productionInputType != None` wait for a matching card to be dropped on them before starting timer.
- 2026-03-21: Meal buff is flag-only (no mechanical effect yet). Suggested "Beats in a Bar" global timer rework for future iteration.
- 2026-03-21: Meals are NOT allied — workers interact with them like enemies. HP=3 means ~1-3 worker hits to consume.
- 2026-03-21: Per-faction interaction model: InteractionRegistry now has 3 bool columns (ally/enemy/wildAnimal) per entry, matching Google Sheet. Legacy `unlocked` field preserved as fallback.
- 2026-03-21: Wild animals use PerformStrongInteraction (same as workers) when attacking interactible targets. Feast has killerAdvances=false so animals won't advance into its cell.

---

## Notes / Flags

_Anything one agent needs to flag for the other._

- 2026-03-20: Five items in Notion "Ready for Review" awaiting Jai's review (PlacementCostDisplay v6, resource scarcity CSV, cost curve CSV, gameplay analytics, timer bubble delay).
- 2026-03-20: Map Density Slider plan approved but not yet implemented.
- 2026-03-21: TrainingFacility, Kitchen, Meal all need prefabs + icons created in Unity (human To Do cards on Trello).
- 2026-03-21: New files created: `MealBuffSource.cs`. Modified: `DragDropHandler.cs`, `GridEntityActor.cs`, `GridEntityHealth.cs`, `BuildingProductionManager.cs`, `BuildingData.cs`, `UnitStats.cs`, `MapGeneratorV2.cs`, `RaritySystem.cs`, `BuildingDatabase.asset`.
- 2026-03-22: Cards #27 and #61 moved to Ready for Review. Both await Jai's review and testing.
  - #27 (Training System): Prefab creation + placement cost config needed
  - #61 (Multi-pickup SFX): Verify coinCollect AudioClip is assigned in GameSFXManager Inspector, test in-game with rapid loot drops
- 2026-03-22: Map Density Slider plan still pending (from 2026-03-20). Plan exists in `/sessions/vibrant-practical-thompson/mnt/.claude/plans/async-knitting-codd.md`
- 2026-03-22: Trello Processing Complete (Read → Tag → Fix Spelling → Clarify)
  - Card #87: Tagged "Balance". Name fixed. Investigation confirmed system is correct. ✅ Moved to Ready for Review (awaiting Jai's playtesting)
  - Card #88: Tagged "Bug". Name fixed (ConeTent→Home rename clarified). ✅ Moved to Ready for Review (Fighter removed from WorkerDatabase)
  - Card #89: Tagged "Behavior". Name fixed (TrainingFacility removed, Feast restored). ✅ Moved to Ready for Review
  - Card #90: Already tagged (System, Feature, Actionable). Tier field confirmed in Google Sheets. Plan document posted (9 implementation steps). Ready for code execution pending Jai's signal
- 2026-03-22: DOCUMENTATION UPDATES — Best practices consolidated and clarified for all agents:
  - **Card #85** (Trello Workflow Reference): Added explicit checkmark timing, AskUserQuestion popup requirement for clarifications (NOT card comments), verification step reminder
  - **Card #70** (AskUserQuestion Usage): Updated with when/why/how to use popup tool for clarifications
  - **CLAUDE_USER_JAI.md**: Added checkmark timing table, updated "When Receiving a Task" steps to show CHECK on execution/UNCHECK before Ready for Review, added rules about AskUserQuestion and asking when unsure
  - **KEY RULE**: If unsure about something, use AskUserQuestion popup to ask — don't make assumptions, don't leave questions in card comments
