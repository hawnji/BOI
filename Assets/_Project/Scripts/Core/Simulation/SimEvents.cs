using System.Collections.Generic;
using Hollowcrawl.Items;

namespace Hollowcrawl.Simulation
{
    public enum SimEventKind
    {
        EnemyHit, EnemyKilled, BossPhaseStarted, EnemyTelegraph, EnemyAttack,
        PlayerHurt, PlayerHealed, PlayerDied, PlayerBlocked,
        ShotFired, ShotImpact, Explosion, TileChanged, DoorChanged, SecretRevealed,
        PickupCollected, ItemCollected, PurchaseFailed, RoomCleared, RoomEntered, FloorStarted,
        Feedback, ActiveUsed, RunWon, RunLost, ConsumableUsed, ConsumableIdentified, ShrineUsed, HexApplied,
    }

    /// <summary>A presentation cue (sound, particles, screen shake, HUD popups). Gameplay never depends on these.</summary>
    public struct SimEvent
    {
        public SimEventKind Kind;
        public int ActorId;
        public Vec2 Position;
        public float Value;
        public string Key;
        public string AudioKey;
        public int TileX, TileRow;
        public bool Flag;
    }

    /// <summary>Per-frame event buffer. Presentation drains it after each step.</summary>
    public sealed class SimEventQueue
    {
        private readonly List<SimEvent> _events = new List<SimEvent>(64);
        public const int Capacity = 512;
        public IReadOnlyList<SimEvent> Events => _events;
        public void Add(SimEvent e) { if (_events.Count < Capacity) _events.Add(e); }
        public void Clear() => _events.Clear();
    }
}
