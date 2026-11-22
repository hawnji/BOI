using Hollowcrawl.Combat;

namespace Hollowcrawl.Items
{
    /// <summary>A live effect instance owned by an item, synergy, transformation, or other source.</summary>
    public interface IEffect
    {
        void OnAttach(EffectContext ctx);
        void OnDetach(EffectContext ctx);
    }

    /// <summary>Base class that keeps the context; override only what you need.</summary>
    public abstract class EffectBase : IEffect
    {
        protected EffectContext Ctx { get; private set; }
        public virtual void OnAttach(EffectContext ctx) { Ctx = ctx; }
        public virtual void OnDetach(EffectContext ctx) { }
    }

    // ---- Hooks. An effect implements any subset; the engine discovers them on attach. ----

    /// <summary>Modifies the player's (or a companion's) shot before spawning. Lower Order runs first.</summary>
    public interface IProjectileModifier
    {
        int Order { get; }
        void ModifyProjectile(ProjectileSpec spec, ShotContext shot);
    }

    public interface IOutgoingDamageModifier { void ModifyOutgoingDamage(ref DamageInfo damage, in HitContext hit); }
    public interface IIncomingDamageModifier { void ModifyIncomingDamage(ref DamageInfo damage); }

    public interface IOnHitHook { void OnHit(in HitContext hit); }
    public interface IOnKillHook { void OnKill(in KillContext kill); }
    public interface IOnPlayerDamagedHook { void OnPlayerDamaged(int halvesLost); }
    public interface IOnRoomEnteredHook { void OnRoomEntered(in RoomContext room); }
    public interface IOnRoomClearedHook { void OnRoomCleared(in RoomContext room); }
    public interface IOnFloorStartedHook { void OnFloorStarted(int floorIndex); }
    public interface IOnBombExplodedHook { void OnBombExploded(Vec2 position, BombSpec bomb); }
    public interface IBombModifier { void ModifyBomb(BombSpec bomb); }
    public interface IOnPickupHook { void OnPickup(PickupKind kind); }
    public interface IOnActiveUsedHook { void OnActiveUsed(string activeItemId); }
    public interface IShopPriceModifier { void ModifyPrice(ref int price, ItemDefinition item); }
    public interface IDropChanceModifier { void ModifyDropChance(ref float chance, PickupKind kind); }
    public interface ITickHook { void Tick(float dt); }

    /// <summary>Applied to companions' shots in addition to their own definition (for "companions inherit" synergies).</summary>
    public interface ICompanionShotModifier { void ModifyCompanionShot(ProjectileSpec spec, string companionId); }

    /// <summary>An effect executed when an active item is used. Return false if it could not be used (charge kept).</summary>
    public interface IActiveEffect { bool Use(ActiveUseContext use); }

    // ---- Contexts ----

    public struct ShotContext
    {
        public bool IsCompanion;
        public string CompanionId;
        public float ChargeFraction;   // 0..1 for charged shots
        public int VolleyIndex;
    }

    public struct HitContext
    {
        public int TargetId;
        public Vec2 Position;
        public float DamageDealt;
        public bool IsCritical;
        public bool TargetIsBoss;
        public bool Killed;
        public DamageType Types;
        /// <summary>The spec of the projectile that hit (null for melee/explosions).</summary>
        public ProjectileSpec Projectile;
        /// <summary>0 = direct player shot, 1+ = spawned by another effect. Spawning effects must check CanSpawnFrom.</summary>
        public int Generation;
        public bool FromCompanion;
    }

    public struct KillContext
    {
        public int TargetId;
        public string EnemyId;
        public Vec2 Position;
        public bool WasBoss;
        public DamageType KillingTypes;
        public int Generation;
    }

    public struct RoomContext
    {
        public string RoomType;
        public int RoomIndex;
        public bool FirstVisit;
        public bool TookDamage;
    }

    public struct ActiveUseContext
    {
        public string ItemId;
        public Vec2 Position;
        public Vec2 Aim;
        public int Stacks;
    }

    /// <summary>Data for a placed bomb, modifiable by items before it explodes.</summary>
    public sealed class BombSpec
    {
        public float Damage;
        public float Radius = 1.6f;
        public float Fuse = 1.5f;
        public DamageType Types = DamageType.Explosive;
        public bool Homing;
        public bool Sticky;
        public int ClusterCount;          // mini bombs spawned on explosion
        public string StatusId;           // status applied to enemies caught in the blast
        public float StatusDuration;
        public bool DamagesPlayer = true;
        public readonly System.Collections.Generic.List<string> VisualKeys = new System.Collections.Generic.List<string>(2);
    }
}
