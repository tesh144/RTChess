# Game Economy & Balancing Research

Reference document for ClockworkCraft economy tuning. Use alongside gameplay CSV recordings.

---

## 1. Source/Sink Economics

The core principle: **Sources (resource generation) must roughly balance Sinks (resource consumption).**

**Phase-based ratios:**
- **Early game:** Sources > Sinks (1.5:1) — player feels empowered, invited to keep playing
- **Mid game:** Sources ≈ Sinks (1:1) — resources become meaningfully scarce
- **Late game:** Sinks > Sources (0.8:1) — aspirational goals, prestige mechanics kick in

**The Pinch Point:** The supply level where demand is maximized — players feel rich enough to spend but poor enough to keep playing. This is the Goldilocks zone.

**Key insight:** The ratio isn't fixed — it's a dynamic relationship that shifts as the game progresses. Income and expense curves can vary (sine wave patterns where players alternate between deficits and surpluses) to create pacing variation.

---

## 2. Cost Escalation Curves

| Curve Type | Formula | Use Case | Feel |
|---|---|---|---|
| Linear | `Cost(n) = c × n` | Short paths, clarity | Gentle, predictable |
| Quadratic | `Cost(n) = c × n²` | Mid-tier progression | Moderate gates |
| Polynomial | `Cost(n) = c × n^k` (k=2.5-3) | Weighty upgrades | Each purchase matters |
| Exponential | `Cost(n) = c × r^n` (r=1.15-1.5) | Idle games | Fast acceleration |

**Real examples:**
- **Cookie Clicker:** Each building costs ~15% more than previous (multiplicative)
- **Clash of Clans:** Different curves per building type (piecewise)
- **Idle Miner:** Each mineshaft produces ~500× more cash than the one above

**Feel principle:** If each level increases income by 60% but costs 2× as much, it takes 25% longer per level — but *feels* like acceleration because absolute income grows fast.

**For ClockworkCraft:** Start quadratic for early buildings, transition to polynomial (k≈2.5) for mid/late game. Avoid pure exponential unless we add prestige mechanics.

---

## 3. Analytics — What to Measure from CSV

**From our GameplayRecorder data, we should calculate:**

### Income Metrics
- Currency generation rate per resource (per tick, per minute)
- Which buildings produce what, and at what rate
- Passive vs active income ratio

### Spending Metrics
- Average time-to-afford for each building (1st, 2nd, 3rd placement)
- Cost-to-income ratio (how many ticks to afford next building)
- Which resources run dry first (bottleneck detection)

### Progression Metrics
- Time between building placements
- Resource accumulation curves over time
- "Dead zones" where no building is affordable for extended periods

### Warning Signs
- Resource stockpiling beyond use → sinks too weak
- Prolonged zero-balance on any resource → sources too weak
- One resource always abundant while others bottleneck → imbalanced production

---

## 4. Pacing Targets

| Game Phase | Time to Afford Next Building | Source:Sink Ratio |
|---|---|---|
| Early (first 5 min) | 30-90 seconds | 1.5:1 |
| Mid (5-15 min) | 2-5 minutes | 1:1 |
| Late (15+ min) | 5-15 minutes | 0.8:1 |

**Progression feel:**
- Early: constant reward, new building every minute
- Mid: increasing scarcity, meaningful choices
- Late: aspirational goals, resource management becomes the game

---

## 5. Multi-Resource Balance

**Resource roles in ClockworkCraft:**

| Resource | Role | Generation | Scarcity |
|---|---|---|---|
| Gold | Primary currency | Workers + loot | Moderate |
| Wood | Production/economy | Trees + buildings | Abundant early |
| Stone | Defenses/stability | Rocks + buildings | Scarce |
| Food | Population growth | Farms + buildings | Moderate |

**Principles:**
- Every building should require 2-3 resource types simultaneously
- No resource should ever be completely trivial or permanently scarce
- Shift which resource is the bottleneck as the game progresses
- Late-game buildings requiring ALL resources forces balanced accumulation

**Interdependency:** If Gold is abundant but Stone is scarce, players are forced to develop Stone production before placing expensive buildings. This creates strategic depth.

---

## 6. Common Pitfalls

### Hyperinflation (Resource Flooding)
- Sources generate faster than sinks consume
- **Fix:** Scale sinks proportionally with sources. Each new building tier IS a new sink.

### Dead-End States
- Players can't afford anything and have no way to earn more
- **Fix:** Always ensure at least one affordable action. Workers should always generate *something*.

### Single-Resource Dominance
- One resource is all that matters, others feel useless
- **Fix:** Mandatory multi-resource costs. No building costs only Gold.

### Difficulty Spikes
- Sudden jump in costs without matching income increase
- **Fix:** Gradual cost escalation with `costIncrement` field. Never more than 2× per step.

---

## 7. Applying to ClockworkCraft EconomyBalanceConfig

When we have CSV data, the analysis process:

1. **Calculate income rates** — resources/tick for each type at different game phases
2. **Calculate time-to-afford** — for each building at each placement count
3. **Check ratios** — is source:sink ratio appropriate for the game phase?
4. **Identify bottlenecks** — which resource causes the longest waits?
5. **Tune baseCost** — set so 1st placement takes ~30-60s of play
6. **Tune costIncrement** — set so each subsequent placement takes ~50% longer
7. **Validate multi-resource** — ensure no single resource dominates costs

**Target formula:**
```
effectiveCost = baseCost + (placementCount × costIncrement)
timeToAfford = effectiveCost / incomeRate_per_tick / ticks_per_second
```

If timeToAfford for the Nth placement feels too fast → increase costIncrement.
If timeToAfford feels too slow → decrease baseCost or costIncrement.
