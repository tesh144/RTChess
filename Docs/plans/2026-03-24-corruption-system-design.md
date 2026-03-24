# Corruption System Design

**Date:** 2026-03-24
**Status:** Approved
**Trello Card:** #22 — Corruption System

## Overview

Corruption hearts are hostile map entities that create two threats: corruption overlays on tiles and spawned spike units. Multiple hearts can exist per map, controlled by MapGeneratorV2 spawn count.

## Entities

**Corruption Heart** — placed by MapGeneratorV2. Each heart:
- Starts dormant, activates when player reveals tiles within activation radius
- Spreads corruption overlays onto adjacent tiles on a timer (existing CorruptionManager system)
- Spawns spike units on nearby empty cells over time
- Has its own HP; destroying it clears its corruption overlay cluster
- Quantity per map controlled by spawn count in MapGeneratorV2 Inspector

**Spikes** — regular hostile units living in UnitDatabase (synced from Workers & Entities sheet). Hearts spawn them, but they're otherwise normal units: own HP, attack power, behavior. Workers fight them like any other hostile.

**Corruption Overlay** — runtime layer on tiles. Has its own HP (configured on CorruptionManager). Pauses building production on corrupted tiles. Workers must destroy overlay HP before interacting with underlying tile content.

## Data Architecture

- Remove `CorruptionDatabase`, `CorruptionData`, `CreateCorruptionDatabase.cs`
- Spikes live in `UnitDatabase` as "Corruption" type entries, synced from Google Sheet
- Heart stats (HP, attack) stay on the `CorruptionHeart` prefab or are configured via MapGeneratorV2 spawn settings
- Global corruption settings (spread interval, activation radius, overlay HP) remain on `CorruptionManager` Inspector fields

## Spike Spawning (new behavior on CorruptionHeart)

- Heart periodically spawns spike units from UnitDatabase entries tagged as "Corruption" type
- Spikes are placed on empty adjacent cells around the heart
- Spawn rate configurable (e.g., every N spread ticks, spawn a spike)
- Spikes are standalone units — if the heart dies, existing spikes remain alive

## Existing Systems (no changes needed)

- `CorruptionManager` — spread ticking, heart registration, tile corruption/clearing, fog activation
- `CorruptionOverlay` — tile overlay HP, building pause/resume, visual placeholder
- `CorruptionHeart` — dormancy, activation, health, death clears cluster
- `GridEntityActor` — workers target corruption overlay HP before underlying entities
- `BuildingProductionManager` — PauseBuilding/ResumeBuilding API
- `MapGeneratorV2` — corruption placement (scattered/clustered/edge modes)
