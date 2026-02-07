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
            TryAttack();
        }

        private void TryAttack()
        {
            if (GridManager.Instance == null) return;

            currentFacing.ToGridOffset(out int dx, out int dy);

            // Check cells from range 1 to attackRange in facing direction
            for (int r = 1; r <= attackRange; r++)
            {
                int targetX = GridX + dx * r;
                int targetY = GridY + dy * r;

                CellState targetState = GridManager.Instance.GetCellState(targetX, targetY);

                if (targetState == CellState.Resource)
                {
                    AttackResource(targetX, targetY);
                    return;
                }

                // Stop at first occupied cell (can't attack through units)
                if (targetState != CellState.Empty)
                    return;
            }
        }

        private void AttackResource(int targetX, int targetY)
        {
            GameObject targetObj = GridManager.Instance.GetCellOccupant(targetX, targetY);
            if (targetObj == null) return;

            ResourceNode node = targetObj.GetComponent<ResourceNode>();
            if (node == null) return;

            int tokensEarned = node.TakeDamage(attackDamage);

            // Grant tokens to player
            if (team == Team.Player && tokensEarned > 0 && ResourceTokenManager.Instance != null)
            {
                Vector3 nodePos = GridManager.Instance.GridToWorldPosition(targetX, targetY);
                ResourceTokenManager.Instance.AddTokens(tokensEarned, nodePos);
            }

            // Spawn attack VFX
            SpawnAttackEffect(targetX, targetY);
        }

        private void SpawnAttackEffect(int targetX, int targetY)
        {
            Vector3 targetPos = GridManager.Instance.GridToWorldPosition(targetX, targetY);
            Vector3 myPos = transform.position;
            Vector3 midPoint = Vector3.Lerp(myPos, targetPos, 0.6f) + Vector3.up * 0.5f;

            GameObject vfxObj = new GameObject("AttackVFX");
            vfxObj.transform.position = midPoint;

            ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.3f;
            main.startSpeed = 2f;
            main.startSize = 0.08f;
            main.startColor = Color.yellow;
            main.maxParticles = 8;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 8)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            Destroy(vfxObj, 1f);
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
