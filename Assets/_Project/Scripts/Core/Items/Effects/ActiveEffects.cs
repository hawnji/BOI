using System;
using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Items.Effects
{
    /// <summary>"active_room_damage": {damage=40, damageStatScale=1, type, status, duration, radius=0}.</summary>
    public sealed class ActiveRoomDamageEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            float dmg = Ctx.P.GetFloat("damage", 40f) + Ctx.Stats[StatId.Damage] * Ctx.P.GetFloat("damageStatScale", 1f);
            float radius = Ctx.P.GetFloat("radius", 0f);
            Ctx.World.DamageEnemiesInRadius(use.Position, radius, dmg, Ctx.P.GetEnum("type", DamageType.Physical));
            var status = Ctx.P.GetString("status");
            if (status != null) Ctx.World.ApplyStatusToEnemiesInRadius(use.Position, radius, status, Ctx.P.GetFloat("duration", 3f), 1f);
            Ctx.World.PlayFeedback(Ctx.P.GetString("visual"), Ctx.P.GetString("audio"), use.Position);
            return true;
        }
    }

    /// <summary>"active_heal": {halves=2, ward=0}. Fails (keeps charge) if nothing would change.</summary>
    public sealed class ActiveHealEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            if (Ctx.Health == null) return false;
            int ward = Ctx.P.GetInt("ward", 0);
            int umbral = Ctx.P.GetInt("umbral", 0);
            int healed = Ctx.Health.Heal(Ctx.P.GetInt("halves", 2));
            if (ward > 0) Ctx.Health.AddWard(ward);
            if (umbral > 0) Ctx.Health.AddUmbral(umbral);
            return healed > 0 || ward > 0 || umbral > 0;
        }
    }

    /// <summary>
    /// "active_temp_stat": {stat, op=Flat, value, duration=0}. Temporary buff; duration 0 = until the
    /// player leaves the room.
    /// </summary>
    public sealed class ActiveTempStatEffect : EffectBase, IActiveEffect, IOnRoomEnteredHook, ITickHook
    {
        private readonly List<StatModifier> _mods = new List<StatModifier>();
        private float _remaining;

        public bool Use(ActiveUseContext use)
        {
            var mod = new StatModifier(Ctx.P.GetEnum("stat", StatId.Damage), Ctx.P.GetEnum("op", ModifierOp.Flat),
                Ctx.P.GetFloat("value", 1f), this, 0, null, Ctx.SourceId + " (temp)");
            if (!Ctx.Stats.Add(mod)) return false;
            _mods.Add(mod);
            _remaining = Ctx.P.GetFloat("duration", 0f);
            return true;
        }

        public void OnRoomEntered(in RoomContext room) { if (Ctx.P.GetFloat("duration", 0f) <= 0f) Clear(); }

        public void Tick(float dt)
        {
            if (_mods.Count == 0 || Ctx.P.GetFloat("duration", 0f) <= 0f) return;
            _remaining -= dt;
            if (_remaining <= 0f) Clear();
        }

        public override void OnDetach(EffectContext ctx) => Clear();

        private void Clear()
        {
            foreach (var m in _mods) Ctx.Stats.Remove(m);
            _mods.Clear();
        }
    }

    /// <summary>"active_projectile_ring": {count=12, damageScale=1.5}. Fires the player's current shot in a full ring.</summary>
    public sealed class ActiveProjectileRingEffect : EffectBase, IActiveEffect
    {
        private readonly ProjectileSpec _spec = new ProjectileSpec();
        public bool Use(ActiveUseContext use)
        {
            int count = Math.Max(1, Ctx.P.GetInt("count", 12));
            if (!Ctx.CanSpawnFrom(0, count)) return false;
            Ctx.Engine.BuildShot(_spec, new ShotContext());
            _spec.ShotsPerVolley = 1;
            _spec.ChargeTime = 0f;
            _spec.Damage *= Ctx.P.GetFloat("damageScale", 1.5f);
            float step = 360f / count;
            for (int i = 0; i < count; i++) Ctx.World.SpawnProjectile(_spec, use.Position, use.Aim.Rotated(step * i));
            return true;
        }
    }

    /// <summary>"active_teleport": {target=RandomRoom}.</summary>
    public sealed class ActiveTeleportEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use) { Ctx.World.TeleportPlayer(Ctx.P.GetEnum("target", TeleportTarget.RandomRoom)); return true; }
    }

    /// <summary>"active_spawn_pickups": {pickup=Bomb, count=1}.</summary>
    public sealed class ActiveSpawnPickupsEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            int n = Math.Max(1, Ctx.P.GetInt("count", 1));
            var kind = Ctx.P.GetEnum("pickup", PickupKind.Bomb);
            for (int i = 0; i < n; i++)
            {
                var offset = new Vec2(Ctx.Rng.Range(-1f, 1f), Ctx.Rng.Range(-1f, 1f));
                Ctx.World.SpawnPickup(kind, use.Position + offset);
            }
            return true;
        }
    }

    /// <summary>"active_companion_room": {companion, count=1}. Summons temporary companions until the room is left.</summary>
    public sealed class ActiveCompanionRoomEffect : EffectBase, IActiveEffect, IOnRoomEnteredHook
    {
        public bool Use(ActiveUseContext use)
        {
            var id = Ctx.P.GetString("companion");
            if (id == null) return false;
            int n = Math.Max(1, Ctx.P.GetInt("count", 1));
            for (int i = 0; i < n; i++) Ctx.World.SpawnCompanion(id, this);
            return true;
        }
        public void OnRoomEntered(in RoomContext room) => Ctx.World.RemoveCompanionsFromSource(this);
        public override void OnDetach(EffectContext ctx) => ctx.World?.RemoveCompanionsFromSource(this);
    }

    /// <summary>"active_explosions": {count=5, radius=1.6, damage=30}. Random explosions around the room (never on the player).</summary>
    public sealed class ActiveExplosionsEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            int n = Math.Max(1, Ctx.P.GetInt("count", 5));
            if (!Ctx.CanSpawnFrom(0, n)) return false;
            float r = Ctx.P.GetFloat("radius", 1.6f);
            for (int i = 0; i < n; i++)
            {
                var angle = Ctx.Rng.Range(0f, 360f);
                var dist = Ctx.Rng.Range(r + 0.5f, r + 4f);
                var pos = use.Position + new Vec2(1f, 0f).Rotated(angle) * dist;
                Ctx.World.SpawnExplosion(pos, r, Ctx.P.GetFloat("damage", 30f), Team.Player, DamageType.Explosive);
            }
            return true;
        }
    }
}

namespace Hollowcrawl.Items.Effects
{
    /// <summary>Optional world capabilities beyond <see cref="IGameWorld"/> (the run simulation implements them).</summary>
    public interface IRunWorld
    {
        void RevealMap(bool includeSecrets);
    }

    /// <summary>"active_perm_stat": {stat, op=Flat, value}. A permanent stat change each use (draughts, shrines).</summary>
    public sealed class ActivePermanentStatEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            var mod = new Stats.StatModifier(Ctx.P.GetEnum("stat", Stats.StatId.Damage), Ctx.P.GetEnum("op", Stats.ModifierOp.Flat),
                Ctx.P.GetFloat("value", 0.1f), Ctx.Engine, 0, null, Ctx.SourceId);
            return Ctx.Stats.Add(mod); // owned by the engine, so detaching this source never removes it
        }
    }

    /// <summary>"active_resource": {resource=Coins|Bombs|Keys|Embers, amount}. Amount may be negative (fails if not affordable).</summary>
    public sealed class ActiveResourceEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            var kind = Ctx.P.GetEnum("resource", Economy.ResourceKind.Coins);
            int amount = Ctx.P.GetInt("amount", 1);
            if (amount < 0) return Ctx.Resources.TrySpend(kind, -amount);
            Ctx.Resources.Add(kind, amount);
            return amount > 0;
        }
    }

    /// <summary>"active_hurt_self": {halves=1}. Ignores defense and invulnerability but never kills (leaves half a point).</summary>
    public sealed class ActiveHurtSelfEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            if (Ctx.Health == null) return false;
            int halves = Math.Min(Ctx.P.GetInt("halves", 1), Math.Max(0, Ctx.Health.TotalHalves - 1));
            if (halves <= 0) return true; // the curse fizzles harmlessly rather than killing
            Ctx.Health.TakeDamage(halves, 0f, null, ignoreInvulnerability: true);
            return true;
        }
    }

    /// <summary>"active_reveal_map": {secrets=false}. Reveals the floor map.</summary>
    public sealed class ActiveRevealMapEffect : EffectBase, IActiveEffect
    {
        public bool Use(ActiveUseContext use)
        {
            if (!(Ctx.World is IRunWorld w)) return false;
            w.RevealMap(Ctx.P.GetBool("secrets", false));
            return true;
        }
    }
}
