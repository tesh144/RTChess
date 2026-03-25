# Corruption Fog Visual — Design Spec

**Date:** 2026-03-24
**Project:** RTChess / Auto RTS
**Status:** Approved

---

## Overview

Replace the static placeholder purple quad in `CorruptionOverlay.SpawnVisual()` with a code-driven Unity `ParticleSystem` that renders as a low-lying purple fog blanketing each corrupted tile. No prefab assets or textures required — everything is generated at runtime.

---

## Render Pipeline

RTChess uses the **Built-in Render Pipeline** (`m_CustomRenderPipeline: {fileID: 0}` in `ProjectSettings/GraphicsSettings.asset`). The `"Particles/Standard Unlit"` shader is available and should be used — no fallback required.

---

## Scope

**Single file:** `Assets/Scripts/LittleCafe/CorruptionOverlay.cs`

Only the `else` branch of `SpawnVisual()` (the no-prefab fallback) is changed. The prefab path (`if (prefab != null)`) is untouched. No other files, scenes, or assets are modified.

---

## New Private Fields

Add two owned fields to `CorruptionOverlay`:

```csharp
private Material _fogMaterial;   // created in SpawnVisual(), destroyed in Cleanup()
private Texture2D _fogTexture;   // created in SpawnVisual(), destroyed in Cleanup()
```

Both must be destroyed in `Cleanup()` (which is always called before `Destroy(overlay)`) to avoid material/texture memory leaks. This also fixes an existing leak: the current placeholder creates a `new Material(...)` and never destroys it.

---

## SpawnVisual() — Placeholder Branch

Replace the existing `else` block with the following logic. All steps execute once on `Start()`.

### Step 1 — Generate soft-circle texture

Create a 32×32 `Texture2D` in memory with a radial alpha gradient: centre pixel is fully opaque, edge pixels are fully transparent, falloff is quadratic (`alpha = (1 - dist/radius)²`). RGB is white throughout — colour is applied via the particle system's start colour.

```csharp
_fogTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
Vector2 center = new Vector2(15.5f, 15.5f);  // true centre of a 32×32 texture (pixels 0–31)
for (int px = 0; px < 32; px++)
{
    for (int py = 0; py < 32; py++)
    {
        float dist = Vector2.Distance(new Vector2(px, py), center);
        float t = Mathf.Clamp01(1f - dist / 16f);
        float alpha = t * t;
        _fogTexture.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
    }
}
_fogTexture.Apply();
```

### Step 2 — Create material

Use `"Particles/Standard Unlit"` (always available in the Built-in pipeline). Set blend mode to alpha-blended so particles are transparent.

```csharp
var sh = Shader.Find("Particles/Standard Unlit");
if (sh == null)
{
    Debug.LogError("[CorruptionOverlay] Shader 'Particles/Standard Unlit' not found.");
    return;
}
_fogMaterial = new Material(sh);
_fogMaterial.mainTexture = _fogTexture;
_fogMaterial.SetFloat("_Mode", 2);  // Fade blend mode
_fogMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
_fogMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
_fogMaterial.SetInt("_ZWrite", 0);
_fogMaterial.EnableKeyword("_ALPHABLEND_ON");
_fogMaterial.renderQueue = 3000;
```

### Step 3 — Create child GameObject and ParticleSystem

```csharp
visualChild = new GameObject("CorruptionFog");
visualChild.transform.SetParent(transform);
visualChild.transform.localPosition = new Vector3(0f, 0.05f, 0f);
visualChild.transform.localRotation = Quaternion.identity;
visualChild.transform.localScale    = Vector3.one;

var ps = visualChild.AddComponent<ParticleSystem>();
```

### Step 4 — Configure ParticleSystem modules

All configuration is via the module structs on `ps`. **Call `ps.Play()` explicitly at the end** — a newly created `ParticleSystem` starts in a stopped state and will not emit without it.

#### Main module

```csharp
var main = ps.main;
main.duration        = 3f;
main.loop            = true;
main.startLifetime   = 2.5f;
main.startSpeed      = 0.04f;   // slow upward drift
main.startSize       = 0.45f;   // covers roughly half a tile
main.maxParticles    = 60;
main.simulationSpace = ParticleSystemSimulationSpace.Local;
main.gravityModifier = 0f;
// Two-colour startColor — Unity picks randomly between A and B per particle for subtle hue variation
main.startColor = new ParticleSystem.MinMaxGradient(
    new Color(0.45f, 0f, 0.75f, 0.5f),   // colour A
    new Color(0.60f, 0f, 0.85f, 0.5f)    // colour B
);
```

#### Emission module

```csharp
var emission = ps.emission;
emission.enabled = true;
emission.rateOverTime = 20f;
```

#### Shape module

```csharp
var shape = ps.shape;
shape.enabled   = true;
shape.shapeType = ParticleSystemShapeType.Box;
shape.scale     = new Vector3(0.85f, 0.05f, 0.85f);  // flat box covering tile footprint
```

#### Colour over lifetime module

Fade in from transparent → opaque → transparent. Use a `Gradient` with `alphaKeys`, wrapped in a `MinMaxGradient`:

```csharp
var gradient = new Gradient();
gradient.alphaKeys = new GradientAlphaKey[]
{
    new GradientAlphaKey(0f, 0.0f),
    new GradientAlphaKey(1f, 0.2f),
    new GradientAlphaKey(1f, 0.8f),
    new GradientAlphaKey(0f, 1.0f),
};
gradient.colorKeys = new GradientColorKey[]
{
    new GradientColorKey(Color.white, 0f),
    new GradientColorKey(Color.white, 1f),
};

var col = ps.colorOverLifetime;
col.enabled = true;
col.color   = new ParticleSystem.MinMaxGradient(gradient);
```

#### Size over lifetime module

Use a `MinMaxCurve` in `Curve` mode — expands from 0.8× to 1.0× at mid-life, then shrinks to 0.6× for a subtle dissipation feel:

```csharp
var sizeCurve = new AnimationCurve(
    new Keyframe(0f,   0.8f),
    new Keyframe(0.5f, 1.0f),
    new Keyframe(1f,   0.6f)
);

var size = ps.sizeOverLifetime;
size.enabled = true;
size.size    = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
```

#### Renderer module

Retrieve via `GetComponent<ParticleSystemRenderer>()` — it is added automatically alongside `ParticleSystem` and is always present:

```csharp
var psr = visualChild.GetComponent<ParticleSystemRenderer>();
psr.material        = _fogMaterial;
psr.renderMode      = ParticleSystemRenderMode.Billboard;
psr.sortingOrder    = 10;
psr.sortingLayerName = "Default";
```

#### Start playback

```csharp
visualChild.SetActive(true);  // ensure active before playback (consistent with prefab path)
ps.Play();
```

`ps.Play()` must be the last call after all modules are configured. `visualChild` is active by default as a newly created GameObject, but `SetActive(true)` is called explicitly for consistency with the prefab branch of `SpawnVisual()`.

---

## Cleanup() Changes

Before the existing `Destroy(visualChild)` call, add:

```csharp
if (_fogMaterial != null) { Destroy(_fogMaterial); _fogMaterial = null; }
if (_fogTexture  != null) { Destroy(_fogTexture);  _fogTexture  = null; }
```

---

## Conventions Followed

- `Shader.Find()` called in `Start()` (via `SpawnVisual()`) — not per-frame
- `new Material()` and `new Texture2D()` destroyed in `Cleanup()` to avoid memory leaks
- `Destroy(this)` pattern not applicable here — this component is destroyed by `CorruptionManager.ClearTile()` which calls `Cleanup()` first then `Destroy(overlay)`
- No `static` fields — each overlay instance owns its own material and texture

---

## Out of Scope

- Spike visual (deferred)
- Corruption heart visual upgrade (deferred)
- Prefab-based visual path (untouched)
- Performance optimisation (e.g. shared material across tiles) — not needed at current corruption scale; noted as a future improvement if tile counts grow large
