using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Vex.HudShowcase
{
    /// <summary>
    /// The showcase "conductor" — stands in for the game, PASSING values to the dumb bar. It changes CurrentHealth in
    /// discrete hits/heals (the structure value) AND maintains the GHOST SLIDER value (BarGhost): the ghost lingers
    /// briefly after a change then drains/fills toward current, so the bar shows the classic delayed-damage / heal-lead
    /// band. The UI never computes any of this — it renders the two values it is handed.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CombatConductorSystem : SystemBase
    {
        private NativeHashMap<Entity, int> lastAction;
        private NativeHashMap<Entity, float> lastChange;

        protected override void OnCreate()
        {
            this.lastAction = new NativeHashMap<Entity, int>(8, Allocator.Persistent);
            this.lastChange = new NativeHashMap<Entity, float>(8, Allocator.Persistent);
            this.RequireForUpdate<CombatConductor>();
        }

        protected override void OnDestroy()
        {
            this.lastAction.Dispose();
            this.lastChange.Dispose();
        }

        protected override void OnUpdate()
        {
            var c = SystemAPI.GetSingleton<CombatConductor>();
            var key = (IntrinsicKey)c.HealthKey;
            var time = (float)SystemAPI.Time.ElapsedTime;
            var dt = math.min((float)SystemAPI.Time.DeltaTime, 0.1f);

            const float holdDelay = 0.3f; // linger before the ghost starts draining
            var drainPerSec = c.Max * 1.2f; // ghost catch-up speed (units/sec)

            // Ensure each health actor carries the ghost-slider value.
            var need = new NativeList<Entity>(Allocator.Temp);
            foreach (var (intr, ent) in SystemAPI.Query<DynamicBuffer<Intrinsic>>().WithEntityAccess().WithNone<BarGhost>())
            {
                if (intr.AsMap().TryGetValue(key, out _))
                {
                    need.Add(ent);
                }
            }

            foreach (var e in need)
            {
                this.EntityManager.AddComponentData(e, new BarGhost { Value = 0f });
                this.EntityManager.AddBuffer<BarFeedbackEvent>(e);
            }

            need.Dispose();

            var actor = 0;
            foreach (var (intr, ghost, fb, ent) in SystemAPI.Query<DynamicBuffer<Intrinsic>, RefRW<BarGhost>, DynamicBuffer<BarFeedbackEvent>>().WithEntityAccess())
            {
                var map = intr.AsMap();
                if (!map.TryGetValue(key, out _))
                {
                    continue;
                }

                var localTime = time + (actor * c.PhasePerActor);
                var actionIndex = (int)(localTime / c.Interval);
                actor++;

                ref var hp = ref map.GetOrAddRefUnsafe(key, c.Max);

                // First sight: snap the ghost to the live value (no spurious band) and don't fire an action.
                if (!this.lastAction.ContainsKey(ent))
                {
                    this.lastAction[ent] = actionIndex;
                    this.lastChange[ent] = time - 10f;
                    ghost.ValueRW.Value = hp;
                    continue;
                }

                if (this.lastAction[ent] != actionIndex)
                {
                    this.lastAction[ent] = actionIndex;
                    if (hp > 35)
                    {
                        var dmg = math.min(hp, 16 + ((actionIndex % 3) * 10)); // varied 16 / 26 / 36
                        hp -= dmg;
                        fb.Add(new BarFeedbackEvent { Kind = FeedbackKind.DamageChip, Amount = dmg }); // damage → the chip
                        ghost.ValueRW.Value = hp; // snap the slider to current: damage shows the chip, not the slider
                    }
                    else
                    {
                        hp = c.Max; // heal → leave the ghost low so the slider shows a green lead that fills up
                    }

                    this.lastChange[ent] = time;
                }

                // Ghost lags: hold briefly, then move toward the live value (drains on damage, fills on heal).
                var g = ghost.ValueRO.Value;
                if (time - this.lastChange[ent] > holdDelay)
                {
                    g = MoveToward(g, hp, drainPerSec * dt);
                }

                ghost.ValueRW.Value = g;
            }
        }

        private static float MoveToward(float a, float b, float maxDelta)
        {
            var d = b - a;
            return math.abs(d) <= maxDelta ? b : a + (math.sign(d) * maxDelta);
        }
    }
}
