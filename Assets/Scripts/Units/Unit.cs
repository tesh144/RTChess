using UnityEngine;

namespace ClockworkGrid
{
    public enum Team
    {
        Player,
        Enemy
    }

    public class Unit : MonoBehaviour
    {
        [Header("Unit Stats")]
        [SerializeField] protected int hp = 10;
        [SerializeField] protected int attackDamage = 3;
        [SerializeField] protected int attackRange = 1;
        [SerializeField] protected int attackIntervalMultiplier = 2;
        [SerializeField] protected int resourceCost = 3;

        [Header("Unit State")]
        [SerializeField] private Team team = Team.Player;
        [SerializeField] private Facing currentFacing = Facing.North;

        [Header("Rotation Animation")]
        [SerializeField] private float rotationDuration = 0.25f;

        // Grid position
        public int GridX { get; set; }
        public int GridY { get; set; }

        public Team Team => team;
        public Facing CurrentFacing => currentFacing;
        public int HP => hp;
        public int AttackDamage => attackDamage;
        public int AttackRange => attackRange;
        public int AttackIntervalMultiplier => attackIntervalMultiplier;
        public int ResourceCost => resourceCost;

        // Rotation animation state
        private bool isRotating;
        private Quaternion rotationStart;
        private Quaternion rotationTarget;
        private float rotationElapsed;

        protected virtual void Start()
        {
            // Set initial rotation to match facing
            transform.rotation = Quaternion.Euler(0f, currentFacing.ToYRotation(), 0f);

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }
        }

        protected virtual void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        private void Update()
        {
            if (isRotating)
            {
                rotationElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(rotationElapsed / rotationDuration);
                // Smooth ease-in-out
                t = t * t * (3f - 2f * t);
                transform.rotation = Quaternion.Slerp(rotationStart, rotationTarget, t);

                if (t >= 1f)
                {
                    isRotating = false;
                    transform.rotation = rotationTarget;
                }
            }
        }

        private void OnIntervalTick(int intervalCount)
        {
            // Only rotate on intervals that match our multiplier
            if (intervalCount % attackIntervalMultiplier != 0) return;

            Rotate();
        }

        private void Rotate()
        {
            if (team == Team.Player)
                currentFacing = currentFacing.RotateClockwise();
            else
                currentFacing = currentFacing.RotateCounterClockwise();

            // Start smooth rotation animation
            rotationStart = transform.rotation;
            rotationTarget = Quaternion.Euler(0f, currentFacing.ToYRotation(), 0f);
            rotationElapsed = 0f;
            isRotating = true;
        }

        public void Initialize(Team unitTeam, int gridX, int gridY)
        {
            team = unitTeam;
            GridX = gridX;
            GridY = gridY;
            currentFacing = Facing.North;
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
}
