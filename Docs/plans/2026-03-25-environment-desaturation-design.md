# Environment Desaturation — Design Document

**Date:** 2026-03-25
**Card:** #128 "Greyed-out environment objects until interacted with"
**Status:** Approved

## Summary

Environment objects (trees, rocks, goldmines, water) appear 50% desaturated by default. On first worker hit, they transition to full colour over 0.3 seconds. This reduces visual clutter while keeping vibrancy where action is happening.

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Trigger | First TakeDamage call | Clean cause-and-effect — colour blooms where workers are actively working |
| Persistence | Permanent | Once coloured, stays coloured. No fade-back tracking needed |
| Transition | 0.3s saturation fade | Quick enough to feel responsive, slow enough to be noticeable |
| Technique | MaterialPropertyBlock + shader `_Saturation` | Zero material allocations, preserves GPU batching, per-instance control |
| Saturation level | 0.5 (50% desaturated) | Per card spec — not full greyscale, just muted |

## New Component: EnvironmentDesaturation.cs

**Namespace:** ClockworkCraft
**Location:** `Assets/ClockworkCraft/Scripts/World/EnvironmentDesaturation.cs`

### Responsibilities

1. On `Start()`: find all `Renderer` children, create a `MaterialPropertyBlock`, set `_Saturation = 0.5` on every renderer
2. `Colorize()`: public method that lerps `_Saturation` from 0.5 to 1.0 over 0.3s via coroutine
3. `hasColorized` bool prevents re-triggering

### Attachment

Added by `GridEntityManager.AttachFromEnvironmentData()` — same place all environment entity components are wired up. Only environment objects get this component.

## Shader Change

Add a `_Saturation` float property (default 1.0) to the shader used by PEPO environment prefabs. In the fragment pass, after texture sampling:

```hlsl
float grey = dot(col.rgb, float3(0.299, 0.587, 0.114));
col.rgb = lerp(float3(grey, grey, grey), col.rgb, _Saturation);
```

At `_Saturation = 0.5`: 50% desaturated. At `_Saturation = 1.0`: original colour.

MaterialPropertyBlock sets this per-renderer without creating material instances.

## Integration Point

In `GridEntityHealth.TakeDamage()`, after dealing damage:

```csharp
GetComponent<EnvironmentDesaturation>()?.Colorize();
```

Single GetComponent call per hit. The component's `hasColorized` bool short-circuits on subsequent hits.

## Scope — What's NOT Affected

- Allied buildings, workers, units — only environment objects
- Corruption overlays/spikes — not environment type
- Objects start desaturated regardless of fog state (desaturation is about interaction, not visibility)

## Files Modified

| File | Change |
|---|---|
| `Assets/ClockworkCraft/Scripts/World/EnvironmentDesaturation.cs` | New component |
| `Assets/Scripts/LittleCafe/GridEntityManager.cs` | Add component in `AttachFromEnvironmentData()` |
| `Assets/Scripts/LittleCafe/GridEntityHealth.cs` | Call `Colorize()` in `TakeDamage()` |
| `Assets/Shaders/UnlitSaturation.shader` | New custom shader replacing built-in `Unlit/Texture` — adds `_Saturation` property |
| PEPO environment materials | Swap from `Unlit/Texture` to `Custom/UnlitSaturation` (same look, adds saturation control) |
