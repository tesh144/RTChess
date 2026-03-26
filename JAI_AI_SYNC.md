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

## Cross-Agent Requests

| From | To | Request | Status |
|------|----|---------|--------|
| Claude Code | Co-Work | **Google Sheets MCP for Claude Code**: No standalone Google Sheets MCP available in registry. Cowork uses Anthropic's managed connector (can't be shared). Claude Code needs a Google Cloud service account (JSON key) to use an open-source MCP. **Workaround**: Claude Code reads/writes SheetCache.json. Cowork pushes changes to actual sheets. | Answered |

---

## Active Work

| Agent | Task | Status |
|-------|------|--------|
| Co-Work | #130 POI Bubble System | In Progress — Design doc written, POI sheet + Ref tab created, data pipeline pending |
| Co-Work | #117 New Buildings: Scrapper, Garden, Rabbit Farm | In Progress — DB entries synced; scene spawn cleanup done; production logic pending |
| Co-Work | #22 Corruption System | In Progress — data arch + thorns + spike spawning done; scene entries cleaned up |
| Co-Work | Enemy interaction fix | In Progress — ScanAndInteract faction-aware, loot gated for enemies, Enemy tag synced |

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
| 2026-03-22 | Co-Work | #88 Bug Fix: Renamed ConeTent→Home in BuildingDatabase.asset, Google Sheets (Buildings & Production), and code comments in BuildingData.cs. NOTE: Fighter was incorrectly removed from WorkerDatabase in this session — has been restored with original values from git history (type=8, icon guid recovered). |
| 2026-03-22 | Co-Work | #89 Resolved: TrainingFacility removed from BuildingDatabase.asset and Google Sheets. Feast entity confirmed present and correctly configured. Barracks is the training facility (consumes Workers, produces Fighters). |
| 2026-03-22 | Claude Code | #90 RandomTier0-3 System: Tier field on BuildingData/UnitStats, RandomTier0-3 enums added, RaritySystem.DrawRandomUnitByTier() implemented, gacha defaults to RandomTier0, all .asset files synced, SheetSyncEditor syncs tier field. → Ready for Review |
| 2026-03-22 | Co-Work | Home Worker production fix: BuildingProductionManager.cs — ProductionOutputType.Worker now calls workerDatabase.GetByName("Worker") (direct lookup) instead of PickRandomWorker() (random selection). Sheet confirms Home output = "Worker" specifically. |
| 2026-03-22 | Co-Work | Gold startingAmount fix: CurrencyDatabase.asset — Gold startingAmount corrected 20→0 to match Google Sheets. All other currencies confirmed 0. |
| 2026-03-22 | Co-Work | TrainingFacility removed from BuildingDatabase.asset. Was incorrectly left in from prior session. Google Sheets is authoritative — TrainingFacility is not in Buildings & Production sheet. |
| 2026-03-22 | Co-Work | Documentation update: CLAUDE_USER_JAI.md — added checkmark timing table, 10-step task workflow, data-must-match-sheets rule, AskUserQuestion popup mandate, check-codebase-before-asking rule. |
| 2026-03-22 | Co-Work | Documentation update: Trello Card #85 + #70 — updated with AI best practices from this session (data consistency, popup for clarifications, codebase-first verification). |
| 2026-03-23 | Co-Work | #97 Draw button cost feedback: Timer bubble now shows cost (e.g. "5 Gold") after cooldown ends, red text if can't afford, updates live on resource changes. DrawButtonController.cs |
| 2026-03-23 | Co-Work | #101 Map scale: Grid 80x80→120x120. Camera zoom absoluteMax 60→80, distancePerTile 0.04→0.06. Pan distance now dynamic (basePanDistance + revealedTiles * 0.05). Both zoom and pan scale with FogManager revealed count. GridCamera.cs, MapGeneratorV2.cs |
| 2026-03-23 | Co-Work | Trello cleanup: Merged AI Guideline cards (10→4), merged SFX pitch bug into #61, merged meal buff cards (5→2). Moved AI Guidelines to Important Documents with AI Instructions label. |
| 2026-03-24 | Co-Work | #22 Corruption System: Data architecture refactor complete. GameUnitType.Corruption added. CorruptionHeart uses serialized fields. CorruptionDatabase/CorruptionData/CreateCorruptionDatabase deleted. MapGeneratorV2 + Editor updated to use UnitDatabase. |
| 2026-03-24 | Co-Work | Trello: Maintained all 5 Tasks (Claude) cards on Auto RTS board — labels, descriptions, progress comments, dueComplete flags. |
| 2026-03-24 | Co-Work | Google Sheets: Alternating row zebra stripes fixed across all 9 Placement Costs sections. Scrapper/Rabbit Farm/Garden emoji prefixes added to Buildings & Production. |
| 2026-03-24 | Co-Work | #115 Fix killer behavior: cachedSlotTakeable fix already in place from prior session. → Ready for Review |
| 2026-03-24 | Co-Work | #118 Zoom out with progress: GridCamera subscribes to FogManager.OnCellRevealed → RecalculateZoomLevels(). → Ready for Review |
| 2026-03-24 | Co-Work | #120 Active flag: SyncSpawnEntries() and SyncUnitSpawnEntries() now filter inactive entries. → Ready for Review |
| 2026-03-24 | Co-Work | #22 Corruption: Thorns (1 dmg retaliation) + spike spawning added to CorruptionHeart. OnDamagedBy event + TakeDamageFrom() added to GridEntityHealth; GridEntityActor uses TakeDamageFrom. unitDatabase injected into heart by MapGeneratorV2. → Ready for Review |
| 2026-03-24 | Co-Work | New enums: ProductionInputType.Any, ProductionOutputType.Scrap/PetRabbit/TreeSeed, ResourceType.Scrap added. SheetSyncEditor column names fixed (Input Card, Resource Use, Resource Amount) and new aliases added. |
| 2026-03-24 | Co-Work | UnitDatabase.asset: CorruptedHeart, Spike1, Spike2, PetRabbit, TreeSeed entries added (type=Corruption or Generic; prefabs need assigning in Inspector). |
| 2026-03-24 | Co-Work | BuildingDatabase.asset: Scrapper (tier 3, Any input, Scrap output), Rabbit Farm (tier 4, Petal cost, PetRabbit output), Garden (tier 5, Water cost, TreeSeed output) entries added. |
| 2026-03-24 | Co-Work | SyncUnits() updated to sync Corruption-type entries (not just Hostile); also syncs GameUnitType from Type column. |
| 2026-03-24 | Co-Work | Scrap/PetRabbit/TreeSeed moved from BuildingDatabase to EnvironmentDatabase (user correction: these are environment entities, not buildings). |
| 2026-03-24 | Co-Work | EnvironmentData.cs: Added isMapGenerated (bool) and dropOnDeath (ResourceType) fields. UnitData.cs: Added dropOnDeath field. |
| 2026-03-24 | Co-Work | SheetSyncEditor: SyncEnvironment() now syncs MapGenerated + Drop on Death columns. SyncUnits() now syncs Drop on Death + Drops + Loot per Hit. |
| 2026-03-24 | Co-Work | EnvironmentDatabase.asset: Added Scrap (loot=Scrap/32, HP=5, isMapGenerated=false), PetRabbit (loot=Meat/23, HP=3, isMapGenerated=false), TreeSeed (loot=Wood/2, HP=5, isMapGenerated=false). Existing entries updated with isMapGenerated=true + dropOnDeath=0. |
| 2026-03-24 | Co-Work | BuildingDatabase.asset synced with latest Google Sheets: Statue interval 10→15, Barracks interval 10→20 bonus 20→30, Kitchen cost Food/10→Meat/3 +increment 1, all wildAnimalInteractible corrected. RabbitFarm/Garden +productionCostIncrement=1. |
| 2026-03-24 | Co-Work | SheetCache.json fully rewritten with all 3 sheets' latest data including new columns (MapGenerated, Drop on Death, Resource Increment, Input Card, etc.). |
| 2026-03-24 | Co-Work | Pet Rabbit → Lizard rename: BuildingData.cs enum (ProductionOutputType.Lizard), SheetSyncEditor aliases, EnvironmentDatabase.asset (assetName: Lizard), SheetCache.json, Google Sheets (Buildings & Production Output column). |
| 2026-03-24 | Co-Work | Custom PropertyDrawers: DatabaseEntryDrawers.cs — NamedEntryDrawer base class + 6 drawers (Building/Environment/Unit/Worker/Furniture/Currency). Inspector now shows asset names instead of "Element N". |
| 2026-03-24 | Co-Work | SyncCorruptionSpawnEntries() rewritten: removes invalid/empty/inactive entries, updates prefabs on existing, adds missing from UnitDatabase. |
| 2026-03-24 | Co-Work | SyncSpawnEntries() isMapGenerated filter: non-map-generated environment objects (Scrap, Lizard, TreeSeed) excluded from map generator spawn list. |
| 2026-03-24 | Co-Work | Scene file cleanup: Corruption entries shifted from indices 1-3 to 0-2 (removed unnamed empty entry at index 0). spawnEntries reduced 8→6 (removed Scrap/Lizard at indices 6-7, non-map-generated). |
| 2026-03-24 | Co-Work | Google Sheets Output dropdown: Updated Buildings & Production column M validation to include all buildings (with emoji), all environment types (with emoji), workers, and tier draws. |
| 2026-03-24 | Co-Work | Enemy interaction fix: ScanAndInteract() now faction-aware — non-allied actors (corruption spikes) check InteractionRegistry.CanInteract(Enemy) instead of WorkerCanInteract. Prevents spikes from attacking goldmines/trees/rocks. |
| 2026-03-24 | Co-Work | Enemy loot gate: PerformStrongInteraction() only grants loot/resources when attacker is allied. Enemies deal damage but don't feed player economy. |
| 2026-03-24 | Co-Work | Enemy tag integration: SheetCache "Enemy" column added to Workers & Entities. SheetSyncEditor reads explicit "Enemy" column (falls back to Attack Behavior). CorruptedHeart isEnemy fixed 0→1 in UnitDatabase.asset. |
| 2026-03-24 | Co-Work | Same-faction skip: ScanAndInteract, ScanAndInteractWildAnimal, TryMoveForward all now skip same-faction targets. Enemies don't attack enemies, allies don't attack allies. Prevents spike-on-heart friendly fire. |
| 2026-03-24 | Co-Work | Starvation guard: IncrementIdleCounter() now requires IsAllied in addition to RotateAndInteract. Enemy spikes sharing the same behavior type no longer starve to death from idling. |
| 2026-03-24 | Co-Work | isMapGenerated for units: Added field to UnitData.cs. Set in UnitDatabase.asset (Dinosaur/Mammoth/Heart=true, Spike1/Spike2=false). SyncUnitSpawnEntries() now filters by isMapGenerated. SheetSyncEditor syncs MapGenerated column for units. Scene file unitSpawnEntries cleaned: removed Spike1/Spike2 (size 5→3). |
| 2026-03-26 | Co-Work | POI bubble scale fix: Root cause was WorldCanvas_Popups (2560×1440) at localScale 1 = each pixel was 1 world unit (bubbles invisible/enormous). Added targetScale to POIBubble (animations scale relative to it), bubbleWorldScale field to POIManager (default 0.005). Added diagnostic logging + Diagnose button. |
| 2026-03-26 | Co-Work | Building bubble integration: BuildingProductionManager now supports designed bubbles via buildingBubblePrefab field. Bubble_Collect replaces procedural popup (shows reward icon). Bubble_Insert shows when building awaits input/resources (not HoldToFill). SpawnInsertBubble/DismissInsertBubble lifecycle managed at register, feed, resource-gate, and collect. |
| 2026-03-26 | Co-Work | POIBubble.GetIconImage(): New method finds "Icon" Image within active variant — used by BuildingProductionManager for reward/input icons on Bubble_Collect/Bubble_Insert. |

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
- 2026-03-22: SESSION HANDOFF (Co-Work → new Co-Work agent). Pending work for next agent:
  1. **Map Density Slider** (plan at `/sessions/…/.claude/plans/async-knitting-codd.md`): Add `mapDensity` field to MapGeneratorV2.cs, scale Scattered/Clusters/Edge generation, add slider + estimate to MapGeneratorV2Editor.cs. Has full plan written — just needs implementation.
  2. **Trello Card #90** (RandomTier0-3): Claude Code marked Ready for Review. Jai must review/test in Unity before closing.
  3. **Barracks prefab + icon**: Needs a prefab with a prefab guid assigned in BuildingDatabase.asset — currently uses placeholder `47b7e0cce81224b9f987986ef1ad5a4b`. Verify in Unity.
  4. **Kitchen + Meal prefabs/icons**: Human To-Do cards on Trello. Need creation in Unity.
  5. **Cards #27 and #61** still in Ready for Review — awaiting Jai's in-game testing.
  6. **Google Sheets sync** — after any .asset changes, run SheetSyncEditor to keep sheets current.
  7. **Workers & Entities tier column** — verify Fighter tier = 3 in sheet matches WorkerDatabase.asset (drawWeight=0, no tier field on WorkerData currently). Check if SheetSyncEditor handles tier for workers.
- 2026-03-22: SESSION CONTINUATION (Co-Work). Changes this session:
  1. **SheetSyncEditor SyncDrawButton()**: Fixed column references — "Cost"→"Cost Type", "Value"→"Cost Amount" to match actual sheet headers.
  2. **DrawButton Google Sheet**: Updated first 3 Output entries from "👷 Worker" to Home/Statue/Torch. Updated Output dropdown validation to include all buildings (Home, Torch, Statue, Barracks, Kitchen, Feast), workers (Worker, Fighter), and tier draws (RandomTier0-3, RandomBuilding, None).
  3. **MapGeneratorV2 SetupDeck()**: Removed Home/Statue from starting hand. Player now starts with only a Worker card. Draw button delivers subsequent cards.
  4. **Button_Battle (draw button) hidden on startup**: Set `m_IsActive: 0` in scene file. DockBarManager.RemoveCard() calls `drawButtonController.Show()` after first card placement to activate it. Show/Hide target `drawButton.gameObject` (Button_Battle), not the Lobby panel.
  5. **DrawButtonController**: Per-level output, cost (multi-currency via ResourceManager), cooldown. Sheet-driven via DrawButtonEntry list serialized in scene (33 levels). Levels up on each successful draw. `workerDatabase` reference wired in scene.
  6. **Draw button animations**: Pop-in scale animation on first reveal. Bouncy scale animation when cooldown ends (catches player's eye). Punch animation on click.
  7. **Card fly-in arc from draw button**: All AddCard/AddWorkerCard calls from DrawButtonController now pass `animateFromDraw: true`. CardFlyInAnimation uses upward arc (`Sin(t*PI) * arcHeight`) matching world-collection fly style. AddWorkerCard gained `animateFromDraw` parameter.
  6. **RaritySystem**: Added `DrawRandomUnitUpToTier(int maxTier)` — cumulative tier draw (RandomTier1 = Tier 0 + Tier 1 pool).
  7. **ResourceManager.cs**: Starting gold fallback changed 20→0 to match sheet.
  8. **WorkerData.cs**: Added `tier` and `isSlotTakeable` fields synced from Workers & Entities sheet.
  9. **Duplicate Feast removal**: Removed manual Feast UnitStats creation in SetupDeck (BuildingDatabase version is authoritative).
  10. **Fighter card fix**: SetupDeck now pulls Fighter data from WorkerDatabase.GetByName("Fighter") instead of null workerTemplate.
- 2026-03-23: SHEET SYNC + KITCHEN RESOURCE COST:
  - **Sheet sync**: Pulled fresh data from all Google Sheets. Updated BuildingDatabase.asset (Home/Barracks/Kitchen intervals+bonuses, wildAnimalInteractible), EnvironmentDatabase.asset (Goldmine HP 300→300000), EconomyBalanceConfig.asset (Fighter Food cost slot added), SheetCache.json (lastSynced, all Buildings values current).
  - **Kitchen resource cost** (data-driven `productionCostResourceType/Amount`): Added fields to BuildingData.cs + UnitStats.cs, copied in MapGeneratorV2.SetupDeck(), added `waitingForResources` flag to ProductionEntry + RegisterBuilding, resource guard in OnIntervalTick (SpendResources pre-pay), reset in CollectReward. SheetSyncEditor.SyncBuildings() parses new "Cost Resource"/"Cost Amount" columns. SheetCache.json updated. Kitchen = Food/10. All other buildings = None/0.
- 2026-03-24: CORRUPTION SYSTEM DATA ARCHITECTURE (#22):
  - **GameUnitType.Corruption** added to enum in UnitData.cs. Corruption hearts/spikes are now UnitDatabase entries.
  - **CorruptionHeart refactor**: Removed `Initialize(CorruptionData)`. Stats (maxHP, attackPower, floatingIndicatorPrefab) are now [SerializeField] on the prefab itself. Set values directly in the Unity prefab Inspector.
  - **CorruptionSpawnEntry**: Added `GameObject prefab` field. entityName (hidden) still used as planGrid key — populated from `unitData.assetName` during sync.
  - **MapGeneratorV2**: Removed `corruptionDatabase` field. `SyncCorruptionSpawnEntries()` now pulls from `unitDatabase.GetByType(GameUnitType.Corruption)`. `PlaceCorruptionEntities()` and `SpawnAllCorruptionEntitiesStaggered()` use `entry.prefab` directly — no DB lookup.
  - **MapGeneratorV2Editor**: All CorruptionDatabase references removed. Corruption sync button and entry cards now driven by UnitDatabase.
  - **Deleted**: `CorruptionDatabase.cs`, `CorruptionData.cs`, `CreateCorruptionDatabase.cs` (+ .meta files).
  - **Next step for Jai**: In Unity — (1) assign heart prefabs as UnitDatabase entries with type=Corruption, (2) set HP/attack/indicator fields directly on CorruptionHeart prefab, (3) hit Sync from Database on MapGeneratorV2 to populate corruptionSpawnEntries.
- 2026-03-24: TRELLO CARD MAINTENANCE (Auto RTS Tasks):
  - #117 "New Buildings: Scrapper, Garden, Rabbit Farm": Fixed title typo (Builsings→Buildings), added Feature+Creative labels, added progress description. Sheet work done ✅. Unity prefabs/icons still pending.
  - #120 "Active flag on sheet entries": Added System+Feature labels, wrote design description.
  - #118 "Zoom out increases with map progress": Added Feature+Behavior labels, wrote design description.
  - #115 "Fix killer behavior on wild animal death": Wrote description noting slotTakeable context.
  - #22 "Corruption System": Checked (in-progress), added 🔍 comment. Implementation completed this session.
- 2026-03-24: GOOGLE SHEETS (Placement Costs + Buildings & Production):
  - Applied alternating zebra stripe formatting across all 9 building sections × 30 data rows on Placement Costs sheet.
  - Fixed Placement Costs headers: U1="🐇 Rabbit Farm", AD1="🌿 Garden".
  - Fixed Buildings & Production B8=🔧 Scrapper, B9=🐇 Rabbit Farm, B10=🌿 Garden emoji prefix.
- 2026-03-25: SESSION — POI SYSTEM + GOOGLE SHEETS RESTRUCTURE:
  - **Trello card merges completed**: #144→#117, #145→#117, #146→#92, #118→#101. Merged cards archived.
  - **MapGeneratorV2 typo fix**: `InitialCorruptedRadius` → `InitialCorruptionRadius` (line 2424).
  - **Mountain→Coral rename**: Environment & Loot sheet, SheetCache.json, EnvironmentDatabase.asset, MapGeneratorV2 keyword match.
  - **Reed environment type added**: ResourceType.Reed=33, EnvironmentDatabase.asset entry (lootResourceType=17/Grass), Google Sheet row.
  - **PointsOfInterest sheet created**: Columns: Object, Grouping, Quantity Minimum, Name, Color, Reward Type, Reward Quantity.
  - **Ref tab created**: Central reference tab for dynamic dropdowns. Columns: Objects (env with emoji), Units (with emoji), Currencies (emoji+name), Manual (special values), Output combo (Units+Objects+Manual), Drops combo (Currencies+Manual).
  - **Environment & Loot Icon column added** (column B, inserted): ⛏️ Goldmine, 🌲 Tree, 🪨 Rock, 🪸 Coral, 💧 Water, 🌻 Flowers, 🦴 Bone, 🦎 Lizard, 🌾 Reed, 🔩 Scrap. **NOTE: This shifted all E&L columns right by 1** — Object is now column D (was C), Drops is now column E (was D), etc.
  - **Workers & Entities emojis added**: Corrupted Heart=🗼, Spike 1=🦑, Spike 2=🐙.
  - **Data validation rewired**: E&L Drops + Drop on Death → `Ref!$F:$F` (Drops combo). Buildings & Production Input Card → `Ref!$G:$G` (Input combo). POI Object → `Ref!A`, POI Reward Type → `Ref!D`.
  - **Design doc**: `docs/plans/2026-03-25-unified-bubble-system-design.md` — unified bubble prefab (POI + building popups), incremental rollout.
- 2026-03-25: GOOGLE SHEETS STRUCTURE — FOR CLAUDE CODE REFERENCE:
  - **Ref tab** is the central source for all dropdown validation. Any sheet needing a dropdown references Ref columns.
  - **Ref column layout**: A=Objects (env emoji+name from E&L B+D), B=Units (emoji+name from W&E B+C), C=Currencies (emoji+name from Currencies A+B), D=Manual (hardcoded special values like ❌ None, ⏳ Hold to Fill, 🎲 Any Resource, 📦 Tier 0-3 Resource, 🏗️ Tier 0-3 Building, 🃏 Any Card), E=Output combo (B+A+D), F=Drops combo (C+D).
  - **Environment & Loot columns** (after Icon insert): A=Active, B=Icon, C=MapGenerated, D=Object, E=Drops, F=Loot per Hit, G=HP, H=Total Yield, I=Ally Interactible, J=Enemy Interactible, K=Wild Animal Interactible, L=Killer's Behavior, M=Drop on Death. **SheetSyncEditor column references need updating to match**.
  - **PointsOfInterest columns**: A=Object (dropdown from Ref!A), B=Grouping (Singular/Cluster/Area), C=Quantity Minimum, D=Name, E=Color (Gold/Grey/Red), F=Reward Type (dropdown from Ref!D), G=Reward Quantity.
  - **Current POI entries**: Row 2: Tree/Cluster/5/Forest/Grey. Row 3: Heart of Corruption/Singular/1/!!!/Red.
  - **Spreadsheet ID**: `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`
  - **Sheet IDs**: PointsOfInterest=764607241, Ref=1089629018, Environment & Loot=1027353443, Buildings & Production=2122729009.
- 2026-03-25: PENDING CODE CHANGES FOR POI SYSTEM — ✅ DONE BY CO-WORK (2026-03-26):
  - All items below completed. See 2026-03-26 session entries.
- 2026-03-26: SESSION — DROPDOWN REWIRING + SHEET CLEANUP:
  - **All dropdown validations across all sheets** now reference Ref tab dynamically (ONE_OF_RANGE). No more static ONE_OF_LIST.
  - **Ref!D Manual column expanded**: Added 🎲 RandomTier0-3, 🎲 RandomBuilding, 🍖 Meal.
  - **Ref!E Output combo formula updated**: Now includes Buildings & Production (was missing). Full list: Units + Buildings + Environment + Manual (44 entries).
  - **DrawButton**: Output→Ref!E, Cost Type→Ref!F. Fixed Scrapper emoji.
  - **Buildings & Production**: Input Card→Ref!E, Resource Use→Ref!F, Output Card→Ref!E. All values updated to emoji format.
  - **Environment & Loot**: Drops→Ref!F, Drop on Death→Ref!F. Values fixed.
  - **PointsOfInterest**: Active→checkbox, Object→Ref!E, Grouping→ONE_OF_LIST, Color→ONE_OF_LIST Gold/Grey/Red, Reward Type→Ref!F. Fixed stale "Refs" tab references and swapped validations.
  - **Ref column layout**: A=Objects, B=Units, C=Currencies, D=Manual (18 entries), E=Output combo (Units+Buildings+Objects+Manual), F=Drops combo (Currencies+Manual). 6 columns total.
- 2026-03-26: POI BUBBLE SYSTEM CODE (Co-Work):
  - **BubbleType.cs** (NEW): Enum with POI_Gold, POI_Grey, POI_Red, Bubble_Insert, Bubble_Collect, Bubble_Alert. Child GameObjects must be named exactly as enum values.
  - **POITypeData.cs** (REWRITTEN): Added POIGrouping enum (Singular/Cluster/Area), POITier enum (Gold/Grey/Red), new fields: groupingType, quantityMinimum, tier, rewardType (ResourceType), rewardQuantity. Legacy bubbleColor + approvalReward kept as [HideInInspector]. GetBubbleType() helper maps tier→BubbleType.
  - **POIBubble.cs** (REWRITTEN): Auto-discovers children by name matching BubbleType enum values on Awake. Setup(BubbleType, text, worldPos) toggles the correct child and finds its TextMeshProUGUI label. DeactivateAllChildren() on dismiss. Legacy Setup(text, color, worldPos) overload preserved.
  - **POIManager.cs** (UPDATED): ShowBubble now takes BubbleType param. RegisterHeart uses POI_Red. RefreshEnvWindow reads tier from POITypeData.GetBubbleType(). AwardReward uses data.rewardType + data.rewardQuantity instead of hardcoded Approval. New `bubbleParent` Transform field for World Canvas parenting. Pool creates instances under bubbleParent.
  - **SheetSyncEditor.cs SyncPOI()** (REWRITTEN): Sheet key changed "Points of Interest"→"PointsOfInterest". Column mapping: Active (filter FALSE), Object→typeName (StripEmoji), Grouping→groupingType, Quantity Minimum→quantityMinimum, Name→label, Color→tier (POITier enum parse), Reward Type→rewardType (ResourceType enum parse), Reward Quantity→rewardQuantity.
- 2026-03-26: REMAINING WORK FOR CLAUDE CODE:
  - **SheetSyncEditor.cs SyncEnvironment()**: Column indices still shifted +1 from Icon column insert. Object was index 2→3, Drops was 3→4, etc. Must update all column references or switch to column-name-based lookup.
  - **SheetCache.json**: Add "PointsOfInterest" section with current sheet data (4 entries: Tree/Forest/Grey, Corrupted Heart/!!!/Red, Water/River/Grey, Coral/Treasure/Gold). Columns: Active, Object, Grouping, Quantity Minimum, Name, Color, Reward Type, Reward Quantity.
  - **POIDatabase.asset**: Must be created in Unity Editor (Create → RTChess → POI Database). Then assign to POIManager.poiDatabase in Inspector. Run SyncPOI to populate.
  - **POIManager Inspector setup**: Assign bubblePrefab (the BubblePopup prefab), poiDatabase, and bubbleParent (World Canvas or child holder).
- 2026-03-26: GATHERING DETECTION SYSTEM (Co-Work):
  - **EnvironmentGathering.cs** (NEW): Data class for a contiguous group of same-type environment tiles. Fields: assetName, cells (List<Vector2Int>), centroid, size. Named "Gathering" to avoid collision with SpawnMode.Clustered.
  - **MapGeneratorV2.cs**: Added `detectedGatherings` list + public `DetectedGatherings` read-only property. `DetectGatherings()` runs after PlaceAllEntries+PlaceCorruptionEntities — BFS flood-fill (4-connected, cardinal only) finds all same-type groups. `FloodFillGathering()` does the BFS. Results passed to `POIManager.RegisterGatherings()` before Initialize().
  - **POIManager.cs**: Added `RegisterGatherings(IReadOnlyList<EnvironmentGathering>)` — filters gatherings against POIDatabase, only Cluster/Area types that meet quantityMinimum get registered at centroid. `RegisterEnvPOI()` now only handles Singular-type POIs (skips Cluster/Area to avoid double-registration).
  - **MapGeneratorV2.cs cleanup**: Fully reverted prior unauthorized cluster detection code (DetectClusters, FloodFillCluster, RegisterClusterPOIs, poiDatabase field, EnvironmentCluster class all removed). Restored per-object RegisterEnvPOI call.
  - **Design decisions** (confirmed via AskUserQuestion): 4-connected adjacency, one-time on generation, MapGen stores all gatherings blindly + POIManager filters via POIDatabase, "Gathering" naming.
- 2026-03-22: DATA CONSISTENCY AUDIT & FIXES:
  - **TrainingFacility**: Removed from BuildingDatabase.asset (correctly — not in Google Sheets)
  - **Fighter**: Restored to WorkerDatabase.asset (was incorrectly deleted; Google Sheets shows it as Worker type with tier 3, hp 10). Values synced from sheet.
  - **RotateRotateMove**: Confirmed as real behavior in BehaviorType.cs (Mammoth correctly uses it)
  - All data now consistent between Google Sheets and .asset files for Buildings, Workers, Units, Environment
