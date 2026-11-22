using System;
using Hollowcrawl.Combat;

namespace Hollowcrawl.Items.Effects
{
    /// <summary>Standard execution order for projectile modifiers (lower runs first).</summary>
    public static class ProjectileOrder
    {
        public const int Delivery = 0;
        public const int Shape = 100;
        public const int Behavior = 200;
        public const int Element = 300;
        public const int Scaling = 400;
        public const int Synergy = 500;   // synergies run after ingredients so they can rewrite them
        public const int Visual = 900;
    }

    /// <summary>Common base: reads "order" override from params and the stack count.</summary>
    public abstract class ProjectileModifierBase : EffectBase, IProjectileModifier
    {
        private int _order;
        protected abstract int DefaultOrder { get; }
        public int Order => _order;

        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            _order = ctx.P.GetInt("order", DefaultOrder);
            Configure(ctx);
        }

        protected virtual void Configure(EffectContext ctx) { }
        public abstract void ModifyProjectile(ProjectileSpec spec, ShotContext shot);
        protected void Visual(ProjectileSpec spec) { var v = Ctx.P.GetString("visual"); if (v != null) spec.AddVisual(v); }
    }

    /// <summary>"proj_pierce": {count=1 (-1 infinite)}. Stacks add.</summary>
    public sealed class PierceEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            int c = Ctx.P.GetInt("count", 1);
            if (c < 0 || spec.PierceCount < 0) spec.PierceCount = -1;
            else spec.PierceCount += c * Ctx.Stacks;
            Visual(spec);
        }
    }

    /// <summary>"proj_homing": {strength=0.5}. Strengths add, capped at 2.</summary>
    public sealed class HomingEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.HomingStrength = Math.Min(2f, spec.HomingStrength + Ctx.P.GetFloat("strength", 0.5f) * Ctx.Stacks);
            Visual(spec);
        }
    }

    /// <summary>"proj_bounce": {count=2}.</summary>
    public sealed class BounceEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.Bounces += Ctx.P.GetInt("count", 2) * Ctx.Stacks;
            Visual(spec);
        }
    }

    /// <summary>"proj_split": {count=2, damageScale=0.5}. Shots split into children when they hit or expire.</summary>
    public sealed class SplitEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.SplitCount += Ctx.P.GetInt("count", 2) * Ctx.Stacks;
            spec.SplitDamageScale = Math.Max(spec.SplitDamageScale, Ctx.P.GetFloat("damageScale", 0.5f));
            Visual(spec);
        }
    }

    /// <summary>"proj_explode": {radius=1.2, damageScale=1}. Radius of multiple sources takes the max, then +15% per extra stack.</summary>
    public sealed class ExplodeEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            float r = Ctx.P.GetFloat("radius", 1.2f) * (1f + 0.15f * (Ctx.Stacks - 1));
            spec.ExplosionRadius = Math.Max(spec.ExplosionRadius, r);
            spec.ExplosionDamageScale = Math.Max(spec.ExplosionDamageScale, Ctx.P.GetFloat("damageScale", 1f));
            spec.Types |= DamageType.Explosive;
            Visual(spec);
        }
    }

    /// <summary>"proj_chain": {jumps=2, range=3, damageScale=0.6}. Hits arc to nearby enemies.</summary>
    public sealed class ChainEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.ChainJumps += Ctx.P.GetInt("jumps", 2) * Ctx.Stacks;
            spec.ChainRange = Math.Max(spec.ChainRange, Ctx.P.GetFloat("range", 3f));
            spec.ChainDamageScale = Math.Max(spec.ChainDamageScale, Ctx.P.GetFloat("damageScale", 0.6f));
            spec.Types |= DamageType.Shock;
            Visual(spec);
        }
    }

    /// <summary>
    /// "proj_element": {type, status, chance=1, duration=3, potency=1, visual}. Adds a damage type and
    /// optionally a status application. Chance scales with luck (+3% per point) and with stacks.
    /// </summary>
    public sealed class ElementEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Element;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.Types |= Ctx.P.GetEnum("type", DamageType.None);
            string status = Ctx.P.GetString("status");
            if (status != null)
            {
                float chance = Math.Min(1f, Ctx.P.GetFloat("chance", 1f) * Ctx.Stacks + Ctx.Stats[Stats.StatId.Luck] * 0.03f);
                spec.Statuses.Add(new StatusApplication
                {
                    StatusId = status,
                    Chance = chance,
                    Duration = Ctx.P.GetFloat("duration", 3f),
                    Potency = Ctx.P.GetFloat("potency", 1f) * (1f + 0.25f * (Ctx.Stacks - 1)),
                });
            }
            Visual(spec);
        }
    }

    /// <summary>"proj_size": {mult=1.3}.</summary>
    public sealed class ShotSizeEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Scaling;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.Size *= (float)Math.Pow(Ctx.P.GetFloat("mult", 1.3f), Ctx.Stacks);
            Visual(spec);
        }
    }

    /// <summary>"proj_volley": {shots=1, spread=12, damageScale=1}. Adds shots per volley; extra shots can reduce damage.</summary>
    public sealed class VolleyEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Shape;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.ShotsPerVolley += Ctx.P.GetInt("shots", 1) * Ctx.Stacks;
            spec.SpreadDegrees = Math.Max(spec.SpreadDegrees, Ctx.P.GetFloat("spread", 12f));
            spec.Damage *= Ctx.P.GetFloat("damageScale", 1f);
            Visual(spec);
        }
    }

    /// <summary>"proj_delivery": {delivery=Beam, priority=10}. Exclusive: the highest priority wins.</summary>
    public sealed class DeliveryEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Delivery;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            var d = Ctx.P.GetEnum("delivery", ProjectileDelivery.Bolt);
            if (spec.TrySetDelivery(d, Ctx.P.GetInt("priority", 10), Ctx.SourceId))
            {
                if (d == ProjectileDelivery.Beam && spec.PierceCount >= 0) spec.PierceCount = -1;
                if (d == ProjectileDelivery.Lobbed) spec.Spectral = true;
            }
            Visual(spec);
        }
    }

    /// <summary>"proj_spectral": shots pass through obstacles.</summary>
    public sealed class SpectralEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot) { spec.Spectral = true; Visual(spec); }
    }

    /// <summary>"proj_charge": {time=1, damageMult=3}. Shots must be charged; damage scales with charge fraction.</summary>
    public sealed class ChargeEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Shape;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            spec.ChargeTime = Math.Max(spec.ChargeTime, Ctx.P.GetFloat("time", 1f));
            float mult = Ctx.P.GetFloat("damageMult", 3f);
            spec.Damage *= 1f + (mult - 1f) * Math.Max(0f, Math.Min(1f, shot.ChargeFraction));
            Visual(spec);
        }
    }

    /// <summary>"proj_accelerate": {perSecond=6}. Shots speed up while flying.</summary>
    public sealed class AccelerateEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        { spec.AccelerationPerSecond += Ctx.P.GetFloat("perSecond", 6f) * Ctx.Stacks; Visual(spec); }
    }

    /// <summary>"proj_wavy": {amplitude=0.4}. Shots weave in a sine pattern.</summary>
    public sealed class WavyEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Behavior;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        { spec.SineAmplitude = Math.Max(spec.SineAmplitude, Ctx.P.GetFloat("amplitude", 0.4f)); Visual(spec); }
    }

    /// <summary>"proj_damage_mult": {mult=1.5}. Multiplies shot damage (separate from the Damage stat, e.g. for synergies).</summary>
    public sealed class ShotDamageMultEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Scaling;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        { spec.Damage *= (float)Math.Pow(Ctx.P.GetFloat("mult", 1.5f), Ctx.Stacks); Visual(spec); }
    }

    /// <summary>"proj_visual": {key}. Presentation-only key (synergy-specific trails, colours, audio).</summary>
    public sealed class ProjectileVisualEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Visual;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        { var k = Ctx.P.GetString("key"); if (k != null) spec.AddVisual(k); }
    }

    /// <summary>
    /// "proj_convert": {from (status or type), to, ...}. Rewrites an ingredient's output, e.g. turning
    /// burn statuses into a stronger "scorch" when two fire items combine. Runs at synergy order.
    /// Params: fromStatus, toStatus, potencyMult=1, durationMult=1.
    /// </summary>
    public sealed class ConvertStatusEffect : ProjectileModifierBase
    {
        protected override int DefaultOrder => ProjectileOrder.Synergy;
        public override void ModifyProjectile(ProjectileSpec spec, ShotContext shot)
        {
            string from = Ctx.P.GetString("fromStatus"), to = Ctx.P.GetString("toStatus");
            if (from == null || to == null) return;
            for (int i = 0; i < spec.Statuses.Count; i++)
            {
                var s = spec.Statuses[i];
                if (s.StatusId != from) continue;
                s.StatusId = to;
                s.Potency *= Ctx.P.GetFloat("potencyMult", 1f);
                s.Duration *= Ctx.P.GetFloat("durationMult", 1f);
                spec.Statuses[i] = s;
            }
            Visual(spec);
        }
    }
}
