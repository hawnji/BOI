using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Items
{
    public enum EffectSourceKind { Item, Synergy, Transformation, Character, Curse, Temporary, ActiveUse }

    /// <summary>Everything an effect instance may use. One context per effect record.</summary>
    public sealed class EffectContext
    {
        public InteractionEngine Engine { get; internal set; }
        public EffectSpec Spec { get; internal set; }
        public EffectSourceKind SourceKind { get; internal set; }
        /// <summary>Id of the owning item/synergy/transformation (e.g. "ember_sigil").</summary>
        public string SourceId { get; internal set; }
        /// <summary>The removal handle for stat modifiers and companions (unique per attachment).</summary>
        public object Owner { get; internal set; }
        public int Stacks { get; internal set; } = 1;
        /// <summary>True only the first time this source is acquired this run (for one-time grants).</summary>
        public bool IsFirstAcquisition { get; internal set; }
        internal EffectRecord Record { get; set; }

        public StatSheet Stats => Engine.Stats;
        public HealthPool Health => Engine.Health;
        public PlayerResources Resources => Engine.Resources;
        public IGameWorld World => Engine.World;
        public SeededRandom Rng => Engine.Rng;
        public Content.ParamBag P => Spec.Params;

        /// <summary>True when the effect is neither suppressed nor blocked by its condition.</summary>
        public bool IsEnabled => Record == null || Record.IsEnabled;

        /// <summary>Luck-scaled chance roll: each point of luck adds luckScale to the probability.</summary>
        public bool Roll(float chance, float luckScale = 0.02f)
        {
            float c = chance + Engine.Stats[StatId.Luck] * luckScale;
            return Rng.Chance(c);
        }

        /// <summary>
        /// Gate for any effect that spawns projectiles/explosions in reaction to an event.
        /// Fails when the source is too many generations deep or the per-frame spawn budget is spent.
        /// </summary>
        public bool CanSpawnFrom(int generation, int count = 1)
        {
            if (generation >= ProjectileSpec.MaxGeneration)
            {
                Engine.ReportGuard("generation-limit", SourceId);
                return false;
            }
            return Engine.TryConsumeSpawnBudget(count, SourceId);
        }
    }

    /// <summary>Engine bookkeeping for one effect instance.</summary>
    public sealed class EffectRecord
    {
        public IEffect Effect;
        public EffectContext Context;
        public string Condition;
        public bool Suppressed;
        public string SuppressedBy;
        public long Sequence;           // registration order for deterministic tie-breaks

        internal ConditionRegistry Conditions;
        internal InteractionEngine Engine;

        public string Type => Context.Spec.Type;
        public string SourceId => Context.SourceId;
        public string EffectId => Context.Spec.Id;

        public bool ConditionMet => string.IsNullOrEmpty(Condition) || Conditions.Evaluate(Condition, Engine, Context);
        public bool IsEnabled => !Suppressed && ConditionMet;
    }

    /// <summary>Adapter so conditional stat modifiers follow their effect record's state.</summary>
    internal sealed class RecordStatCondition : IStatCondition
    {
        private readonly EffectRecord _record;
        public RecordStatCondition(EffectRecord record) { _record = record; }
        public bool IsMet() => _record.IsEnabled;
    }
}
