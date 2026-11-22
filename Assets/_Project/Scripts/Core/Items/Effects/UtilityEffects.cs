using System;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;

namespace Hollowcrawl.Items.Effects
{
    /// <summary>"grant_resource": {resource=Bombs, amount=5}. One-time on first acquisition.</summary>
    public sealed class GrantResourceEffect : EffectBase
    {
        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            if (ctx.IsFirstAcquisition) ctx.Resources.Add(ctx.P.GetEnum("resource", ResourceKind.Coins), ctx.P.GetInt("amount", 1));
        }
    }

    /// <summary>"grant_health": {containers=0, heal=0, ward=0, umbral=0}. Containers persist while held; the rest is one-time.</summary>
    public sealed class GrantHealthEffect : EffectBase
    {
        private Stats.StatModifier _containers;

        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            int containers = ctx.P.GetInt("containers", 0);
            if (containers != 0)
            {
                _containers = new Stats.StatModifier(Stats.StatId.MaxHealth, Stats.ModifierOp.Flat, containers * ctx.Stacks, ctx.Record,
                    0, new RecordStatCondition(ctx.Record), ctx.SourceId);
                ctx.Stats.Add(_containers);
                if (ctx.Health != null) ctx.Health.SetContainers((int)Math.Round(ctx.Stats[Stats.StatId.MaxHealth]), ctx.IsFirstAcquisition || ctx.Stacks > 1);
            }
            if (!ctx.IsFirstAcquisition || ctx.Health == null) return;
            ctx.Health.Heal(ctx.P.GetInt("heal", 0));
            int ward = ctx.P.GetInt("ward", 0); if (ward > 0) ctx.Health.AddWard(ward);
            int umbral = ctx.P.GetInt("umbral", 0); if (umbral > 0) ctx.Health.AddUmbral(umbral);
        }

        public override void OnDetach(EffectContext ctx)
        {
            if (_containers == null) return;
            ctx.Stats.Remove(_containers);
            _containers = null;
            if (ctx.Health != null) ctx.Health.SetContainers((int)Math.Round(ctx.Stats[Stats.StatId.MaxHealth]), false);
        }
    }

    /// <summary>"companion": {companion, count=1}. Spawns companions while held (count per stack).</summary>
    public sealed class CompanionEffect : EffectBase
    {
        public override void OnAttach(EffectContext ctx)
        {
            base.OnAttach(ctx);
            var id = ctx.P.GetString("companion");
            if (id == null || ctx.World == null) return;
            int n = Math.Max(1, ctx.P.GetInt("count", 1)) * ctx.Stacks;
            for (int i = 0; i < Math.Min(n, 8); i++) ctx.World.SpawnCompanion(id, this);
        }

        public override void OnDetach(EffectContext ctx) { ctx.World?.RemoveCompanionsFromSource(this); }
    }

    /// <summary>
    /// "companion_inherit": {scale=0.5, companionTag}. Companions' shots receive the player's projectile
    /// modifiers (homing, elements, explosions...) with damage scaled - the classic familiar synergy.
    /// </summary>
    public sealed class CompanionInheritEffect : EffectBase, ICompanionShotModifier
    {
        public void ModifyCompanionShot(ProjectileSpec spec, string companionId)
        {
            float baseDamage = spec.Damage;
            int volley = spec.ShotsPerVolley;
            var delivery = spec.Delivery;
            Ctx.Engine.ApplyPlayerProjectileModifiers(spec, new ShotContext { IsCompanion = true, CompanionId = companionId });
            // Companions keep their own firing shape and delivery; only behaviours/elements carry over.
            spec.ShotsPerVolley = volley;
            spec.ChargeTime = 0f;
            spec.Delivery = delivery;
            spec.Damage = baseDamage + (spec.Damage - baseDamage) * Ctx.P.GetFloat("scale", 0.5f);
        }
    }

    /// <summary>"companion_boost": {damageMult=1.5, sizeMult=1}. Strengthens all companion shots.</summary>
    public sealed class CompanionBoostEffect : EffectBase, ICompanionShotModifier
    {
        public void ModifyCompanionShot(ProjectileSpec spec, string companionId)
        {
            spec.Damage *= (float)Math.Pow(Ctx.P.GetFloat("damageMult", 1.5f), Ctx.Stacks);
            spec.Size *= Ctx.P.GetFloat("sizeMult", 1f);
        }
    }

    /// <summary>"shop_discount": {percent=0.25, flat=0}. Discounts stack multiplicatively.</summary>
    public sealed class ShopDiscountEffect : EffectBase, IShopPriceModifier
    {
        public void ModifyPrice(ref int price, ItemDefinition item)
        {
            float p = price * (float)Math.Pow(1f - Ctx.P.GetFloat("percent", 0.25f), Ctx.Stacks) - Ctx.P.GetInt("flat", 0);
            price = (int)Math.Ceiling(p);
        }
    }

    /// <summary>"drop_chance": {pickup (optional), mult=1.25, add=0}.</summary>
    public sealed class DropChanceEffect : EffectBase, IDropChanceModifier
    {
        public void ModifyDropChance(ref float chance, PickupKind kind)
        {
            var only = Ctx.P.GetString("pickup");
            if (only != null && !string.Equals(only, kind.ToString(), StringComparison.OrdinalIgnoreCase)) return;
            chance = chance * (float)Math.Pow(Ctx.P.GetFloat("mult", 1.25f), Ctx.Stacks) + Ctx.P.GetFloat("add", 0f);
        }
    }

    /// <summary>"incoming_immunity": {types=Explosive}. Negates damage of the listed types.</summary>
    public sealed class IncomingImmunityEffect : EffectBase, IIncomingDamageModifier
    {
        public void ModifyIncomingDamage(ref DamageInfo damage)
        {
            var types = Ctx.P.GetEnum("types", DamageType.None);
            if ((damage.Types & types) != 0) damage.Cancelled = true;
        }
    }

    /// <summary>"incoming_block_chance": {chance=0.1}. Chance (luck-scaled) to ignore a hit.</summary>
    public sealed class IncomingBlockEffect : EffectBase, IIncomingDamageModifier
    {
        public void ModifyIncomingDamage(ref DamageInfo damage)
        {
            if (!damage.Cancelled && Ctx.Roll(Ctx.P.GetFloat("chance", 0.1f) * Ctx.Stacks, 0.01f)) damage.Cancelled = true;
        }
    }

    /// <summary>"outgoing_mult": {mult=1.5, vsBoss=false, types (any of), crit=false}. Conditional damage multipliers.</summary>
    public sealed class OutgoingMultEffect : EffectBase, IOutgoingDamageModifier
    {
        public void ModifyOutgoingDamage(ref DamageInfo damage, in HitContext hit)
        {
            if (Ctx.P.GetBool("vsBoss") && !hit.TargetIsBoss) return;
            if (Ctx.P.GetBool("crit") && !damage.IsCritical) return;
            var types = Ctx.P.GetEnum("types", DamageType.None);
            if (types != DamageType.None && (damage.Types & types) == 0) return;
            damage.Amount *= (float)Math.Pow(Ctx.P.GetFloat("mult", 1.5f), Ctx.Stacks);
        }
    }

    /// <summary>"regen": {interval=30, halves=1, inCombatOnly=false}. Heals over time.</summary>
    public sealed class RegenEffect : EffectBase, ITickHook
    {
        private float _timer;
        public void Tick(float dt)
        {
            if (Ctx.Health == null) return;
            if (Ctx.P.GetBool("inCombatOnly") && !Ctx.Engine.InCombat) return;
            _timer += dt * Ctx.Stacks;
            float interval = Math.Max(1f, Ctx.P.GetFloat("interval", 30f));
            if (_timer < interval) return;
            _timer -= interval;
            Ctx.Health.Heal(Ctx.P.GetInt("halves", 1));
        }
    }

    /// <summary>"aura_status": {radius=1.8, status, interval=1, duration=1.5, potency=1}. Periodic status aura around the player.</summary>
    public sealed class AuraStatusEffect : EffectBase, ITickHook
    {
        private float _timer;
        public void Tick(float dt)
        {
            if (!Ctx.Engine.InCombat) return;
            _timer += dt;
            float interval = Math.Max(0.2f, Ctx.P.GetFloat("interval", 1f));
            if (_timer < interval) return;
            _timer = 0f;
            var status = Ctx.P.GetString("status");
            if (status == null) return;
            Ctx.World.ApplyStatusToEnemiesInRadius(Ctx.World.PlayerPosition, Ctx.P.GetFloat("radius", 1.8f) * (1f + 0.25f * (Ctx.Stacks - 1)),
                status, Ctx.P.GetFloat("duration", 1.5f), Ctx.P.GetFloat("potency", 1f));
        }
    }

    /// <summary>"trinket_slots": {add=1}. Extra trinket capacity while held.</summary>
    public sealed class TrinketSlotsEffect : EffectBase
    {
        private int _added;
        public override void OnAttach(EffectContext ctx) { base.OnAttach(ctx); _added = ctx.P.GetInt("add", 1); ctx.Engine.TrinketCapacity += _added; }
        public override void OnDetach(EffectContext ctx) { ctx.Engine.TrinketCapacity = Math.Max(1, ctx.Engine.TrinketCapacity - _added); }
    }

    /// <summary>"invuln_on_hit": {seconds=0.5}. Extends the post-hit invulnerability window.</summary>
    public sealed class InvulnOnHitEffect : EffectBase, IOnPlayerDamagedHook
    {
        public void OnPlayerDamaged(int halvesLost) => Ctx.Health?.GrantInvulnerability(Ctx.Health.HitInvulnerability + Ctx.P.GetFloat("seconds", 0.5f) * Ctx.Stacks);
    }
}
