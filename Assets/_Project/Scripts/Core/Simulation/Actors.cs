using Hollowcrawl.Combat;
using Hollowcrawl.Enemies;
using Hollowcrawl.Items;
using Hollowcrawl.Movement;
using Hollowcrawl.ProcGen;

namespace Hollowcrawl.Simulation
{
    public enum ActorKind { Player, Enemy, PlayerShot, EnemyShot, Pickup, Bomb, Hazard, Companion, Exit, Shrine }

    /// <summary>Anything that lives in a room. Ids are unique per run so presentation can pool views by id.</summary>
    public abstract class Actor
    {
        public int Id;
        public Vec2 Position;
        public float Radius = 0.3f;
        public bool Removed;
        public abstract ActorKind Kind { get; }
        /// <summary>Visual key for the presentation layer (sprite / prefab lookup).</summary>
        public string ViewKey;
    }

    public sealed class PlayerActor : Actor
    {
        public override ActorKind Kind => ActorKind.Player;
        public readonly MovementModel Movement;
        public readonly FireController Fire = new FireController();
        public Vec2 Aim = new Vec2(0f, -1f);
        public bool Flying;
        public float ChargeFraction => Fire.ChargeFraction;
        public PlayerActor(MovementTuning tuning) { Movement = new MovementModel(tuning); Radius = 0.32f; ViewKey = "player"; }
    }

    public sealed class EnemyActor : Actor, IEnemySensors
    {
        public override ActorKind Kind => ActorKind.Enemy;
        public EnemyDefinition Definition;
        public BossDefinition Boss;
        public BossController BossController;
        public EnemyBrain Brain;
        public EnemyScaling Scaling;
        public StatusController Status;
        public float Health;
        public float MaxHealth;
        public bool Elite;
        /// <summary>Counts toward the room's clear condition (false for some summoned fodder? kept true by default).</summary>
        public bool CountsForClear = true;
        public Vec2 Knockback;
        public Vec2 Velocity;
        public string SummonedBy;
        public float FlashTimer;
        public EnemyIntent LastIntent;
        internal RoomSimulation Room;

        public bool IsBoss => Boss != null;
        public bool Flying => Definition.Movement != null && Definition.Movement.Flying;

        // IEnemySensors
        public Vec2 SelfPosition => Position;
        public Vec2 PlayerPosition => Room.Player.Position;
        public bool HasLineOfSight => Room.Grid.LineOfSight(Position, Room.Player.Position);
        public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public bool CanAttack => Status.CanAttack;
        public float MoveMultiplier => Status.MoveMultiplier;
        public int AliveSummons(string enemyId) => Room.CountAlive(enemyId, Id);
    }

    public sealed class ShotActor : Actor
    {
        public override ActorKind Kind => Team == Team.Player ? ActorKind.PlayerShot : ActorKind.EnemyShot;
        public Team Team;
        /// <summary>Player and companion shots run the full projectile simulation.</summary>
        public ProjectileSimulation Sim;
        public bool FromCompanion;
        // simple enemy shot
        public Vec2 Velocity;
        public int Damage = 1;
        public float RangeLeft = 12f;
        public bool Spectral;
    }

    public sealed class PickupActor : Actor
    {
        public override ActorKind Kind => ActorKind.Pickup;
        public PickupKind Pickup;
        public string ContentId;      // item/trinket id for pedestals
        public int Price;             // > 0 in shops
        public bool Pedestal;         // stays in place, collected by touching
        public bool Veiled;           // hex: contents hidden until collected
        public float SpawnProtection = 0.35f; // can't be collected the instant it appears
        public bool CanCollect => SpawnProtection <= 0f;
    }

    public sealed class BombActor : Actor
    {
        public override ActorKind Kind => ActorKind.Bomb;
        public BombSpec Spec;
        public float Fuse;
        public bool PlacedByPlayer;
    }

    public sealed class HazardActor : Actor
    {
        public override ActorKind Kind => ActorKind.Hazard;
        public string HazardId;
        public float Remaining;
        public int Damage = 1;
        public bool HurtsPlayer = true;
    }

    public sealed class CompanionActor : Actor
    {
        public override ActorKind Kind => ActorKind.Companion;
        public string CompanionId;
        public object Source;
        public float Cooldown;
        public float OrbitAngle;
        public float FireInterval = 1.2f;
    }

    /// <summary>One option of an event-room shrine. Touching it pays the cost and rolls an outcome.</summary>
    public sealed class ShrineActor : Actor
    {
        public override ActorKind Kind => ActorKind.Shrine;
        public Consumables.ShrineDefinition Definition;
        public int OptionIndex;
        public int Uses;
        public float Cooldown;
        public Consumables.ShrineOption Option => Definition.Options[OptionIndex];
    }

    /// <summary>The trapdoor / portal that appears after a boss.</summary>
    public sealed class ExitActor : Actor
    {
        public override ActorKind Kind => ActorKind.Exit;
        public bool Active = true;
        public bool IsVictory;
    }
}
