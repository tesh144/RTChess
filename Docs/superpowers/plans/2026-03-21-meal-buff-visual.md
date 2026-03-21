# Meal Buff Visual Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a food-icon arc from Feast to worker on buff grant, and a golden-white particle aura on the worker while the buff is active, flickering in the final 3 ticks.

**Architecture:** Two new files (`IconFlyFX` — screen-space icon arc singleton, `MealBuffVisual` — self-cleaning aura component), one field added to `MealBuffSource`, one property + two lines added to `GridEntityActor`. No new assets required.

**Tech Stack:** Unity 2022.3, C#, `UnityEngine.UI` (Canvas/Image), `Unlit/Color` shader, `IntervalTimer` event system.

**Spec:** `Docs/superpowers/specs/2026-03-21-meal-buff-visual-design.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs` | Screen-space world-to-world icon arc singleton |
| Create | `Assets/Scripts/LittleCafe/MealBuffVisual.cs` | Particle aura component + `MealBuffParticle` helper class |
| Modify | `Assets/Scripts/LittleCafe/MealBuffSource.cs` | Add `public Sprite icon` field |
| Modify | `Assets/Scripts/LittleCafe/GridEntityActor.cs` | Add `MealBuffTicksRemaining` property; add visual calls at buff grant site |

---

## Task 1: Add `MealBuffTicksRemaining` property to `GridEntityActor`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/GridEntityActor.cs` (~line 92)

`mealBuffTicksRemaining` is currently private. `MealBuffVisual` needs to read it for the flicker check. Add a public read-only property alongside the existing `HasMealBuff` property.

- [ ] **Open** `Assets/Scripts/LittleCafe/GridEntityActor.cs`. Find the public accessors section — look for `public bool HasMealBuff => hasMealBuff;` (~line 92).

- [ ] **Add** the following property directly after `HasMealBuff`:

```csharp
/// <summary>Number of interval ticks remaining on the meal buff.</summary>
public int MealBuffTicksRemaining => mealBuffTicksRemaining;
```

- [ ] **Verify** Unity console shows zero compile errors. No Play Mode needed yet.

- [ ] **Commit:**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "feat: expose MealBuffTicksRemaining property on GridEntityActor"
```

---

## Task 2: Add `icon` field to `MealBuffSource`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/MealBuffSource.cs`

- [ ] **Replace** the entire contents of `Assets/Scripts/LittleCafe/MealBuffSource.cs` with:

```csharp
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Marker component attached to placed Meal objects.
    /// Workers that interact with a MealBuffSource receive a temporary meal buff.
    /// Workers with an active buff skip meals during target scanning.
    /// </summary>
    public class MealBuffSource : MonoBehaviour
    {
        /// <summary>
        /// Icon sprite shown flying from this Feast to a worker when the buff is granted.
        /// Assign the food/meat sprite in the Inspector on the Feast prefab.
        /// </summary>
        public Sprite icon;

        // Buff duration and effects are managed on GridEntityActor.
    }
}
```

- [ ] **Verify** Unity console shows zero compile errors.

- [ ] **Commit:**

```bash
git add Assets/Scripts/LittleCafe/MealBuffSource.cs
git commit -m "feat: add icon field to MealBuffSource for buff visual transfer"
```

---

## Task 3: Create `IconFlyFX`

**Files:**
- Create: `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs`

Scene-placed singleton (same pattern as `ResourceLootFX`). Spawns a single UI `Image` that arcs from a world position to another world position via a coroutine.

- [ ] **Create** `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs` with the following content:

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ClockworkCraft
{
    /// <summary>
    /// Spawns a single icon that arcs in screen-space from one world position to another.
    /// General-purpose "unit picks up item from world" visual — used for meal buff and
    /// future world pickups.
    ///
    /// Scene-placed singleton. Add as a component to a manager object alongside ResourceLootFX.
    /// </summary>
    public class IconFlyFX : MonoBehaviour
    {
        public static IconFlyFX Instance { get; private set; }

        [Header("Arc Settings")]
        [Tooltip("Height of the arc curve in screen pixels.")]
        public float arcHeight = 60f;
        [Tooltip("Duration of the pop-in phase.")]
        public float popInDuration = 0.15f;
        [Tooltip("Duration of the arc travel phase.")]
        public float arcDuration = 0.4f;
        [Tooltip("Size of the icon in screen pixels.")]
        public float iconSize = 56f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera mainCamera;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
            mainCamera = Camera.main;
        }

        /// <summary>
        /// Spawn an icon arc from worldFrom to worldTo.
        /// No-op if the canvas or camera is not available.
        /// </summary>
        public void SpawnArc(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
        {
            if (canvas == null || mainCamera == null || icon == null) return;
            StartCoroutine(ArcCoroutine(icon, worldFrom, worldTo));
        }

        private IEnumerator ArcCoroutine(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
        {
            // ── Create icon GameObject ──────────────────────────────────────
            GameObject iconObj = new GameObject("MealBuffIconFly");
            iconObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            Image img = iconObj.AddComponent<Image>();
            img.sprite = icon;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Sorting: child Canvas so this renders above other UI regardless of canvas mode
            Canvas iconCanvas = iconObj.AddComponent<Canvas>();
            iconCanvas.overrideSorting = true;
            iconCanvas.sortingOrder = 100;

            // ── Phase 1: Pop-in ─────────────────────────────────────────────
            // Snapshot worldFrom to screen space once for the pop-in phase.
            Vector3 startScreen = mainCamera.WorldToScreenPoint(worldFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, startScreen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 startLocal);

            rect.anchoredPosition = startLocal;
            rect.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < popInDuration)
            {
                if (iconObj == null) yield break;
                float t = elapsed / popInDuration;
                float eased = 1f - Mathf.Pow(1f - t, 3f); // cubic ease-out
                rect.localScale = Vector3.one * eased;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (iconObj == null) yield break;
            rect.localScale = Vector3.one;

            // ── Phase 2: Arc ────────────────────────────────────────────────
            elapsed = 0f;
            while (elapsed < arcDuration)
            {
                if (iconObj == null) yield break;
                float t = elapsed / arcDuration;

                // Re-convert each frame so the arc tracks camera movement
                Vector3 fromScreen = mainCamera.WorldToScreenPoint(worldFrom);
                Vector3 toScreen   = mainCamera.WorldToScreenPoint(worldTo);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, fromScreen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    out Vector2 fromLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, toScreen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    out Vector2 toLocal);

                Vector2 pos = Vector2.Lerp(fromLocal, toLocal, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                rect.anchoredPosition = pos;

                // Shrink in the final 20%
                if (t > 0.8f)
                {
                    float shrinkT = (t - 0.8f) / 0.2f;
                    rect.localScale = Vector3.one * (1f - shrinkT);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (iconObj != null)
                Destroy(iconObj);
        }
    }
}
```

- [ ] **Verify** Unity console shows zero compile errors.

- [ ] **Commit:**

```bash
git add "Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs"
git commit -m "feat: add IconFlyFX world-to-world icon arc singleton"
```

---

## Task 4: Create `MealBuffVisual`

**Files:**
- Create: `Assets/Scripts/LittleCafe/MealBuffVisual.cs`

Self-contained aura component. Spawns golden-white upward-drifting spheres while the buff is active. Flickers in the last 3 ticks. Self-destructs (component only, not the worker) when `HasMealBuff` becomes false.

`MealBuffParticle` is a helper class in the same file — same pattern as `StarvationCountdownPopup` at the bottom of `GridEntityActor.cs`.

- [ ] **Create** `Assets/Scripts/LittleCafe/MealBuffVisual.cs` with the following content:

```csharp
using System.Collections;
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Continuous particle aura attached to a worker while they have a meal buff active.
    /// Spawns golden-white spheres that drift upward. Flickers in the last 3 ticks.
    /// Self-destructs (component only) when the buff expires.
    ///
    /// Added at runtime by GridEntityActor when GrantMealBuff fires.
    /// Reads buff state directly from the sibling GridEntityActor component.
    /// </summary>
    public class MealBuffVisual : MonoBehaviour
    {
        // ── Tuning constants ───────────────────────────────────────────────
        private const float NORMAL_INTERVAL  = 0.667f;  // seconds between spawns (~1.5/sec)
        private const float FLICKER_INTERVAL = 0.222f;  // seconds between spawns (~4.5/sec)
        private const float NORMAL_LIFETIME  = 1.2f;    // particle lifetime in normal mode
        private const float FLICKER_LIFETIME = 0.6f;    // particle lifetime in flicker mode
        private const float PARTICLE_RADIUS  = 0.35f;   // max XZ offset from worker position
        private static readonly Color BUFF_COLOR = new Color(1f, 0.92f, 0.45f);

        // ── State ──────────────────────────────────────────────────────────
        private GridEntityActor actor;
        private bool isFlickering = false;  // one-way latch; set when ticksRemaining <= 3
        private bool isExpiring   = false;  // set when HasMealBuff becomes false
        private float timeSinceLastSpawn = 0f;

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Start()
        {
            actor = GetComponent<GridEntityActor>();
            if (actor == null)
            {
                Debug.LogWarning("[MealBuffVisual] No GridEntityActor on same GameObject — removing self.");
                Destroy(this);
                return;
            }

            if (IntervalTimer.Instance == null)
            {
                Debug.LogWarning("[MealBuffVisual] IntervalTimer.Instance is null — aura will not respond to ticks.");
                return;
            }

            IntervalTimer.Instance.OnIntervalTick += OnTick;
        }

        void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;
        }

        void Update()
        {
            if (isExpiring || actor == null) return;

            float interval = isFlickering ? FLICKER_INTERVAL : NORMAL_INTERVAL;
            timeSinceLastSpawn += Time.deltaTime;

            if (timeSinceLastSpawn >= interval)
            {
                timeSinceLastSpawn = 0f;
                SpawnParticle(isFlickering ? FLICKER_LIFETIME : NORMAL_LIFETIME);
            }
        }

        // ── Tick handler ───────────────────────────────────────────────────

        private void OnTick(int intervalCount)
        {
            if (actor == null) return;

            // Expiry check first — takes priority over flicker
            if (!actor.HasMealBuff && !isExpiring)
            {
                isExpiring = true;
                StartCoroutine(ExpireAfterDelay());
                return;
            }

            // Flicker check (only while not expiring)
            if (!isExpiring && actor.MealBuffTicksRemaining <= 3 && !isFlickering)
            {
                isFlickering = true;
                timeSinceLastSpawn = 0f; // spawn a flicker particle immediately on next Update
            }
        }

        private IEnumerator ExpireAfterDelay()
        {
            // Wait for in-flight particles to finish their longest possible lifetime
            yield return new WaitForSeconds(NORMAL_LIFETIME);
            Destroy(this); // component only — worker GameObject is unaffected
        }

        // ── Particle spawn ─────────────────────────────────────────────────

        private void SpawnParticle(float lifetime)
        {
            float offsetX = Random.Range(-PARTICLE_RADIUS, PARTICLE_RADIUS);
            float offsetZ = Random.Range(-PARTICLE_RADIUS, PARTICLE_RADIUS);
            Vector3 spawnPos = transform.position + new Vector3(offsetX, 0f, offsetZ);

            float size = Random.Range(0.05f, 0.10f);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MealBuffParticle";
            sphere.transform.position = spawnPos;
            sphere.transform.localScale = Vector3.one * size;

            // Remove collider — we don't need physics
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Unlit material in buff color
            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = BUFF_COLOR;
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            // Attach the particle animator
            MealBuffParticle particle = sphere.AddComponent<MealBuffParticle>();
            particle.Initialize(lifetime, size);
        }
    }

    /// <summary>
    /// Animates a single meal buff aura sphere: drifts upward, shrinks to zero, self-destructs.
    /// Parented to scene root (not the worker) so it persists through worker destruction.
    /// </summary>
    public class MealBuffParticle : MonoBehaviour
    {
        private float lifetime;
        private float startSize;
        private float elapsed;

        private const float DRIFT_SPEED = 0.6f; // units/sec upward

        public void Initialize(float lifetime, float startSize)
        {
            this.lifetime  = lifetime;
            this.startSize = startSize;
            this.elapsed   = 0f;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Drift upward
            transform.position += Vector3.up * DRIFT_SPEED * Time.deltaTime;

            // Quadratic ease-in shrink: holds size briefly then shrinks fast
            float scaleT = t * t;
            float currentSize = startSize * (1f - scaleT);
            transform.localScale = Vector3.one * Mathf.Max(currentSize, 0f);
        }
    }
}
```

- [ ] **Verify** Unity console shows zero compile errors.

- [ ] **Commit:**

```bash
git add Assets/Scripts/LittleCafe/MealBuffVisual.cs
git commit -m "feat: add MealBuffVisual particle aura component"
```

---

## Task 5: Wire up visual calls in `GridEntityActor.ScanAndInteract`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/GridEntityActor.cs` (~line 691)

- [ ] **Open** `Assets/Scripts/LittleCafe/GridEntityActor.cs`. Search for the comment `// Grant meal buff if target is a MealBuffSource` (around line 691). The block looks like:

```csharp
// Grant meal buff if target is a MealBuffSource
MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
if (mealSource != null && !hasMealBuff)
{
    GrantMealBuff(8); // 8 interval ticks
    if (verboseLogging)
        Debug.Log($"[GridEntityActor] {gameObject.name} received meal buff ({mealBuffTicksRemaining} ticks)");
}
```

- [ ] **Replace** that block with:

```csharp
// Grant meal buff if target is a MealBuffSource
MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
if (mealSource != null && !hasMealBuff)
{
    GrantMealBuff(8); // 8 interval ticks

    // Visual: food icon arcs from feast to this worker
    if (mealSource.icon != null)
        ClockworkCraft.IconFlyFX.Instance?.SpawnArc(mealSource.icon, target.transform.position, transform.position);

    // Visual: attach particle aura (guard prevents duplicate on re-grant)
    if (GetComponent<MealBuffVisual>() == null)
        gameObject.AddComponent<MealBuffVisual>();

    if (verboseLogging)
        Debug.Log($"[GridEntityActor] {gameObject.name} received meal buff ({mealBuffTicksRemaining} ticks)");
}
```

- [ ] **Verify** Unity console shows zero compile errors.

- [ ] **Commit:**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "feat: wire up MealBuffVisual and IconFlyFX on meal buff grant"
```

---

## Task 6: Human Setup in Unity Editor

These steps must be done manually in the Unity Editor. **No code changes required.**

- [ ] **Add `IconFlyFX` to the scene.** In the Hierarchy, find the manager object that hosts `ResourceLootFX`. Select it. In the Inspector, click **Add Component** → search `IconFlyFX` → add it. Verify the component appears with default values (`arcHeight=60`, `popInDuration=0.15`, etc.).

- [ ] **Assign the food sprite to the Feast prefab.** In the Project window, find the Feast prefab (the `MealBuffSource`-bearing object). Open it in Prefab Edit mode. Select the root GameObject. In the Inspector, find the `Meal Buff Source` component → `Icon` field. Drag the food/meat sprite (from the animal kill loot system) into the field. Save the prefab.

- [ ] **Play-test — icon arc.** Enter Play Mode. Place a Worker and a Feast. Wait for the Worker to interact with the Feast. Verify: a food icon pops up at the Feast position and arcs to the Worker in ~0.55s.

- [ ] **Play-test — aura.** After the icon arc, verify: small golden-white spheres begin drifting upward around the Worker.

- [ ] **Play-test — flicker.** Wait for `mealBuffTicksRemaining` to reach 3 (watch the `verboseLogging` output, or enable verbose logging on the Worker's `GridEntityActor`). Verify: particle spawn rate visibly increases and particles disappear faster.

- [ ] **Play-test — expiry.** Wait for the buff to expire (after 8 ticks). Verify: aura stops, remaining in-flight particles finish and disappear, Worker continues functioning normally (not destroyed).

- [ ] **Tune parameters (optional).** If the arc feels too flat, increase `arcHeight` on `IconFlyFX`. If the aura is too subtle or too noisy, adjust `NORMAL_INTERVAL` / `FLICKER_INTERVAL` / particle size constants in `MealBuffVisual.cs`.

- [ ] **Commit scene changes:**

```bash
git add "Assets/Scenes/Auto RTS Main.unity" "Assets/Scenes/Auto RTS Main.unity.meta"
git commit -m "feat: complete meal buff visual — scene setup"
```
