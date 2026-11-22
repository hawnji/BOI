using System;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Items.Effects
{
    /// <summary>
    /// "stat": {stat, op, value, perStack=true}. Adds one stat modifier while attached.
    /// Flat/PercentAdd scale linearly with stacks; Multiply compounds (value^stacks).
    /// The modifier honours the effect's condition and suppression automatically.
    /// </summary>
    public sealed class StatEffect : EffectBase
    {
        private StatModifier _mod;

        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            var stat = ctx.P.GetEnum("stat", StatId.Damage);
            var op = ctx.P.GetEnum("op", ModifierOp.Flat);
            float value = ctx.P.GetFloat("value");
            bool perStack = ctx.P.GetBool("perStack", true);
            int stacks = perStack ? ctx.Stacks : 1;
            switch (op)
            {
                case ModifierOp.Flat:
                case ModifierOp.PercentAdd: value *= stacks; break;
                case ModifierOp.Multiply: value = (float)Math.Pow(value, stacks); break;
            }
            _mod = new StatModifier(stat, op, value, ctx.Record, ctx.P.GetInt("priority"),
                new RecordStatCondition(ctx.Record), ctx.SourceId);
            ctx.Stats.Add(_mod);
        }

        public override void OnDetach(EffectContext ctx)
        {
            if (_mod != null) ctx.Stats.Remove(_mod);
            _mod = null;
        }
    }

    /// <summary>
    /// "stat_scaling": {stat, perUnit, source: coins|bombs|keys|companions|missing_health|items|tag, tag, max}.
    /// A flat bonus proportional to a live quantity. The engine invalidates the stat sheet whenever
    /// inventory, resources (via pickups) or health change, so the value stays current.
    /// </summary>
    public sealed class StatScalingEffect : EffectBase, IStatValueProvider
    {
        private StatModifier _mod;
        private float _perUnit, _max;
        private string _source, _tag;

        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            var stat = ctx.P.GetEnum("stat", StatId.Damage);
            _perUnit = ctx.P.GetFloat("perUnit", 0.1f) * ctx.Stacks;
            _max = ctx.P.GetFloat("max", 999f);
            _source = ctx.P.GetString("source", "coins");
            _tag = ctx.P.GetString("tag");
            _mod = new StatModifier(stat, ModifierOp.Flat, this, ctx.Record, 0, new RecordStatCondition(ctx.Record), ctx.SourceId);
            ctx.Stats.Add(_mod);
        }

        public override void OnDetach(EffectContext ctx) { if (_mod != null) ctx.Stats.Remove(_mod); _mod = null; }

        public float GetValue()
        {
            float units;
            var e = Ctx.Engine;
            switch (_source)
            {
                case "coins": units = e.Resources.Get(Economy.ResourceKind.Coins); break;
                case "bombs": units = e.Resources.Get(Economy.ResourceKind.Bombs); break;
                case "keys": units = e.Resources.Get(Economy.ResourceKind.Keys); break;
                case "companions": units = e.CountHeldWithTag("companion"); break;
                case "missing_health": units = e.Health == null ? 0 : e.Health.MaxVigor - e.Health.Vigor; break;
                case "items": units = e.Passives.Count; break;
                case "tag": units = _tag == null ? 0 : e.CountHeldWithTag(_tag); break;
                default: units = 0; break;
            }
            return Math.Min(_max, units * _perUnit);
        }
    }
}
