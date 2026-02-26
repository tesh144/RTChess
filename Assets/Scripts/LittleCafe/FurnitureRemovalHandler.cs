using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Handles tap-and-hold removal of placed furniture from the grid.
    ///
    /// Detection uses the GRID (raycast → ground plane → grid cell → occupant)
    /// so that chairs tucked under tables are still selectable via their
    /// original grid square.
    ///
    /// - Tap (release before tapThreshold): triggers "Interact" animation
    /// - Hold (holdDuration seconds): shows radial loading bar, then removes
    ///   with "Destroy" animation
    ///
    /// Does NOT interfere with DragDropHandler (only activates when not dragging).
    /// </summary>
    public class FurnitureRemovalHandler : MonoBehaviour
    {
        public static FurnitureRemovalHandler Instance { get; private set; }

        [Header("Hold Settings")]
        [SerializeField] private float holdDuration = 3f;
        [SerializeField] private float tapThreshold = 0.2f;

        [Header("Loading Bar")]
        [SerializeField] private float barSize = 60f;
        [SerializeField] private Color barBackgroundColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color barFillColor = new Color(1f, 0.35f, 0.2f, 0.9f);
        [SerializeField] private Color barCompletedColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        [Header("Animation")]
        [SerializeField] private float interactAnimDuration = 0.6f;
        [SerializeField] private float destroyAnimDuration = 0.5f;

        // State
        private bool isHolding = false;
        private float holdTimer = 0f;
        private FurnitureObject holdTarget = null;
        private Animator holdTargetAnimator = null;
        private bool interactTriggered = false; // Whether we've already fired the interact anim this hold

        // Loading bar UI (created dynamically)
        private Canvas uiCanvas;
        private GameObject loadingBarObj;
        private Image loadingBarBackground;
        private Image loadingBarFill;
        private RectTransform loadingBarRect;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CreateLoadingBarUI();
            HideLoadingBar();
        }

        private void Update()
        {
            // Don't process if DragDropHandler is actively dragging
            if (DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging)
            {
                CancelHold();
                return;
            }

            // Mouse/touch down
            if (Input.GetMouseButtonDown(0))
            {
                TryStartInteraction();
            }

            // Mouse/touch held
            if (Input.GetMouseButton(0) && isHolding)
            {
                UpdateHold();
            }

            // Mouse/touch released
            if (Input.GetMouseButtonUp(0))
            {
                if (isHolding && holdTimer < tapThreshold && holdTarget != null)
                {
                    // Short tap — trigger interact animation
                    TriggerInteractAnimation(holdTarget);
                }
                CancelHold();
            }
        }

        /// <summary>
        /// Raycast to the ground plane, convert to grid cell, look up the
        /// occupant via GridManager. This works for chairs that have visually
        /// tucked under a table — we detect them by their original grid cell.
        /// </summary>
        private void TryStartInteraction()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            // Raycast to ground plane at Y=0 (tile surface)
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float dist)) return;

            Vector3 worldHit = ray.GetPoint(dist);

            // Convert to grid coordinates
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            if (!gm.WorldToGridPosition(worldHit, out int gx, out int gy)) return;

            // Don't allow interaction with fogged (unrevealed) cells
            if (FogManager.Instance != null && !FogManager.Instance.IsCellRevealed(gx, gy))
                return;

            // Look up the occupant on this cell
            GameObject occupantObj = gm.GetOccupant(gx, gy);
            if (occupantObj == null) return;

            FurnitureObject furniture = occupantObj.GetComponent<FurnitureObject>();
            if (furniture == null) return;

            isHolding = true;
            holdTimer = 0f;
            holdTarget = furniture;
            interactTriggered = false;

            // Find animator — check root first, then AnimatorHolder child
            holdTargetAnimator = furniture.GetComponent<Animator>();
            if (holdTargetAnimator == null)
            {
                Transform animHolder = furniture.transform.Find("AnimatorHolder");
                if (animHolder != null)
                    holdTargetAnimator = animHolder.GetComponent<Animator>();
            }

            Debug.Log($"[FurnitureRemoval] Started interaction with {furniture.name} at grid ({gx},{gy})");
        }

        /// <summary>
        /// Update hold progress. Fires interact animation once when crossing
        /// the tap threshold, then advances the radial loading bar.
        /// </summary>
        private void UpdateHold()
        {
            holdTimer += Time.deltaTime;

            // Once past tap threshold, this is a hold — fire interact animation once
            if (holdTimer >= tapThreshold && !interactTriggered)
            {
                interactTriggered = true;
                TriggerInteractAnimation(holdTarget);
            }

            // Show and advance loading bar
            if (holdTimer >= tapThreshold)
            {
                float holdProgress = Mathf.Clamp01((holdTimer - tapThreshold) / holdDuration);
                UpdateLoadingBar(holdProgress);

                if (holdProgress >= 1f)
                {
                    RemoveFurniture(holdTarget);
                    CancelHold();
                }
            }
        }

        /// <summary>
        /// Trigger a small bounce/interact animation.
        /// </summary>
        private void TriggerInteractAnimation(FurnitureObject furniture)
        {
            if (furniture == null) return;

            if (holdTargetAnimator != null)
            {
                holdTargetAnimator.enabled = true;
                holdTargetAnimator.SetTrigger("interact");

                // Disable animator after animation completes to avoid conflicts
                StartCoroutine(DisableAnimatorAfterDelay(holdTargetAnimator, interactAnimDuration));
            }

            // SFX: tap interaction
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayHitWeak();

            Debug.Log($"[FurnitureRemoval] Interact animation on {furniture.name}");
        }

        private IEnumerator DisableAnimatorAfterDelay(Animator animator, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (animator != null)
                animator.enabled = false;
        }

        /// <summary>
        /// Remove furniture from the board with destroy animation.
        /// </summary>
        private void RemoveFurniture(FurnitureObject furniture)
        {
            if (furniture == null) return;

            Debug.Log($"[FurnitureRemoval] Removing {furniture.name} at ({furniture.GridX}, {furniture.GridY})");

            // Trigger destroy animation
            if (holdTargetAnimator != null)
            {
                holdTargetAnimator.enabled = true;
                holdTargetAnimator.SetTrigger("remove");
            }

            // Clean up grid state and connectivity immediately
            furniture.OnRemoved();

            // Unregister from building production if tracked
            if (BuildingProductionManager.Instance != null)
                BuildingProductionManager.Instance.UnregisterBuilding(furniture.gameObject);

            // Destroy the GameObject after the animation plays
            Destroy(furniture.gameObject, destroyAnimDuration + 0.1f);

            // Play SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayRemoval();
            else if (SFXManager.Instance != null)
                SFXManager.Instance.PlayPlayerPlacement();

            // Camera shake for feedback
            CameraSystemLocator.Current?.Shake(0.08f, 0.15f);
        }

        /// <summary>
        /// Cancel any in-progress hold.
        /// </summary>
        private void CancelHold()
        {
            isHolding = false;
            holdTimer = 0f;
            holdTarget = null;
            holdTargetAnimator = null;
            interactTriggered = false;
            HideLoadingBar();
        }

        // ========== Loading Bar UI ==========

        private void CreateLoadingBarUI()
        {
            // Find or create a canvas
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                GameObject canvasObj = new GameObject("RemovalUICanvas");
                uiCanvas = canvasObj.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Loading bar container
            loadingBarObj = new GameObject("RemovalLoadingBar");
            loadingBarRect = loadingBarObj.AddComponent<RectTransform>();
            loadingBarRect.SetParent(uiCanvas.transform, false);
            loadingBarRect.sizeDelta = new Vector2(barSize, barSize);

            // Background (dark circle)
            loadingBarBackground = loadingBarObj.AddComponent<Image>();
            loadingBarBackground.color = barBackgroundColor;
            loadingBarBackground.type = Image.Type.Filled;
            loadingBarBackground.fillMethod = Image.FillMethod.Radial360;
            loadingBarBackground.fillAmount = 1f;

            // Fill (radial progress)
            GameObject fillObj = new GameObject("Fill");
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.SetParent(loadingBarRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = new Vector2(-8f, -8f); // Slight inset

            loadingBarFill = fillObj.AddComponent<Image>();
            loadingBarFill.color = barFillColor;
            loadingBarFill.type = Image.Type.Filled;
            loadingBarFill.fillMethod = Image.FillMethod.Radial360;
            loadingBarFill.fillOrigin = (int)Image.Origin360.Top;
            loadingBarFill.fillClockwise = true;
            loadingBarFill.fillAmount = 0f;
        }

        private void UpdateLoadingBar(float progress)
        {
            if (loadingBarObj == null) return;

            loadingBarObj.SetActive(true);

            // Position above the held object in screen space
            if (holdTarget != null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = FindObjectOfType<Camera>();
                if (cam != null)
                {
                    Vector3 worldPos = holdTarget.transform.position + Vector3.up * 2f;
                    Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        uiCanvas.transform as RectTransform,
                        screenPos,
                        null,
                        out Vector2 canvasPos
                    );
                    loadingBarRect.anchoredPosition = canvasPos;
                }
            }

            // Update fill
            loadingBarFill.fillAmount = progress;
            loadingBarFill.color = progress >= 1f ? barCompletedColor : barFillColor;
        }

        private void HideLoadingBar()
        {
            if (loadingBarObj != null)
            {
                loadingBarObj.SetActive(false);
                if (loadingBarFill != null)
                    loadingBarFill.fillAmount = 0f;
            }
        }
    }
}
