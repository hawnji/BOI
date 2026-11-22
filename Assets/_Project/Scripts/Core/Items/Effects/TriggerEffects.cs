using System;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;

namespace Hollowcrawl.Items.Effects
{
    /// <summary>"onhit_explode": {chance=0.1, radius=1.2, damageScale=1}. Respects the spawn budget and generation limit.</summary>
    public sealed class OnHitExplodeEffect : EffectBase, IOnHitHook
    {
        public void OnHit(in HitContext hit)
        {
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 0.1f) * Ctx.Stacks)) return;
            if (!Ctx.CanSpawnFrom(hit.Generation)) return;
            float dmg = hit.DamageDealt * Ctx.P.GetFloat("damageScale", 1f);
            Ctx.World.SpawnExplosion(hit.Position, Ctx.P.GetFloat("radius", 1.2f), dmg, Team.Player, DamageType.Explosive | hit.Types);
        }
    }

    /// <summary>"onhit_status_aoe": {chance=0.2, radius=1.5, status, duration=3, potency=1}. Spreads a status around the hit.</summary>
    public sealed class OnHitStatusAoeEffect : EffectBase, IOnHitHook
    {
        public void OnHit(in HitContext hit)
        {
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 0.2f) * Ctx.Stacks)) return;
            var status = Ctx.P.GetString("status");
            if (status == null) return;
            Ctx.World.ApplyStatusToEnemiesInRadius(hit.Position, Ctx.P.GetFloat("radius", 1.5f), status,
                Ctx.P.GetFloat("duration", 3f), Ctx.P.GetFloat("potency", 1f));
        }
    }

    /// <summary>"onhit_burst": {chance=0.15, count=6, damageScale=0.5}. Releases a radial ring of child shots (generation +1).</summary>
    public sealed class OnHitBurstEffect : EffectBase, IOnHitHook
    {
        private readonly ProjectileSpec _child = new ProjectileSpec();

        public void OnHit(in HitContext hit)
        {
            if (hit.Projectile == null) return;
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 0.15f))) return;
            int count = Math.Max(1, Ctx.P.GetInt("count", 6));
            if (!Ctx.CanSpawnFrom(hit.Generation, count)) return;
            hit.Projectile.CopyTo(_child);
            _child.Generation = hit.Generation + 1;
            _child.Damage = hit.Projectile.Damage * Ctx.P.GetFloat("damageScale", 0.5f);
            _child.ShotsPerVolley = 1;
            _child.SplitCount = 0;
            _child.ChargeTime = 0f;
            _child.Size *= 0.7f;
            _child.Sanitize();
            float step = 360f / count;
            var dir = new Vec2(1f, 0f);
            for (int i = 0; i < count; i++) Ctx.World.SpawnProjectile(_child, hit.Position, dir.Rotated(step * i));
        }
    }

    /// <summary>"onkill_drop": {chance=0.05, pickup=Coin}.</summary>
    public sealed class OnKillDropEffect : EffectBase, IOnKillHook
    {
        public void OnKill(in KillContext kill)
        {
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 0.05f) * Ctx.Stacks)) return;
            Ctx.World.SpawnPickup(Ctx.P.GetEnum("pickup", PickupKind.Coin), kill.Position);
        }
    }

    /// <summary>"onkill_heal": {chance=0.05, halves=1}.</summary>
    public sealed class OnKillHealEffect : EffectBase, IOnKillHook
    {
        public void OnKill(in KillContext kill)
        {
            if (Ctx.Health == null || !Ctx.Roll(Ctx.P.GetFloat("chance", 0.05f) * Ctx.Stacks)) return;
            Ctx.Health.Heal(Ctx.P.GetInt("halves", 1));
        }
    }

    /// <summary>"onkill_explode": {chance=1, radius=1.4, damage=8}. Enemies burst on death (corpse explosions).</summary>
    public sealed class OnKillExplodeEffect : EffectBase, IOnKillHook
    {
        public void OnKill(in KillContext kill)
        {
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 1f))) return;
            if (!Ctx.CanSpawnFrom(kill.Generation)) return;
            float dmg = Ctx.P.GetFloat("damage", 8f) * Ctx.Stacks;
            Ctx.World.SpawnExplosion(kill.Position, Ctx.P.GetFloat("radius", 1.4f), dmg, Team.Player, kill.KillingTypes | DamageType.Explosive);
        }
    }

    /// <summary>"onkill_spread_status": {status, radius=2.5, duration=3, potency=1, requireStatus}. Death spreads a status (e.g. blight).</summary>
    public sealed class OnKillSpreadStatusEffect : EffectBase, IOnKillHook
    {
        public void OnKill(in KillContext kill)
        {
            var status = Ctx.P.GetString("status");
            if (status == null) return;
            var required = Ctx.P.GetEnum("requireType", DamageType.None);
            if (required != DamageType.None && (kill.KillingTypes & required) == 0) return;
            Ctx.World.ApplyStatusToEnemiesInRadius(kill.Position, Ctx.P.GetFloat("radius", 2.5f), status,
                Ctx.P.GetFloat("duration", 3f), Ctx.P.GetFloat("potency", 1f) * Ctx.Stacks);
        }
    }

    /// <summary>"ondamaged_burst": {radius=0 (room), damage=20, status}. Retaliation when the player is hurt.</summary>
    public sealed class OnDamagedBurstEffect : EffectBase, IOnPlayerDamagedHook
    {
        public void OnPlayerDamaged(int halvesLost)
        {
            if (halvesLost <= 0 || !Ctx.Roll(Ctx.P.GetFloat("chance", 1f))) return;
            var pos = Ctx.World.PlayerPosition;
            float radius = Ctx.P.GetFloat("radius", 0f);
            Ctx.World.DamageEnemiesInRadius(pos, radius, Ctx.P.GetFloat("damage", 20f) * Ctx.Stacks, Ctx.P.GetEnum("type", DamageType.Physical));
            var status = Ctx.P.GetString("status");
            if (status != null) Ctx.World.ApplyStatusToEnemiesInRadius(pos, radius, status, Ctx.P.GetFloat("duration", 3f), 1f);
            Ctx.World.PlayFeedback(Ctx.P.GetString("visual"), Ctx.P.GetString("audio"), pos);
        }
    }

    /// <summary>"ondamaged_ward": {chance=0.25, halves=1}. Chance to gain Ward when hurt.</summary>
    public sealed class OnDamagedWardEffect : EffectBase, IOnPlayerDamagedHook
    {
        public void OnPlayerDamaged(int halvesLost)
        {
            if (halvesLost <= 0 || Ctx.Health == null) return;
            if (Ctx.Roll(Ctx.P.GetFloat("chance", 0.25f) * Ctx.Stacks)) Ctx.Health.AddWard(Ctx.P.GetInt("halves", 1));
        }
    }

    /// <summary>"ondamaged_charge": {amount=1}. Taking damage charges the active item.</summary>
    public sealed class OnDamagedChargeEffect : EffectBase, IOnPlayerDamagedHook
    {
        public void OnPlayerDamaged(int halvesLost) { if (halvesLost > 0) Ctx.Engine.AddActiveCharge(Ctx.P.GetInt("amount", 1) * Ctx.Stacks); }
    }

    /// <summary>"roomclear_drop": {chance=0.2, pickup=Coin, requireNoDamage=false}.</summary>
    public sealed class RoomClearDropEffect : EffectBase, IOnRoomClearedHook
    {
        public void OnRoomCleared(in RoomContext room)
        {
            if (Ctx.P.GetBool("requireNoDamage") && room.TookDamage) return;
            if (!Ctx.Roll(Ctx.P.GetFloat("chance", 0.2f) * Ctx.Stacks)) return;
            Ctx.World.SpawnPickup(Ctx.P.GetEnum("pickup", PickupKind.Coin), Ctx.World.PlayerPosition);
        }
    }

    /// <summary>"roomclear_heal": {chance=1, halves=1}.</summary>
    public sealed class RoomClearHealEffect : EffectBase, IOnRoomClearedHook
    {
        public void OnRoomCleared(in RoomContext room)
        {
            if (Ctx.Health != null && Ctx.Roll(Ctx.P.GetFloat("chance", 1f))) Ctx.Health.Heal(Ctx.P.GetInt("halves", 1) * Ctx.Stacks);
        }
    }

    /// <summary>"roomclear_charge": {amount=1}. Extra active charge per cleared room.</summary>
    public sealed class RoomClearChargeEffect : EffectBase, IOnRoomClearedHook
    {
        public void OnRoomCleared(in RoomContext room) => Ctx.Engine.AddActiveCharge(Ctx.P.GetInt("amount", 1) * Ctx.Stacks);
    }

    /// <summary>"roomenter_status": {status, duration=4, radius=0}. Applies a status to every enemy when entering a fresh room.</summary>
    public sealed class RoomEnterStatusEffect : EffectBase, IOnRoomEnteredHook
    {
        public void OnRoomEntered(in RoomContext room)
        {
            if (!room.FirstVisit) return;
            var status = Ctx.P.GetString("status");
            if (status == null || !Ctx.Roll(Ctx.P.GetFloat("chance", 1f))) return;
            Ctx.World.ApplyStatusToEnemiesInRadius(Ctx.World.PlayerPosition, Ctx.P.GetFloat("radius", 0f), status, Ctx.P.GetFloat("duration", 4f), 1f);
        }
    }

    /// <summary>"floorstart_resource": {resource=Coins, amount=3}.</summary>
    public sealed class FloorStartResourceEffect : EffectBase, IOnFloorStartedHook
    {
        public void OnFloorStarted(int floorIndex) => Ctx.Resources.Add(Ctx.P.GetEnum("resource", ResourceKind.Coins), Ctx.P.GetInt("amount", 3) * Ctx.Stacks);
    }

    /// <summary>"bomb_mod": {radiusMult, damageAdd, damageMult, fuseMult, homing, sticky, cluster, status, statusDuration, safe}.</summary>
    public sealed class BombModEffect : EffectBase, IBombModifier
    {
        public void ModifyBomb(BombSpec bomb)
        {
            var p = Ctx.P;
            bomb.Radius *= p.GetFloat("radiusMult", 1f);
            bomb.Damage = (bomb.Damage + p.GetFloat("damageAdd", 0f) * Ctx.Stacks) * p.GetFloat("damageMult", 1f);
            bomb.Fuse *= p.GetFloat("fuseMult", 1f);
            bomb.Homing |= p.GetBool("homing");
            bomb.Sticky |= p.GetBool("sticky");
            bomb.ClusterCount += p.GetInt("cluster", 0) * Ctx.Stacks;
            if (p.GetBool("safe")) bomb.DamagesPlayer = false;
            var type = p.GetEnum("type", DamageType.None);
            bomb.Types |= type;
            var status = p.GetString("status");
            if (status != null) { bomb.StatusId = status; bomb.StatusDuration = Math.Max(bomb.StatusDuration, p.GetFloat("statusDuration", 3f)); }
            var v = p.GetString("visual");
            if (v != null && !bomb.VisualKeys.Contains(v)) bomb.VisualKeys.Add(v);
        }
    }

    /// <summary>"bomb_explode_shots": {count=8, damageScale=1}. Bomb blasts fire a ring of the player's current shots.</summary>
    public sealed class BombShotsEffect : EffectBase, IOnBombExplodedHook
    {
        private readonly ProjectileSpec _spec = new ProjectileSpec();
        public void OnBombExploded(Vec2 position, BombSpec bomb)
        {
            int count = Math.Max(1, Ctx.P.GetInt("count", 8));
            if (!Ctx.CanSpawnFrom(0, count)) return;
            Ctx.Engine.BuildShot(_spec, new ShotContext());
            _spec.Generation = 1;
            _spec.ShotsPerVolley = 1;
            _spec.ChargeTime = 0f;
            _spec.Damage *= Ctx.P.GetFloat("damageScale", 1f);
            float step = 360f / count;
            var dir = new Vec2(1f, 0f);
            for (int i = 0; i < count; i++) Ctx.World.SpawnProjectile(_spec, position, dir.Rotated(step * i));
        }
    }

    /// <summary>"pickup_bonus": {pickup=Coin, resource=Coins, amount=1, chance=0.25}. Chance to get extra when collecting a pickup.</summary>
    public sealed class PickupBonusEffect : EffectBase, IOnPickupHook
    {
        public void OnPickup(PickupKind kind)
        {
            if (kind != Ctx.P.GetEnum("pickup", PickupKind.Coin)) return;
            if (Ctx.Roll(Ctx.P.GetFloat("chance", 0.25f) * Ctx.Stacks))
                Ctx.Resources.Add(Ctx.P.GetEnum("resource", ResourceKind.Coins), Ctx.P.GetInt("amount", 1));
        }
    }

    /// <summary>"active_used_status": {status, radius=0, duration=3}. Using any active item also afflicts enemies.</summary>
    public sealed class ActiveUsedStatusEffect : EffectBase, IOnActiveUsedHook
    {
        public void OnActiveUsed(string activeItemId)
        {
            var status = Ctx.P.GetString("status");
            if (status != null) Ctx.World.ApplyStatusToEnemiesInRadius(Ctx.World.PlayerPosition, Ctx.P.GetFloat("radius", 0f), status, Ctx.P.GetFloat("duration", 3f), 1f);
        }
    }
}
