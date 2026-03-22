# New Object Pipeline — Design Document

**Date:** 2026-03-21
**Status:** Approved
**Trello Card:** #28 — New Object Pipeline (Streamlined Content Creation)

## Goal

Create a `/new-object` Claude skill that automates adding new game objects (buildings, workers/units, environment) to ClockworkCraft. The pipeline handles Google Sheets data, C# enum updates, Unity ScriptableObject .asset entries, and Trello tracking in a single automated flow.

The target experience: say "add a Kitchen building that costs 10 meat" and it exists in the data layer immediately, with a Trello card tracking what still needs human attention (prefab, icon, balancing).

## Approach

**Custom Claude Skill** — a `/new-object` skill triggered conversationally. Claude gathers input via structured AskUserQuestion prompts, then executes 4 automated steps. No Unity Editor tooling required; Claude directly modifies sheets, code files, and YAML .asset files.

## Skill Input Flow

The skill asks 4 questions via AskUserQuestion:

1. **Object type** (multiple choice): Building, Worker/Unit, or Environment
2. **Name** (free text): e.g. "Kitchen", "Corruption Heart", "Fighter"
3. **Key properties** (varies by type):
   - Buildings: production behavior (auto-produce, input-triggered, none), resource costs if known
   - Workers/Units: behavior type (RotateAndInteract, RotateAndMove, RotateRotateMove), HP/attack if known
   - Environment: loot resource type, HP, whether it's harvestable
4. **Anything else?** (open-ended): Special behavior notes, related systems, etc.

Everything not explicitly specified gets defaults or is left blank (minimal skeleton approach).

## Pipeline Execution Steps

### Step 1 — Google Sheets

Add a minimal skeleton row to the appropriate sheet in spreadsheet `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`:

- **Buildings** → "Buildings & Production" sheet (columns: Icon, Building, Prod. Interval, Interval Bonus, Input, Output, Output Amt, HP, Attack)
- **Workers/Units** → "Workers & Entities" sheet (columns: Entity, Type, HP, Attack Power, Behavior, Grid Size, Draw Weight, Source File)
- **Environment** → "Environment & Loot" sheet (columns vary — check at runtime)

If placement cost values were specified, also add a Placement Costs column section mirroring the nearest existing tier structure. If not specified, skip — Trello card notes it as a TODO.

### Step 2 — C# Enums (if needed)

Check whether the object introduces a new type value not present in existing enums:

- `Assets/Scripts/Data/BuildingData.cs` → BuildingType enum, ProductionOutputType enum
- `Assets/Scripts/Data/WorkerData.cs` → WorkerType enum
- `Assets/Scripts/Data/EnvironmentData.cs` → EnvironmentType enum (if applicable)

If the new object's type already exists in the enum, skip. If not, append the new value before the closing brace.

### Step 3 — ScriptableObject .asset File

Append a new YAML entry to the correct database:

- **Buildings** → `Assets/Scripts/Data/BuildingDatabase.asset` (buildingList)
- **Workers/Units** → `Assets/Scripts/Data/WorkerDatabase.asset` (workerList)
- **Environment** → `Assets/Scripts/Data/EnvironmentDatabase.asset` (environmentList)

Prefab and icon fields are set to `{fileID: 0}` (null reference). All other fields match the sheet data or use defaults. The YAML format must exactly match Unity's serialization style (2-space indent, field order matching existing entries).

### Step 4 — Trello Card

Create a card in **Ready for Review** (list ID: `69bd0cb4a2c3159d3cd6b111`) on the Auto RTS board (ID: `69bd0b7483af459744b7a24c`).

Card includes:
- Object name and type
- Which files were modified (sheets, .cs, .asset)
- Values that were set
- Checklist of what still needs human attention:
  - [ ] Assign prefab in Unity Inspector
  - [ ] Assign icon sprite
  - [ ] Set placement costs (if not done)
  - [ ] Tune balance values
  - [ ] Test in-game

Labels: Feature + System + any type-specific labels.

## Default Values

### Buildings
| Field | Default |
|-------|---------|
| type | Generic (0) |
| isFunctional | false |
| isWalkable | false |
| isActive | false |
| hp | 10 |
| attackPower | 0 |
| drawWeight | 1 |
| gridSize | 1x1 |
| visualScale | 1.0 |
| productionInterval | 0 |
| productionIntervalBonus | 0 |
| productionOutputType | None (0) |
| productionAmount | 1 |

### Workers/Units
| Field | Default |
|-------|---------|
| type | Generic (0) |
| isFunctional | false |
| isWalkable | true |
| isActive | true |
| behaviorType | RotateAndInteract (0) |
| hp | 5 |
| attackPower | 1 |
| drawWeight | 0 |
| gridSize | 1x1 |
| visualScale | 1.0 |

### Environment
| Field | Default |
|-------|---------|
| type | Generic (0) |
| isFunctional | false |
| isWalkable | false |
| isActive | false |
| lootResourceType | None (0) |
| lootHpCost | 1 |
| lootYield | 1 |
| hp | 5 |
| attackPower | 0 |
| gridSize | 1x1 |
| visualScale | 1.0 |

## File References

### Google Sheets
- Spreadsheet: `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`
- Sheets: Buildings & Production, Workers & Entities, Environment & Loot, Placement Costs

### Unity Assets
- `Assets/Scripts/Data/BuildingDatabase.asset`
- `Assets/Scripts/Data/WorkerDatabase.asset`
- `Assets/Scripts/Data/EnvironmentDatabase.asset`
- `Assets/Scripts/Data/UnitDatabase.asset`

### C# Data Files
- `Assets/Scripts/Data/BuildingData.cs` (BuildingType, ProductionOutputType)
- `Assets/Scripts/Data/WorkerData.cs` (WorkerType)
- `Assets/Scripts/Data/EnvironmentData.cs`

### Trello
- Board: `69bd0b7483af459744b7a24c` (Auto RTS)
- Ready for Review list: `69bd0cb4a2c3159d3cd6b111`
- Label IDs: Feature `69bd0b7583af459744b7a266`, System `69be3cf15271604d16b71e3c`
