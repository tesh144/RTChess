using UnityEngine;
using System.Collections;

namespace LittleCafe
{
    /// <summary>
    /// Controls chair visual positioning states by moving the Recenter transform.
    /// The prefab hierarchy is: Root → AnimatorHolder → Recenter → [model]
    ///
    /// The Animator controls AnimatorHolder, so moving Recenter avoids conflicts.
    /// The root transform stays locked to the grid cell center.
    ///
    /// States:
    /// - Stored: tucked toward the table (default when unoccupied)
    /// - InUse: pulled away from table for character clearance
    /// - Idle: no table nearby, Recenter at local origin
    /// </summary>
    public class ChairPositionController : MonoBehaviour
    {
        public enum ChairState
        {
            Idle,    // Not near a table, Recenter at (0,0,0)
            Stored,  // Tucked toward the table via Recenter local Z
            InUse    // Pulled back for character clearance
        }

        [Header("Position Offsets")]
        [SerializeField] private float storedOffset = 0.75f;  // Local Z offset toward table when tucked
        [SerializeField] private float inUseOffset = 0.15f;   // Small pullback when NPC sitting

        [Header("Animation")]
        [SerializeField] private float transitionSpeed = 4f;   // Lerp speed for position changes
        [SerializeField] private float placementAnimDelay = 0.9f; // Wait for drop animation before tucking

        [Header("Debug")]
        [SerializeField] private ChairState currentState = ChairState.Idle;

        private ChairObject chairObject;
        private Transform recenterTransform;      // The Recenter child transform we move
        private Vector3 targetLocalPosition;      // Target local position for Recenter
        private bool isTransitioning = false;
        private bool waitingForPlacementAnim = false; // True while waiting for drop animation

        public ChairState CurrentState => currentState;

        private void Awake()
        {
            chairObject = GetComponent<ChairObject>();
            FindRecenterTransform();
        }

        /// <summary>
        /// Find the Recenter transform in the prefab hierarchy.
        /// Expected hierarchy: Root → AnimatorHolder → Recenter → [model]
        /// </summary>
        private void FindRecenterTransform()
        {
            // Look for "Recenter" or "CharacterRe:Zero" by name in children
            recenterTransform = FindChildRecursive(transform, "Recenter");
            if (recenterTransform == null)
                recenterTransform = FindChildRecursive(transform, "CharacterRe:Zero");

            if (recenterTransform == null)
            {
                Debug.LogWarning($"[ChairPositionController] {gameObject.name}: Could not find Recenter transform! Chair tuck will not work.");
            }
            else
            {
                Debug.Log($"[ChairPositionController] {gameObject.name}: Found Recenter transform at {recenterTransform.name}");
            }
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Initialize after chair is placed on the grid.
        /// Snaps Recenter instantly to stored position if facing a table.
        /// Root transform stays at grid center — only Recenter moves.
        /// </summary>
        public void Initialize(Vector3 gridCenter, FurnitureObject facingTable)
        {
            if (recenterTransform == null)
                FindRecenterTransform();

            if (recenterTransform == null) return;

            // Always start Recenter at origin — let the drop animation play first
            recenterTransform.localPosition = Vector3.zero;
            targetLocalPosition = Vector3.zero;
            isTransitioning = false;

            if (facingTable != null)
            {
                // After the placement animation finishes, smoothly tuck toward the table
                currentState = ChairState.Idle; // Temporarily idle until animation completes
                waitingForPlacementAnim = true;
                StartCoroutine(TuckAfterPlacementAnimation(facingTable));
                Debug.Log($"[ChairPositionController] {gameObject.name}: Initialized — waiting {placementAnimDelay}s for drop animation before tucking");
            }
            else
            {
                currentState = ChairState.Idle;
            }
        }

        /// <summary>
        /// Transition chair to a new state.
        /// All movement is on the Recenter transform's local Z axis.
        /// Positive Z = toward the table (because the chair faces the table).
        /// </summary>
        public void SetState(ChairState newState, FurnitureObject facingTable)
        {
            if (currentState == newState) return;
            if (recenterTransform == null) return;

            ChairState previousState = currentState;
            currentState = newState;

            switch (newState)
            {
                case ChairState.Idle:
                    targetLocalPosition = Vector3.zero;
                    break;

                case ChairState.Stored:
                    // Tuck toward table along local Z
                    targetLocalPosition = new Vector3(0f, 0f, storedOffset);
                    break;

                case ChairState.InUse:
                    // Pull back slightly from center (away from table)
                    targetLocalPosition = new Vector3(0f, 0f, -inUseOffset);
                    break;
            }

            isTransitioning = true;
            Debug.Log($"[ChairPositionController] {gameObject.name}: {previousState} → {newState} (target localZ = {targetLocalPosition.z})");
        }

        /// <summary>
        /// Pull chair out (NPC approaching to sit).
        /// </summary>
        public void PullOut()
        {
            if (chairObject != null && chairObject.FacingTable != null)
                SetState(ChairState.InUse, chairObject.FacingTable);
        }

        /// <summary>
        /// Tuck chair back under table (NPC left).
        /// </summary>
        public void TuckIn()
        {
            if (chairObject != null && chairObject.FacingTable != null)
                SetState(ChairState.Stored, chairObject.FacingTable);
        }

        /// <summary>
        /// Reset to idle (no table nearby).
        /// </summary>
        public void ResetToIdle()
        {
            SetState(ChairState.Idle, null);
        }

        /// <summary>
        /// Wait for the placement drop animation to finish, then begin the tuck transition.
        /// </summary>
        private IEnumerator TuckAfterPlacementAnimation(FurnitureObject facingTable)
        {
            yield return new WaitForSeconds(placementAnimDelay);

            waitingForPlacementAnim = false;

            // Now smoothly transition to Stored position
            if (facingTable != null && recenterTransform != null)
            {
                currentState = ChairState.Idle; // So SetState doesn't early-out
                SetState(ChairState.Stored, facingTable);
                Debug.Log($"[ChairPositionController] {gameObject.name}: Drop animation done → tucking toward table");
            }
        }

        private void Update()
        {
            if (!isTransitioning) return;
            if (recenterTransform == null) return;

            // Smoothly lerp Recenter's local position toward target
            Vector3 current = recenterTransform.localPosition;
            Vector3 newPos = Vector3.Lerp(current, targetLocalPosition, Time.deltaTime * transitionSpeed);
            recenterTransform.localPosition = newPos;

            // Check if close enough to stop
            if (Vector3.Distance(newPos, targetLocalPosition) < 0.002f)
            {
                recenterTransform.localPosition = targetLocalPosition;
                isTransitioning = false;
            }
        }
    }
}
