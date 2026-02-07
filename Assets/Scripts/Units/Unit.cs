using UnityEngine;

namespace ClockworkGrid
{
    public enum Team
    {
        Player,
        Enemy
    }

    public class Unit : MonoBehaviour, IDamageable
    {
        [Header("Unit Stats")]
        [SerializeField] protected int maxHP = 10;
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

        // Current state
        private int currentHP;
        private bool isDestroyed = false;

        // HP text references
        private TextMesh hpTextMesh;
        private TextMesh hpTextShadow;
        private Renderer[] renderers;
        private Color[] originalColors;
        private float damageFlashTimer;
        private const float DamageFlashDuration = 0.15f;

        // IDamageable implementation
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => isDestroyed;

        // Public accessors
        public Team Team => team;
        public Facing CurrentFacing => currentFacing;
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
            // Initialize HP
            currentHP = maxHP;

            // Set initial rotation to match facing
            transform.rotation = Quaternion.Euler(0f, currentFacing.ToYRotation(), 0f);

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }

            // Find HP text and cache renderers
            FindHPText();
            CacheRenderers();
            UpdateHPText();
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
            // Handle rotation animation
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

            // Handle damage flash
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= Time.deltaTime;
                if (damageFlashTimer <= 0f)
                {
                    RestoreColors();
                }
            }
        }

        private void OnIntervalTick(int intervalCount)
        {
            // Check for placement cooldown
            PlacementCooldown cooldown = GetComponent<PlacementCooldown>();
            if (cooldown != null && cooldown.IsOnCooldown)
                return; // Skip all actions while on cooldown

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
            // Priority: Enemy Units > Resource Nodes > Nothing
            for (int r = 1; r <= attackRange; r++)
            {
                int targetX = GridX + dx * r;
                int targetY = GridY + dy * r;

                CellState targetState = GridManager.Instance.GetCellState(targetX, targetY);

                // Priority 1: Attack enemy units
                if (targetState == CellState.PlayerUnit || targetState == CellState.EnemyUnit)
                {
                    GameObject targetObj = GridManager.Instance.GetCellOccupant(targetX, targetY);
                    if (targetObj != null)
                    {
                        Unit targetUnit = targetObj.GetComponent<Unit>();
                        if (targetUnit != null && targetUnit.Team != this.team)
                        {
                            // Attack enemy unit
                            AttackUnit(targetUnit);
                            return;
                        }
                    }
                    // Stop at ally or invalid unit
                    return;
                }

                // Priority 2: Attack resources
                if (targetState == CellState.Resource)
                {
                    AttackResource(targetX, targetY);
                    return;
                }

                // Stop at first occupied cell (can't attack through obstacles)
                if (targetState != CellState.Empty)
                    return;
            }
        }

        private void AttackUnit(Unit target)
        {
            if (target == null || target.IsDestroyed) return;

            // Deal damage to enemy unit
            target.TakeDamage(attackDamage);

            // Spawn attack VFX
            Vector3 targetPos = GridManager.Instance.GridToWorldPosition(target.GridX, target.GridY);
            SpawnCombatEffect(targetPos);

            Debug.Log($"{team} {gameObject.name} attacks {target.Team} unit for {attackDamage} damage!");
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

        /// <summary>
        /// IDamageable implementation - Take damage from an attack
        /// </summary>
        public int TakeDamage(int damage)
        {
            if (isDestroyed || currentHP <= 0) return 0;

            int actualDamage = Mathf.Min(damage, currentHP);
            currentHP -= actualDamage;

            // Visual feedback
            FlashRed();
            UpdateHPText();

            Debug.Log($"{team} unit took {actualDamage} damage! HP: {currentHP}/{maxHP}");

            // Check for death
            if (currentHP <= 0)
            {
                OnDestroyed();
            }

            return actualDamage;
        }

        private void OnDestroyed()
        {
            if (isDestroyed) return;

            isDestroyed = true;

            Debug.Log($"{team} unit destroyed at ({GridX}, {GridY})");

            // Remove from grid
            if (GridManager.Instance != null)
            {
                GridManager.Instance.RemoveUnit(GridX, GridY);
            }

            // Unsubscribe from interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }

            // Spawn death VFX
            SpawnDeathEffect();

            // Destroy GameObject
            Destroy(gameObject);
        }

        private void SpawnDeathEffect()
        {
            GameObject vfxObj = new GameObject("UnitDeathVFX");
            vfxObj.transform.position = transform.position + Vector3.up * 0.5f;

            ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.startColor = team == Team.Player ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
            main.maxParticles = 30;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 30)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            Destroy(vfxObj, 1.5f);
        }

        private void SpawnCombatEffect(Vector3 targetPos)
        {
            Vector3 myPos = transform.position;
            Vector3 midPoint = Vector3.Lerp(myPos, targetPos, 0.5f) + Vector3.up * 0.5f;

            GameObject vfxObj = new GameObject("CombatVFX");
            vfxObj.transform.position = midPoint;

            ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.3f;
            main.startSpeed = 3f;
            main.startSize = 0.1f;
            main.startColor = Color.red;
            main.maxParticles = 12;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 12)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            Destroy(vfxObj, 1f);
        }

        private void FindHPText()
        {
            // Find HP text components by name in children hierarchy
            TextMesh[] allTextMeshes = GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm.name == "HPText")
                {
                    hpTextMesh = tm;
                }
                else if (tm.name == "HPTextShadow")
                {
                    hpTextShadow = tm;
                }
            }
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                    originalColors[i] = renderers[i].material.color;
            }
        }

        private void FlashRed()
        {
            if (renderers == null) return;

            foreach (Renderer r in renderers)
            {
                if (r != null && r.name != "HPText" && r.name != "HPTextShadow")
                    r.material.color = Color.red;
            }
            damageFlashTimer = DamageFlashDuration;
        }

        private void RestoreColors()
        {
            if (renderers == null || originalColors == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name != "HPText" && renderers[i].name != "HPTextShadow")
                    renderers[i].material.color = originalColors[i];
            }
        }

        private void UpdateHPText()
        {
            if (hpTextMesh == null) return;

            // Update both text meshes with current HP value
            string hpString = currentHP.ToString();
            hpTextMesh.text = hpString;

            if (hpTextShadow != null)
            {
                hpTextShadow.text = hpString;
            }

            // Color from green to red based on HP ratio
            float ratio = (float)currentHP / maxHP;
            hpTextMesh.color = Color.Lerp(Color.red, Color.green, ratio);
        }

        public void Initialize(Team unitTeam, int gridX, int gridY)
        {
            team = unitTeam;
            GridX = gridX;
            GridY = gridY;
            currentFacing = Facing.North;
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        /// <summary>
        /// Initialize unit with UnitStats (Iteration 6)
        /// </summary>
        public void Initialize(Team unitTeam, int gridX, int gridY, UnitStats stats)
        {
            // Apply stats
            maxHP = stats.maxHP;
            attackDamage = stats.attackDamage;
            attackRange = stats.attackRange;
            attackIntervalMultiplier = stats.attackIntervalMultiplier;
            resourceCost = stats.resourceCost;

            // Reset HP to new max
            currentHP = maxHP;

            // Apply team and position
            team = unitTeam;
            GridX = gridX;
            GridY = gridY;
            currentFacing = Facing.North;
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // Apply visual scale
            if (stats.modelScale != 1f)
            {
                transform.localScale = Vector3.one * stats.modelScale;
            }

            // Apply color
            ApplyColor(stats.unitColor);

            // Update HP text
            UpdateHPText();

            Debug.Log($"Initialized {stats.unitName}: HP={maxHP}, Damage={attackDamage}, Range={attackRange}, Interval={attackIntervalMultiplier}");
        }

        /// <summary>
        /// Apply color to all renderers
        /// </summary>
        private void ApplyColor(Color color)
        {
            if (renderers == null || renderers.Length == 0)
            {
                CacheRenderers();
            }

            foreach (Renderer r in renderers)
            {
                if (r != null && !r.name.Contains("HPText"))
                {
                    r.material.color = color;
                }
            }

            // Update cached colors
            if (originalColors != null)
            {
                for (int i = 0; i < originalColors.Length; i++)
                {
                    originalColors[i] = color;
                }
            }
        }
    }
}
