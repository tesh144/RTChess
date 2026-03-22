---
name: new-object
description: "Streamlined pipeline for adding new game objects to ClockworkCraft. Use this skill whenever the user wants to add a new building, worker, unit, or environment object to the game. Triggers on phrases like 'add a new building', 'create a Kitchen', 'new enemy type', 'add an object called X', or any request to introduce a new placeable game entity. Also use when the user says 'new object pipeline' or '/new-object'. This skill handles Google Sheets data entry, C# enum updates, Unity ScriptableObject .asset modifications, and Trello card creation in one automated flow."
---

# New Object Pipeline

Add new game objects (buildings, workers/units, environment) to ClockworkCraft in one automated flow. This skill handles 4 layers: Google Sheets balance data, C# enum updates, Unity ScriptableObject .asset entries, and Trello tracking.

## CRITICAL: Safe Google Sheets Operations

**NEVER use `add_rows` or `add_columns` without specifying `start_row` / `start_column`.** These tools default to inserting at the BEGINNING of the sheet, pushing all existing data down/right and causing overwrites.

**Safe pattern for appending rows:**
1. Read the sheet to find the current last row (e.g. row 5 = index 4)
2. Use `batch_update` with `insertDimension` specifying `startIndex` = last row index + 1
3. Then write data to the new rows with `update_cells`

**Safe pattern for appending columns:**
1. Read the sheet to find the current last column
2. Use `batch_update` with `insertDimension` specifying `startIndex` = last column index + 1
3. Then write data to the new columns with `update_cells`

**NEVER use `add_rows` or `add_columns` at all.** Always use `batch_update` → `insertDimension` with explicit indices.

## When to Use

Any time the user wants to introduce a new placeable game entity — a building, worker variant, unit type, or environment object. The goal is: say "add a Kitchen that costs 10 meat" and it exists in the data layer immediately, with a Trello card tracking what still needs human attention.

## Input Flow

Gather information via AskUserQuestion (one question per message):

### Question 1: Object Type
Multiple choice — Building, Worker, Unit, or Environment.
- **Building**: Placed on grid, may produce things (ConeTent, Torch, Statue, TrainingFacility)
- **Worker**: Allied placeable unit (Worker, Fighter)
- **Unit**: Enemy or NPC entity (dinosaurs, monsters)
- **Environment**: Map objects (Tree, Rock, Goldmine, Water)

### Question 2: Name
Free text. This becomes the `assetName` in the database and the row name in sheets.

### Question 3: Key Properties (varies by type)

**For Buildings**, ask about:
- Production behavior: auto-produce, input-triggered (like Training Facility), or none
- If producing: what output type (Worker, Currency, RandomBuilding, Fighter)
- If input-triggered: what input (Worker, etc.)
- Resource costs if known
- Any special properties (HP, attack, etc.)

**For Workers**, ask about:
- Behavior type: RotateAndInteract, RotateAndMove, RotateRotateMove
- HP and attack if known
- Draw weight

**For Units**, ask about:
- Is enemy? (true/false)
- Behavior type
- Loot resource type and yield
- HP and attack if known

**For Environment**, ask about:
- Loot resource type (Gold, Wood, Stone, etc.)
- HP (how durable)
- Whether harvestable

### Question 4: Anything Else?
Open-ended catch-all for special behavior notes, related systems, or context.

If a value isn't specified, use defaults (see Defaults section below). The approach is **minimal skeleton** — only populate what's explicitly stated.

## Execution Steps

After gathering input, execute these 4 steps in order:

### Step 1: Google Sheets

**Spreadsheet ID**: `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`

Add a minimal skeleton row to the appropriate sheet:

- **Buildings** → "Buildings & Production" sheet
  - Columns: Icon, Building, Prod. Interval (s), Interval Bonus (s), Input, Output, Output Amt, HP, Attack
  - Use a relevant emoji for the icon. Leave unspecified fields as 0 or "None".

- **Workers** → "Workers & Entities" sheet
  - Columns: Entity, Type, HP, Attack Power, Behavior, Grid Size, Draw Weight, Source File
  - Source File: "WorkerDatabase.asset"

- **Units** → "Workers & Entities" sheet (same sheet as workers)
  - Same columns. Source File: "UnitDatabase.asset"

- **Environment** → "Environment & Loot" sheet
  - Check current columns at runtime with get_sheet_data before writing.

If placement cost values were specified, also add a column section to the "Placement Costs" sheet mirroring the nearest existing tier structure. If not specified, skip and note it in the Trello card.

**Important**: Before writing, read the current sheet data to find the correct next empty row. Don't overwrite existing data.

### Step 2: C# Enums (if needed)

Check whether the new object's type exists in the relevant enum. If not, add it.

- Buildings → `Assets/Scripts/Data/BuildingData.cs`
  - `BuildingType` enum (Generic, House, Shop, Workshop, Storage, Civic, Military, Religious)
  - `ProductionOutputType` enum (None, Worker, Currency, RandomBuilding)
- Workers → `Assets/Scripts/Data/WorkerData.cs`
  - `WorkerType` enum (Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter)
- Units → `Assets/Scripts/Data/UnitData.cs`
  - `GameUnitType` enum (Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter, Soldier, Archer, Beast, Boss)
- Environment → `Assets/Scripts/Data/EnvironmentData.cs`
  - `EnvironmentType` enum (Generic, Tree, Rock, Water, Path, Fence, Terrain, Flora)

To add an enum value, insert a new line before the closing brace of the enum, following the existing comment style.

### Step 3: ScriptableObject .asset File

Append a new YAML entry to the correct database .asset file. The YAML format must exactly match Unity's serialization — use 2-space indentation and match the field order of existing entries.

**File paths and list keys:**
- Buildings → `Assets/Scripts/Data/BuildingDatabase.asset` → `buildingList`
- Workers → `Assets/Scripts/Data/WorkerDatabase.asset` → `workerList`
- Units → `Assets/Scripts/Data/UnitDatabase.asset` → `unitList`
- Environment → `Assets/Scripts/Data/EnvironmentDatabase.asset` → `environmentList`

**Template for a new building entry:**
```yaml
  - assetName: NewBuildingName
    type: 0
    isFunctional: 0
    isWalkable: 0
    isActive: 0
    hp: 10
    attackPower: 0
    drawWeight: 1
    placementCost: 0
    gridSize: {x: 0, y: 0}
    visualScale: 0
    prefab: {fileID: 0}
    icon: {fileID: 0}
    productionInterval: 0
    productionIntervalBonus: 0
    productionOutputType: 0
    producedResourceType: 0
    productionAmount: 1
```

**Template for a new worker entry:**
```yaml
  - assetName: NewWorkerName
    type: 0
    isFunctional: 0
    isWalkable: 0
    isActive: 1
    behaviorType: 0
    hp: 5
    attackPower: 1
    drawWeight: 0
    gridSize: {x: 0, y: 0}
    visualScale: 0
    prefab: {fileID: 0}
    icon: {fileID: 0}
```

**Template for a new unit entry:**
```yaml
  - assetName: NewUnitName
    type: 0
    isEnemy: 0
    isFunctional: 0
    isWalkable: 1
    isActive: 1
    behaviorType: 1
    lootResourceType: 0
    lootHpCost: 1
    lootYield: 1
    hp: 3
    attackPower: 1
    drawWeight: 1
    gridSize: {x: 0, y: 0}
    visualScale: 0
    prefab: {fileID: 0}
    icon: {fileID: 0}
```

**Template for a new environment entry:**
```yaml
  - assetName: NewEnvironmentName
    type: 0
    isFunctional: 0
    isWalkable: 0
    isActive: 0
    lootResourceType: 0
    lootHpCost: 1
    lootYield: 1
    hp: 5
    attackPower: 0
    gridSize: {x: 0, y: 0}
    visualScale: 0
    prefab: {fileID: 0}
    icon: {fileID: 0}
```

Set `prefab` and `icon` to `{fileID: 0}` (null) — human assigns these later in Unity Inspector.

Replace default values with any user-specified values. For enum-typed fields, use the integer index matching the enum order.

### Step 4: Trello Card

Create a card in **Ready for Review** on the Auto RTS board.

**Board ID**: `69bd0b7483af459744b7a24c`
**Ready for Review list ID**: `69bd0cb4a2c3159d3cd6b111`

**Card name**: `New [Type]: [Name]` (e.g. "New Building: Kitchen")

**Card description** should include:
- Object name, type, and a brief description of what it does
- Which files were modified (list each: sheet name, .cs file, .asset file)
- Values that were set (table format)
- Human TODO checklist:
  - Assign prefab in Unity Inspector (BuildingDatabase / WorkerDatabase / etc.)
  - Assign icon sprite
  - Set placement costs (if not done by pipeline)
  - Tune balance values
  - Test in-game

**Labels**: Feature (`69bd0b7583af459744b7a266`) + System (`69be3cf15271604d16b71e3c`)

## Defaults

Values used when the user doesn't specify. These match the existing objects in each database.

### Buildings
| Field | Default | Notes |
|-------|---------|-------|
| type | 0 (Generic) | |
| isFunctional | 0 | |
| isWalkable | 0 | |
| isActive | 0 | |
| hp | 10 | Matches ConeTent/Torch/Statue |
| attackPower | 0 | Buildings don't attack |
| drawWeight | 1 | |
| gridSize | {x: 0, y: 0} | Unity treats as 1x1 |
| productionInterval | 0 | 0 = no production |
| productionOutputType | 0 (None) | |
| productionAmount | 1 | |

### Workers
| Field | Default | Notes |
|-------|---------|-------|
| type | 0 (Generic) | |
| isActive | 1 | Workers act each tick |
| behaviorType | 0 (RotateAndInteract) | |
| hp | 5 | |
| attackPower | 1 | |
| drawWeight | 0 | |

### Units
| Field | Default | Notes |
|-------|---------|-------|
| type | 0 (Generic) | |
| isEnemy | 0 | Set to 1 for hostile units |
| behaviorType | 1 (RotateAndMove) | Animals/enemies wander |
| hp | 3 | |
| attackPower | 1 | |
| drawWeight | 1 | |

### Environment
| Field | Default | Notes |
|-------|---------|-------|
| type | 0 (Generic) | |
| lootResourceType | 0 (None) | |
| lootHpCost | 1 | |
| lootYield | 1 | |
| hp | 5 | |

## Enum Integer Mappings

Use these when setting enum fields in .asset YAML:

**BuildingType**: Generic=0, House=1, Shop=2, Workshop=3, Storage=4, Civic=5, Military=6, Religious=7

**ProductionOutputType**: None=0, Worker=1, Currency=2, RandomBuilding=3

**WorkerType**: Generic=0, Villager=1, Farmer=2, Miner=3, Builder=4, Merchant=5, Guard=6, Crafter=7

**GameUnitType**: Generic=0, Villager=1, Farmer=2, Miner=3, Builder=4, Merchant=5, Guard=6, Crafter=7, Soldier=8, Archer=9, Beast=10, Boss=11

**EnvironmentType**: Generic=0, Tree=1, Rock=2, Water=3, Path=4, Fence=5, Terrain=6, Flora=7

**BehaviorType**: RotateAndInteract=0, RotateAndMove=1, RotateRotateMove=2

**ResourceType**: None=0, Gold=1, Wood=2, Flowers=3, Stone=4, Water=5, Food=6 (check CurrencyDatabase for full list)

## Verification

After completing all 4 steps, verify:
1. Re-read the sheet to confirm the row was added correctly
2. Confirm the .asset file parses as valid YAML (no broken indentation)
3. Summarize everything that was done to the user
