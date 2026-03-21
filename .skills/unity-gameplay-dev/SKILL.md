---
name: unity-gameplay-dev
description: Use this skill for Unity C# gameplay programming, MonoBehaviour components, game systems, physics, coroutines, event systems, and runtime behavior. Trigger when the user wants to write or modify gameplay code, create game mechanics, implement character controllers, handle collisions, animate objects at runtime, create UI interactions, or build any game logic. Use this skill when the user mentions "MonoBehaviour", "game logic", "gameplay", "player controller", "enemy AI", "coroutines", "physics", "collisions", "game systems", or wants to implement runtime behavior in Unity.
---

# Unity Gameplay Development

Write robust C# gameplay code using Unity's patterns and lifecycle without common runtime errors.

## When to Use This Skill

- Creating MonoBehaviour components
- Implementing game mechanics and systems
- Player/enemy controllers and AI
- Physics and collision handling
- Coroutines and timed behavior
- Event-driven systems
- UI interactions and game state
- Animation integration

**Not for:** Asset creation (use unity-asset-management) or Editor automation (use unity-editor-scripting).

---

## Core Principle: MonoBehaviour Lifecycle

**All Unity gameplay failures stem from misunderstanding the MonoBehaviour lifecycle:**

```
Initialization:  Awake() → OnEnable() → Start()
Frame Loop:      FixedUpdate() → Update() → LateUpdate()
Physics:         OnCollisionEnter/Stay/Exit, OnTriggerEnter/Stay/Exit
Shutdown:        OnDisable() → OnDestroy()
```

**Golden Rules:**
1. **Awake()**: Initialize self-contained data (runs before Start, even if disabled)
2. **Start()**: Initialize with references to other objects (runs once when enabled)
3. **Update()**: Frame-rate dependent logic (input, non-physics movement)
4. **FixedUpdate()**: Physics calculations (consistent timestep)
5. **LateUpdate()**: After all Updates (camera following, final adjustments)

---

## Pattern 1: Singleton Manager

**Problem:** Need global access to managers without tight coupling.

**Solution:**
```csharp
using UnityEngine;

public class ResourceTokenManager : MonoBehaviour
{
    private static ResourceTokenManager _instance;

    public static ResourceTokenManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ResourceTokenManager>();

                if (_instance == null)
                {
                    Debug.LogError("ResourceTokenManager not found in scene!");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // Enforce singleton
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Multiple ResourceTokenManager instances found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Optional: Persist across scenes
        // DontDestroyOnLoad(gameObject);
    }

    private int tokens = 0;

    public void AddTokens(int amount)
    {
        tokens += amount;
        Debug.Log($"Tokens: {tokens}");
    }
}

// Usage from any script:
// ResourceTokenManager.Instance.AddTokens(5);
```

**Key Points:**
- Check for existing instance in Awake()
- Use property getter for lazy initialization
- Destroy duplicates to prevent conflicts
- Consider DontDestroyOnLoad() if manager should persist

---

## Pattern 2: Event-Driven System

**Problem:** Components need to communicate without tight coupling.

**Solution:**
```csharp
using UnityEngine;
using System;

// Event definition
public class IntervalTimer : MonoBehaviour
{
    public static event Action OnIntervalTick;

    private float intervalDuration = 2f;
    private float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= intervalDuration)
        {
            timer -= intervalDuration;
            OnIntervalTick?.Invoke(); // Safe null-check invoke
        }
    }
}

// Subscriber
public class Unit : MonoBehaviour
{
    private void OnEnable()
    {
        IntervalTimer.OnIntervalTick += OnIntervalTick;
    }

    private void OnDisable()
    {
        IntervalTimer.OnIntervalTick -= OnIntervalTick;
    }

    private void OnIntervalTick()
    {
        Debug.Log("Unit received interval tick!");
        // Perform interval-based action
    }
}
```

**Critical:**
- Subscribe in `OnEnable()`, unsubscribe in `OnDisable()`
- Failure to unsubscribe causes memory leaks and null reference errors
- Use `?.Invoke()` for null-safe invocation
- Static events for global systems, instance events for object-specific

---

## Pattern 3: Coroutines for Timed Behavior

**Problem:** Need to sequence actions over time without blocking.

**Solution:**
```csharp
using UnityEngine;
using System.Collections;

public class Unit : MonoBehaviour
{
    private Coroutine rotateCoroutine;

    public void RotateToFacing(Vector3 targetDirection)
    {
        // Stop previous rotation if running
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
        }

        rotateCoroutine = StartCoroutine(RotateOverTime(targetDirection, 0.25f));
    }

    private IEnumerator RotateOverTime(Vector3 targetDirection, float duration)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null; // Wait one frame
        }

        // Ensure exact final rotation
        transform.rotation = targetRotation;
        rotateCoroutine = null;
    }

    private void OnDisable()
    {
        // Stop all coroutines when disabled
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }
    }
}
```

**Key Points:**
- Store coroutine reference to stop it later
- Stop previous coroutine before starting new one
- Always stop coroutines in OnDisable() to prevent orphaned routines
- Use `yield return null` for frame-based waiting
- Use `yield return new WaitForSeconds(duration)` for time-based waiting
- Ensure final state is exact (don't rely on last interpolation step)

---

## Pattern 4: Null-Safe Component Access

**Problem:** GetComponent() returns null, causing NullReferenceException.

**Solution:**
```csharp
using UnityEngine;

public class Unit : MonoBehaviour
{
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        // Cache components in Awake
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError($"MeshRenderer not found on {gameObject.name}!", this);
        }
    }

    public void SetColor(Color color)
    {
        // Null check before use
        if (meshRenderer == null)
        {
            Debug.LogWarning($"Cannot set color: MeshRenderer is null on {gameObject.name}");
            return;
        }

        // Use sharedMaterial for asset modification (persistent)
        // Use material for runtime instance modification (temporary)
        meshRenderer.material.color = color;
    }
}

// Alternative: RequireComponent attribute
[RequireComponent(typeof(MeshRenderer))]
public class Unit : MonoBehaviour
{
    // Unity will add MeshRenderer if missing
}
```

**Best Practices:**
- Cache components in Awake() or Start()
- Always null-check before use
- Use `RequireComponent` attribute to enforce dependencies
- Use `TryGetComponent()` for optional components:
  ```csharp
  if (TryGetComponent<Rigidbody>(out var rb))
  {
      rb.AddForce(Vector3.up);
  }
  ```

---

## Pattern 5: Physics and Collision Handling

**Problem:** Inconsistent collision detection or physics behavior.

**Solution:**
```csharp
using UnityEngine;

// For solid collisions (Rigidbody + Collider, both non-trigger)
public class Unit : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // collision.gameObject is what we hit
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log($"Hit enemy: {collision.gameObject.name}");

            // Access collision point and normal
            ContactPoint contact = collision.contacts[0];
            Debug.Log($"Hit point: {contact.point}, Normal: {contact.normal}");
        }
    }
}

// For trigger zones (Collider with isTrigger = true)
public class TriggerZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered zone!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player left zone!");
        }
    }
}

// Physics movement (use in FixedUpdate!)
public class PhysicsMovement : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // Physics forces must be applied in FixedUpdate
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, 0, vertical) * 10f;
        rb.AddForce(movement);
    }
}
```

**Collision Matrix:**
- **OnCollisionEnter/Stay/Exit**: Solid collisions (both have Collider, at least one has Rigidbody, neither is trigger)
- **OnTriggerEnter/Stay/Exit**: Trigger zones (one Collider has isTrigger = true, at least one has Rigidbody)

**Physics Rules:**
1. Use `FixedUpdate()` for ALL physics calculations
2. Use `Rigidbody.AddForce()` instead of `transform.position` for physics objects
3. Use tags (`CompareTag()`) for collision filtering
4. Kinematic Rigidbody = no physics forces, but triggers collisions

---

## Common Failure Patterns

### 1. "NullReferenceException on GetComponent"
**Symptom:** Script crashes on component access.

**Diagnosis:**
```csharp
// WRONG: No null check
MeshRenderer renderer = GetComponent<MeshRenderer>();
renderer.material.color = Color.red; // CRASH if no MeshRenderer

// RIGHT: Null check
MeshRenderer renderer = GetComponent<MeshRenderer>();
if (renderer != null)
{
    renderer.material.color = Color.red;
}
else
{
    Debug.LogError("MeshRenderer not found!");
}
```

### 2. "Coroutine continues after object destroyed"
**Symptom:** Errors after scene reload or object destruction.

**Diagnosis:**
```csharp
// WRONG: No cleanup
StartCoroutine(MyCoroutine());

// RIGHT: Stop in OnDisable
private Coroutine myCoroutine;

private void Start()
{
    myCoroutine = StartCoroutine(MyCoroutine());
}

private void OnDisable()
{
    if (myCoroutine != null)
    {
        StopCoroutine(myCoroutine);
        myCoroutine = null;
    }
}
```

### 3. "Event still subscribed after destroy"
**Symptom:** Errors when event fires after object destroyed.

**Diagnosis:**
```csharp
// WRONG: Subscribe but never unsubscribe
private void Start()
{
    GameManager.OnGameOver += HandleGameOver;
}

// RIGHT: Unsubscribe in OnDisable
private void OnEnable()
{
    GameManager.OnGameOver += HandleGameOver;
}

private void OnDisable()
{
    GameManager.OnGameOver -= HandleGameOver;
}
```

### 4. "Physics inconsistent or jittery"
**Symptom:** Movement stutters or behaves unpredictably.

**Diagnosis:**
```csharp
// WRONG: Physics in Update (frame-rate dependent)
private void Update()
{
    rb.AddForce(Vector3.forward);
}

// RIGHT: Physics in FixedUpdate (consistent timestep)
private void FixedUpdate()
{
    rb.AddForce(Vector3.forward);
}
```

### 5. "Singleton duplicates on scene reload"
**Symptom:** Multiple instances of singleton manager.

**Diagnosis:**
```csharp
// WRONG: No duplicate check
private void Awake()
{
    _instance = this;
    DontDestroyOnLoad(gameObject);
}

// RIGHT: Enforce singleton
private void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(gameObject);
        return;
    }

    _instance = this;
    DontDestroyOnLoad(gameObject);
}
```

---

## Unity API Quick Reference

### Transform
```csharp
transform.position                    // World position
transform.localPosition               // Position relative to parent
transform.rotation                    // World rotation (Quaternion)
transform.localRotation               // Local rotation
transform.eulerAngles                 // World rotation (Euler angles)
transform.localScale                  // Scale
transform.parent                      // Parent transform
transform.Find("ChildName")           // Find child by name
```

### GameObject
```csharp
gameObject.SetActive(true/false)      // Enable/disable GameObject
gameObject.tag                        // Tag string
gameObject.CompareTag("TagName")      // Efficient tag comparison
Instantiate(prefab, position, rotation) // Create instance
Destroy(gameObject)                   // Destroy GameObject
Destroy(gameObject, 2f)               // Destroy after delay
```

### Time
```csharp
Time.deltaTime                        // Frame time (Update)
Time.fixedDeltaTime                   // Physics timestep (FixedUpdate)
Time.time                             // Time since game start
Time.timeScale                        // Game speed multiplier (1 = normal, 0 = pause)
```

### Input
```csharp
Input.GetKey(KeyCode.W)               // Held this frame
Input.GetKeyDown(KeyCode.Space)       // Pressed this frame
Input.GetKeyUp(KeyCode.Escape)        // Released this frame
Input.GetAxis("Horizontal")           // -1 to 1 (smooth)
Input.GetMouseButtonDown(0)           // 0=left, 1=right, 2=middle
```

### Physics
```csharp
rb.AddForce(Vector3.forward * 10f)    // Apply force
rb.velocity                           // Current velocity
rb.isKinematic                        // Disable physics simulation
Physics.Raycast(origin, direction, out hit, maxDistance) // Ray intersection
```

---

## Best Practices Checklist

✅ **Lifecycle:**
- [ ] Initialize in Awake() or Start() (not Update)
- [ ] Cache component references (don't call GetComponent every frame)
- [ ] Clean up in OnDisable() or OnDestroy()

✅ **Events:**
- [ ] Subscribe in OnEnable()
- [ ] Unsubscribe in OnDisable()
- [ ] Use `?.Invoke()` for null-safe event invocation

✅ **Coroutines:**
- [ ] Store coroutine reference
- [ ] Stop previous coroutine before starting new one
- [ ] Stop all coroutines in OnDisable()

✅ **Physics:**
- [ ] Use FixedUpdate() for physics calculations
- [ ] Use Rigidbody.AddForce() instead of transform.position for physics objects
- [ ] Use CompareTag() for collision filtering

✅ **Null Safety:**
- [ ] Null-check GetComponent() results
- [ ] Use RequireComponent attribute for dependencies
- [ ] Log errors when expected components are missing

---

## Summary: What This Skill Prevents

✅ NullReferenceException from missing components
✅ Memory leaks from unsubscribed events
✅ Orphaned coroutines after object destruction
✅ Physics inconsistencies from wrong Update method
✅ Singleton duplication across scenes
✅ Collision detection failures
✅ Input polling in wrong lifecycle method

**Use this skill for all Unity gameplay programming and avoid the runtime errors that break your game.**
