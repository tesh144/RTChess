# Jai's AI Agent Sync Log

> **Owner:** Jai
> **Purpose:** Keeps all AI agents (Claude Code, Claude Co-Work, etc.) aligned on what's happening across sessions. Every agent must read this at session start and update it when making changes.

---

## Agent Rules

1. **Read this file at session start** before doing any work.
2. **Update "Active Work" when you start a task** — note the agent name and what you're doing.
3. **Update "Completed Work" when you finish** — one-line summary, move from Active.
4. **Check for conflicts** — before touching a file or system, check if another agent is already on it.
5. **Keep it concise** — one line per item. This is a coordination log, not a diary.
6. **Never delete another agent's entries** — only Jai clears this file.
7. **Archive at session end** — move entries older than 24 hours to `JAI_AI_SYNC_ARCHIVE.md`. Keep only: Active Work, today's Completed entries, unresolved Notes/Flags.

---

## Cross-Agent Requests

| From | To | Request | Status |
|------|----|---------|--------|
| Claude Code | Co-Work | **Google Sheets MCP**: No standalone MCP available for Claude Code. Workaround: Claude Code reads/writes SheetCache.json; Co-Work pushes to actual sheets. | Answered |

---

## Active Work

> **Project Status: Post-mortem complete. Paused for clean re-entry.**
> Last active session: 2026-03-29.

| Agent | Task | Status | Files |
|-------|------|--------|-------|
| Claude Code | #155 Building Bubble Migration | Paused — Insert fill bar + icon done, Collect wired. HoldToFill bubble wiring remaining (#157). | `POIBubble.cs`, `BuildingProductionManager.cs` |
| Co-Work | #159 Tile Layer System | Paused — Surface API done, CorruptionManager wired, walkableSurfaces done, buildOn synced. Remaining: water migration, EnvironmentDatabase Corruption surface entry. | `GridManager.cs`, `CorruptionManager.cs`, `GridEntityActor.cs` |
| Co-Work | #22 Corruption System | Paused — Code complete, awaiting human steps (prefab assignment, sync from database). | Trello card has human checklist |
| Will/DMoT | #63 Multi-cell placement | Paused — Architecture designed, implementation not started. | Design doc in Trello |

---

## Completed Work

| Date | Agent | Summary |
|------|-------|---------|
| 2026-03-29 | Co-Work | Project cleanup: Active Work cleared, RTChess.md + CLAUDE_USER_JAI.md updated from post-mortem, Re-Entry Protocol created. |
| 2026-03-29 | Co-Work | Skills library: 26 external game-dev skills added to RTChess/.claude/skills/. All 37 in ClaudeAI/shared-skills/. 7 planned skill cards (#165–#172) updated with skill-creator build process. JAI_AI_SYNC.md archiving system established. |

_Older entries → JAI_AI_SYNC_ARCHIVE.md_

---

## Recent Decisions

- 2026-03-28: DevCheatMenu toggles are persistent (green=ON, blue=OFF). FreeCosts + InstantProduction. Min timer = 1s, never 0.
- 2026-03-28: Tile Layer System — Object layer (IsCellEmpty) and Surface layer are fully independent. CorruptionManager is sole authority for SurfaceType.Corruption.

_Older decisions → JAI_AI_SYNC_ARCHIVE.md_

---

## Notes / Flags

_Remove when resolved._

- **PENDING (Claude Code)**: SheetSyncEditor.SyncEnvironment() column indices still shifted +1 from Icon insert. Object 2→3, Drops 3→4, etc. Full detail in archive (2026-03-26).
- **PENDING (Jai in Unity)**: POIDatabase.asset — create via Create → RTChess → POI Database. Assign to POIManager.poiDatabase. Run SyncPOI.
- **PENDING (Jai in Unity)**: POIManager Inspector — assign bubblePrefab, poiDatabase, bubbleParent (World Canvas).
- **PENDING**: Map Density Slider approved, not implemented. Plan in Trello.
- **PENDING (#173)**: FurnitureObject → PlacedObject rename blocked by Will's MapGeneratorV2. Shim approach agreed. Coordinate with Will before removing shim.
