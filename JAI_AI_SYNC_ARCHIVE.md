# JAI_AI_SYNC Archive

> Entries older than 24 hours are moved here from JAI_AI_SYNC.md at the end of each session.
> Read this only when investigating a specific historical issue — do NOT read at session start.

---

## Completed Work (archived)

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
| 2026-03-21 | Co-Work | RTChess.md: Added comprehensive Google Sheets formatting rules, sheet IDs, column-to-code mappings |
| 2026-03-21 | Co-Work | Data verification: Compared all 3 synced sheets against 4 .asset databases, fixed mismatches |
| 2026-03-21 | Co-Work | Workers & Entities: Re-added Entity column, fixed Dinosaur/Mammoth Attack Behavior → Hostile |
| 2026-03-21 | Co-Work | UnitDatabase.asset: Added isSlotTakeable=1 for Dinosaur and Mammoth |
| 2026-03-21 | Co-Work | SheetCache.json: Updated to reflect all final sheet values including Entity column and Attack Behavior fixes |
| 2026-03-21 | Co-Work | Google Sheets: Fixed misplaced validations (Type dropdown leaked to Entity column) |
| 2026-03-21 | Co-Work | Google Sheets: Fixed None grey text — TEXT_EQ fails silently, switched to CUSTOM_FORMULA |
| 2026-03-21 | Co-Work | Google Sheets: Removed broken conditional format on Currencies D column |
| 2026-03-21 | Co-Work | Documentation: Updated RTChess.md gotchas (5→10 rules), Trello Google Sheets card, CLAUDE_USER_JAI.md with hard-won Sheets API lessons |
| 2026-03-21 | Co-Work | Full sheet→asset sync: Fixed Torch fogRevealRadius (1→2), TrainingFacility productionInputType (None→Worker), Feast isMealSource (0→1). All 7 sheets verified against code. |
| 2026-03-21 | Co-Work | Killer's Behavior: Added killerAdvances bool to BuildingData, EnvironmentData, WorkerData, UnitData, UnitStats. All .asset files updated. SheetSyncEditor updated. |
| 2026-03-21 | Co-Work | Trello: Created 8 Suggestion cards from voice transcript (Meal Buff Visual, Feast Degradation, etc.) |
| 2026-03-21 | Co-Work | Trello: Created 6 Suggestion cards from temple/tiers transcript (Building Tiers System, Button Level-Up, etc.) |
| 2026-03-21 | Co-Work | #45 Feast Exclusivity: Added isRandomBuilding to BuildingData/UnitStats, set Feast=false, filtered in RaritySystem.DrawRandomUnit() |
| 2026-03-21 | Co-Work | #47 InteractionRegistry: Added per-faction flags (allyInteractible, enemyInteractible, wildAnimalInteractible), InteractorType enum, CanInteract() |
| 2026-03-21 | Co-Work | #46 Wild Animals + Feast: Modified GridEntityActor.TryMoveForward() — wild animals now check InteractionRegistry.CanInteract(WildAnimal) |
| 2026-03-21 | Co-Work | Environment interaction columns added to EnvironmentData.cs, .asset, SheetSyncEditor, InteractionRegistry |
| 2026-03-22 | Co-Work | Bug fix: DockBarManager.FloatAndFadePopup — null guards for destroyed RectTransform/TextMeshPro mid-animation |
| 2026-03-22 | Co-Work | Safety fix: InteractionRegistry.CanInteract() — unknown entries default deny for WildAnimal |
| 2026-03-22 | Co-Work | Removed Grave system: deleted DB entry, corpse-spawning code, SheetCache entry, Sheet row. Card #62 archived. |
| 2026-03-22 | Co-Work | #59: Slot reservation system. BuildingProductionManager uses dock.IsHandFull (includes reservedSlots). SpawnWorkerFly/SpawnCardFly return bool. |
| 2026-03-22 | Co-Work | #58: GetNextSlotWorldPosition() — placeholder RectTransform approach, forces LayoutRebuilder, reads real position. |
| 2026-03-22 | Co-Work | #60 Building HP damage text: GridEntityHPBar auto-enables showDamagePopup for allied non-environment entities. |
| 2026-03-22 | Co-Work | #61: Tuned coin SFX debounce/burst (BURST_RESET 0.5→1.5s, debounce 30→15ms, pitch ramp 0.08→0.12/step, burst cap 10→15). |
| 2026-03-22 | Co-Work | #36 Kitchen + Meal: Meal popup icon fix, MealBuffSource radius behavior (3-cell aura), MealDegradation (1 HP/5s). → Ready for Review |
| 2026-03-22 | Co-Work | #27 Training System: TrainingFacility added to BuildingDatabase. Verified DragDropHandler + BPM fully support it. → Ready for Review |
| 2026-03-22 | Co-Work | #61 Multi-pickup SFX: Verified implementation in GameSFXManager.cs. → Ready for Review |
| 2026-03-22 | Co-Work | Synced Feast data from Google Sheets to BuildingDatabase.asset (hp 5→10, drawWeight 0→0.5, etc.). |
| 2026-03-22 | Co-Work | #88: ConeTent→Home rename in BuildingDatabase, Google Sheets, code comments. |
| 2026-03-22 | Co-Work | #89: TrainingFacility removed from BuildingDatabase + Sheets. Barracks is the training facility. |
| 2026-03-22 | Claude Code | #90 RandomTier0-3: Tier field on BuildingData/UnitStats, DrawRandomUnitByTier() in RaritySystem, .asset files synced. → Ready for Review |
| 2026-03-22 | Co-Work | Home Worker fix: ProductionOutputType.Worker now calls workerDatabase.GetByName("Worker") directly. |
| 2026-03-22 | Co-Work | Gold startingAmount fix: CurrencyDatabase.asset Gold 20→0 to match sheet. |
| 2026-03-22 | Co-Work | Documentation: CLAUDE_USER_JAI.md — checkmark timing table, 10-step task workflow, AskUserQuestion mandate. |
| 2026-03-22 | Co-Work | Documentation: Trello Cards #85 + #70 updated with AI best practices. |
| 2026-03-23 | Co-Work | #97 Draw button cost feedback: Timer bubble shows cost after cooldown, red if can't afford, updates live. DrawButtonController.cs |
| 2026-03-23 | Co-Work | #101 Map scale: Grid 80×80→120×120. Camera zoom absoluteMax 60→80. Pan distance dynamic (basePan + revealedTiles * 0.05). |
| 2026-03-23 | Co-Work | Trello cleanup: Merged AI Guideline cards (10→4), merged SFX pitch bug into #61, merged meal buff cards (5→2). |
| 2026-03-24 | Co-Work | #22 Corruption data architecture: GameUnitType.Corruption added. CorruptionDatabase/CorruptionData deleted. MapGeneratorV2 uses UnitDatabase. |
| 2026-03-24 | Co-Work | Google Sheets: Alternating zebra stripes across all 9 Placement Costs sections. |
| 2026-03-24 | Co-Work | #115 Fix killer behavior: cachedSlotTakeable fix in place from prior session. → Ready for Review |
| 2026-03-24 | Co-Work | #118 Zoom with progress: GridCamera subscribes to FogManager.OnCellRevealed → RecalculateZoomLevels(). → Ready for Review |
| 2026-03-24 | Co-Work | #120 Active flag: SyncSpawnEntries() and SyncUnitSpawnEntries() filter inactive entries. → Ready for Review |
| 2026-03-24 | Co-Work | #22 Corruption: Thorns + spike spawning added to CorruptionHeart. OnDamagedBy event + TakeDamageFrom() on GridEntityHealth. → Ready for Review |
| 2026-03-24 | Co-Work | New enums: ProductionInputType.Any, ProductionOutputType.Scrap/PetRabbit/TreeSeed, ResourceType.Scrap. |
| 2026-03-24 | Co-Work | UnitDatabase.asset: CorruptedHeart, Spike1, Spike2, PetRabbit, TreeSeed entries added. |
| 2026-03-24 | Co-Work | BuildingDatabase.asset: Scrapper, Rabbit Farm, Garden entries added. |
| 2026-03-24 | Co-Work | Scrap/PetRabbit/TreeSeed moved from BuildingDatabase → EnvironmentDatabase. |
| 2026-03-24 | Co-Work | EnvironmentData.cs: isMapGenerated + dropOnDeath fields. UnitData.cs: dropOnDeath field. |
| 2026-03-24 | Co-Work | SheetSyncEditor: SyncEnvironment() syncs MapGenerated + Drop on Death. SyncUnits() syncs Drop on Death + Drops + Loot per Hit. |
| 2026-03-24 | Co-Work | Pet Rabbit → Lizard rename across all files. |
| 2026-03-24 | Co-Work | Custom PropertyDrawers: DatabaseEntryDrawers.cs — Inspector shows asset names instead of "Element N". |
| 2026-03-24 | Co-Work | SyncCorruptionSpawnEntries() rewritten. SyncSpawnEntries() isMapGenerated filter (Scrap/Lizard/TreeSeed excluded). |
| 2026-03-24 | Co-Work | Enemy interaction + loot gate: ScanAndInteract() faction-aware. PerformStrongInteraction() only grants loot to allied attackers. |
| 2026-03-24 | Co-Work | Same-faction skip: ScanAndInteract, ScanAndInteractWildAnimal, TryMoveForward all skip same-faction targets. |
| 2026-03-24 | Co-Work | isMapGenerated for units: Added to UnitData.cs. Spike1/Spike2 excluded from unitSpawnEntries. |
| 2026-03-26 | Co-Work | POI bubble scale fix: WorldCanvas_Popups scale issue. Added targetScale to POIBubble, bubbleWorldScale to POIManager (default 0.005). |
| 2026-03-26 | Co-Work | Building bubble integration: BPM supports buildingBubblePrefab. Bubble_Collect replaces procedural popup. Bubble_Insert for awaiting input. |
| 2026-03-26 | Co-Work | POIBubble.GetIconImage(): Finds "Icon" Image within active variant for reward/input icons. |
| 2026-03-28 | Co-Work | BehaviorType.RotateAndMoveCorrupted (=3): Corruption spikes move only onto SurfaceType.Corruption tiles. |
| 2026-03-28 | Co-Work | Tile Layer System (#159): SurfaceType enum + PlaceSurface/RemoveSurface/GetSurface/HasSurface API on GridManager. |
| 2026-03-28 | Co-Work | CorruptionManager: MarkAsCorrupted() → PlaceSurface(Corruption). ClearCorruption() → RemoveSurface(). |
| 2026-03-28 | Co-Work | GridEntityActor walkableSurfaces: CanWalkOnTile() gate in TryMoveForward(). UnitData/UnitStats both have walkable field. |
| 2026-03-28 | Co-Work | EnvironmentLayerType enum (Object/Surface) + layerType on EnvironmentData. SheetSyncEditor syncs "Type" column. |
| 2026-03-28 | Co-Work | BuildingData.buildOn: Placement surface requirement. Synced from BuildOn column in Buildings & Production sheet. |
| 2026-03-28 | Co-Work | Ref sheet #REF! fix: FILTER formula blocked by manual ☠️ in Ref!A12. Fixed by adding ☠️ to Environment sheet B14. |
| 2026-03-28 | Co-Work | Need bubble fix: BPM.SetBubblePrefabs() auto-assigns needBubblePrefab = insertPrefab when null. |
| 2026-03-28 | Co-Work | DevCheatMenu: FreeCosts toggle (bypasses placement + draw costs). InstantProduction toggle (1s intervals). Both #if DEVELOPMENT_BUILD. |
| 2026-03-28 | Co-Work | Particle prefab fix: VelocityModule minMaxState mismatch in Corruption Tile.prefab, CorruptedLighthouse.prefab, Mu_TinyChair.prefab. |
| 2026-03-28 | Co-Work | Post-mortem audit: 10 ranked pain points, 6 skills proposed. Full report: ClockworkCraft_PostMortem_Audit.docx. |

---

## Session Detail Logs (archived)

### 2026-03-22: SESSION HANDOFF

Pending work at handoff:
1. Map Density Slider (plan at `/sessions/vibrant-practical-thompson/mnt/.claude/plans/async-knitting-codd.md`): Add `mapDensity` to MapGeneratorV2.cs, scale Scattered/Clusters/Edge generation, add slider + estimate to editor.
2. Trello Card #90 (RandomTier0-3): Claude Code → Ready for Review. Jai must test in Unity.
3. Barracks prefab: Needs prefab guid in BuildingDatabase.asset (placeholder `47b7e0cce81224b9f987986ef1ad5a4b`).
4. Kitchen + Meal prefabs/icons: Human To-Do cards on Trello.
5. Cards #27 and #61: Ready for Review, awaiting in-game testing.
6. Google Sheets sync: After any .asset changes, run SheetSyncEditor.
7. Workers & Entities tier column: Verify Fighter tier=3 in sheet matches WorkerDatabase.asset.

### 2026-03-22: SESSION CONTINUATION

Changes:
1. SheetSyncEditor SyncDrawButton(): "Cost"→"Cost Type", "Value"→"Cost Amount".
2. DrawButton Sheet: First 3 Output entries from "Worker" → Home/Statue/Torch. Output dropdown updated.
3. MapGeneratorV2 SetupDeck(): Removed Home/Statue from starting hand. Player starts with only Worker card.
4. Button_Battle hidden on startup. DockBarManager.RemoveCard() calls drawButtonController.Show() after first placement.
5. DrawButtonController: Per-level output, cost (multi-currency), cooldown. Sheet-driven via DrawButtonEntry (33 levels).
6. Draw button animations: Pop-in on reveal, bouncy on cooldown end, punch on click.
7. Card fly-in arc from draw button: animateFromDraw=true for all DrawButtonController AddCard/AddWorkerCard calls.
8. RaritySystem: DrawRandomUnitUpToTier(int maxTier) — cumulative tier draw.
9. ResourceManager.cs: Starting gold fallback 20→0.
10. WorkerData.cs: Added tier + isSlotTakeable fields.
11. Duplicate Feast removal from SetupDeck.
12. Fighter card fix: SetupDeck pulls from WorkerDatabase.GetByName("Fighter").

### 2026-03-23: SHEET SYNC + KITCHEN RESOURCE COST

- Pulled fresh data from all Google Sheets. Updated BuildingDatabase.asset, EnvironmentDatabase.asset (Goldmine HP 300→300000), EconomyBalanceConfig.asset, SheetCache.json.
- Kitchen resource cost (data-driven productionCostResourceType/Amount): Fields added to BuildingData.cs + UnitStats.cs. waitingForResources flag in ProductionEntry. Resource guard in OnIntervalTick. SheetSyncEditor parses "Cost Resource"/"Cost Amount". Kitchen = Food/10.

### 2026-03-24: CORRUPTION SYSTEM DATA ARCHITECTURE

- GameUnitType.Corruption added. CorruptionHeart stats now [SerializeField] on prefab.
- CorruptionSpawnEntry: prefab field added. entityName populated from unitData.assetName during sync.
- MapGeneratorV2: Removed corruptionDatabase. SyncCorruptionSpawnEntries() pulls from unitDatabase.GetByType(Corruption).
- Deleted: CorruptionDatabase.cs, CorruptionData.cs, CreateCorruptionDatabase.cs.
- Jai TODO: (1) Assign heart prefabs in UnitDatabase with type=Corruption. (2) Set HP/attack on CorruptionHeart prefab. (3) Hit Sync from Database on MapGeneratorV2.

### 2026-03-24: TRELLO CARD MAINTENANCE

- #117: Fixed title typo, added Feature+Creative labels, added progress description.
- #120: Added System+Feature labels, wrote design description.
- #118: Added Feature+Behavior labels, wrote design description.
- #115: Wrote description noting slotTakeable context.
- #22: Checked in-progress, added comment. Implementation completed this session.

### 2026-03-25: POI SYSTEM + GOOGLE SHEETS RESTRUCTURE

- Trello merges: #144→#117, #145→#117, #146→#92, #118→#101.
- Mountain→Coral rename across sheet, SheetCache, EnvironmentDatabase.
- Reed environment type added (ResourceType.Reed=33).
- PointsOfInterest sheet created: Object, Grouping, Quantity Minimum, Name, Color, Reward Type, Reward Quantity.
- Ref tab created: A=Objects, B=Units, C=Currencies, D=Manual, E=Output combo, F=Drops combo.
- Environment & Loot Icon column inserted (column B) — shifted all E&L columns right by 1. Object now col D (was C), Drops col E (was D), etc.
- Workers & Entities emojis: Corrupted Heart=🗼, Spike 1=🦑, Spike 2=🐙.
- Design doc: `docs/plans/2026-03-25-unified-bubble-system-design.md`.

### 2026-03-25: GOOGLE SHEETS COLUMN REFERENCE (for Claude Code)

- **Spreadsheet ID**: `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`
- **Sheet IDs**: PointsOfInterest=764607241, Ref=1089629018, Environment & Loot=1027353443, Buildings & Production=2122729009.
- **Ref columns**: A=Objects, B=Units, C=Currencies, D=Manual (18 entries), E=Output combo, F=Drops combo.
- **Environment & Loot columns** (post Icon insert): A=Active, B=Icon, C=MapGenerated, D=Object, E=Drops, F=Loot per Hit, G=HP, H=Total Yield, I=Ally Interactible, J=Enemy Interactible, K=Wild Animal Interactible, L=Killer's Behavior, M=Drop on Death.
- **PointsOfInterest columns**: A=Object, B=Grouping, C=Quantity Minimum, D=Name, E=Color, F=Reward Type, G=Reward Quantity.

### 2026-03-26: DROPDOWN REWIRING + SHEET CLEANUP

- All dropdown validations now reference Ref tab dynamically (ONE_OF_RANGE). No more static ONE_OF_LIST.
- Ref!D Manual column expanded: Added 🎲 RandomTier0-3, 🎲 RandomBuilding, 🍖 Meal.
- Ref!E Output combo: Now includes Buildings & Production. Full list: Units + Buildings + Environment + Manual (44 entries).
- DrawButton: Output→Ref!E, Cost Type→Ref!F.
- Buildings & Production: Input Card→Ref!E, Resource Use→Ref!F, Output Card→Ref!E.
- Environment & Loot: Drops→Ref!F, Drop on Death→Ref!F.
- PointsOfInterest: Active→checkbox, Object→Ref!E, Grouping→ONE_OF_LIST, Color→ONE_OF_LIST.

### 2026-03-26: POI BUBBLE SYSTEM CODE

- **BubbleType.cs** (NEW): Enum — POI_Gold, POI_Grey, POI_Red, Bubble_Insert, Bubble_Collect, Bubble_Alert.
- **POITypeData.cs** (REWRITTEN): POIGrouping + POITier enums, new fields (groupingType, quantityMinimum, tier, rewardType, rewardQuantity). GetBubbleType() maps tier→BubbleType.
- **POIBubble.cs** (REWRITTEN): Auto-discovers children by BubbleType name on Awake. Setup(BubbleType, text, worldPos) toggles correct child.
- **POIManager.cs** (UPDATED): ShowBubble takes BubbleType. RegisterHeart uses POI_Red. AwardReward uses data.rewardType + data.rewardQuantity. bubbleParent Transform field added.
- **SheetSyncEditor.SyncPOI()** (REWRITTEN): Sheet key "Points of Interest"→"PointsOfInterest". Full column mapping.

### 2026-03-26: REMAINING WORK FOR CLAUDE CODE (still pending as of 2026-03-29)

- **SheetSyncEditor.SyncEnvironment()**: Column indices shifted +1 from Icon insert. Object 2→3, Drops 3→4, etc.
- **SheetCache.json**: Add "PointsOfInterest" section (4 entries: Tree/Forest/Grey, Corrupted Heart/!!!/Red, Water/River/Grey, Coral/Treasure/Gold).
- **POIDatabase.asset**: Create in Unity (Create → RTChess → POI Database). Assign to POIManager.poiDatabase. Run SyncPOI.
- **POIManager Inspector**: Assign bubblePrefab, poiDatabase, bubbleParent.

### 2026-03-26: GATHERING DETECTION SYSTEM

- **EnvironmentGathering.cs** (NEW): assetName, cells (List<Vector2Int>), centroid, size.
- **MapGeneratorV2.cs**: detectedGatherings + DetectGatherings() — BFS flood-fill (4-connected) after PlaceAllEntries. Results passed to POIManager.RegisterGatherings().
- **POIManager.cs**: RegisterGatherings() filters against POIDatabase — Cluster/Area types meeting quantityMinimum registered at centroid.
- Design decisions: 4-connected adjacency, one-time on generation, MapGen stores blindly + POIManager filters.

### 2026-03-29: POST-MORTEM + PHASE 1-2 EXECUTION (prior Co-Work session)

- Post-mortem .docx regenerated: ClockworkCraft_PostMortem_Audit.docx saved to project root. Section 7 in execution order.
- Phase 1-2 checklist items completed: CLAUDE.md audit, doc ownership split, stale items removed, JAI_AI_SYNC.md restructured, large file audit (9 files > 600 lines), FurnitureObject rename analysis → Trello #173.
- FurnitureObject rename (#173): Will's MapGeneratorV2 references it. Shim approach = PlacedObject base + FurnitureObject wrapper. Coordinate with Will.
- Trello cleanup: Done/duplicate cards archived, 7 skill cards moved to To Do Humans, #173 added to Tasks Claude.

---

## Resolved Decisions (archived)

- 2026-03-20: PlacementCostDisplay redesigned from orbiting to vertical column layout (v6).
- 2026-03-20: ValidatePlacement split into IsCellValid (cell only) and ValidatePlacement (cell + afford).
- 2026-03-20: Workers only advance into killed target's cell if target is environment (no GridEntityActor).
- 2026-03-20: Trello board "Auto RTS" is the task board for ClockworkCraft.
- 2026-03-21: Slot-takeable uses explicit isSlotTakeable field (auto-derived from !isActive).
- 2026-03-21: Input-triggered production: buildings with productionInputType != None wait for a matching card.
- 2026-03-21: Meal buff is flag-only (no mechanical effect yet).
- 2026-03-21: Meals are NOT allied — workers interact with them like enemies. HP=3.
- 2026-03-21: InteractionRegistry has 3 bool columns (ally/enemy/wildAnimal) per entry.
- 2026-03-21: Wild animals use PerformStrongInteraction on interactible targets. Feast has killerAdvances=false.
