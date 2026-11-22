using System;
using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.ProcGen;

namespace Hollowcrawl.Rooms
{
    public enum DoorState { Open, ClosedCombat, Locked, Hidden, Sealed }

    public enum DoorUseResult { Passed, BlockedByCombat, NeedsKey, KeyUsed, Hidden, NeedsFullHealth, PaidHealth, Sealed }

    /// <summary>Runtime state of one room in the current floor (cleared, visited, doors).</summary>
    public sealed class RoomRuntime
    {
        public RoomNode Node;
        public bool Visited;
        public bool Cleared;
        public bool Revealed;          // shown on the map (adjacent to a visited room, or by map effects)
        public bool TookDamageHere;
        public int EnemiesAlive;
        public readonly Dictionary<RoomDoor, DoorState> Doors = new Dictionary<RoomDoor, DoorState>();
    }

    /// <summary>
    /// Floor-level rules for rooms and doors: combat locks doors until cleared, keys open locked
    /// doors once (both sides), secret doors open when revealed by explosions, challenge doors need
    /// full health, curse doors cost health. Pure logic driven by the Unity room manager.
    /// </summary>
    public sealed class FloorState
    {
        private readonly List<RoomRuntime> _rooms = new List<RoomRuntime>();

        public FloorLayout Layout { get; }
        public int CurrentRoom { get; private set; }
        public IReadOnlyList<RoomRuntime> Rooms => _rooms;
        public RoomRuntime Current => _rooms[CurrentRoom];

        public event Action<RoomRuntime> RoomEntered;
        public event Action<RoomRuntime> RoomCleared;
        public event Action<RoomDoor> DoorRevealed;

        public FloorState(FloorLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            foreach (var node in layout.Rooms)
            {
                var r = new RoomRuntime { Node = node, Cleared = !NeedsClearing(node), EnemiesAlive = node.Enemies.Count };
                foreach (var d in node.Doors)
                    r.Doors[d] = d.Kind == DoorKind.Secret ? DoorState.Hidden : d.Kind == DoorKind.Locked ? DoorState.Locked : DoorState.Open;
                _rooms.Add(r);
            }
            CurrentRoom = layout.StartRoom;
            Enter(layout.StartRoom);
        }

        private static bool NeedsClearing(RoomNode n) =>
            n.Type == RoomType.Combat || n.Type == RoomType.Boss || n.Type == RoomType.Miniboss || n.Type == RoomType.Challenge;

        /// <summary>Doors of the current room as the player sees them (combat closes open doors).</summary>
        public DoorState GetDoorState(RoomDoor door)
        {
            var room = _rooms[door.FromRoom];
            var s = room.Doors[door];
            if (s == DoorState.Open && !room.Cleared && room.Visited) return DoorState.ClosedCombat;
            return s;
        }

        /// <summary>Attempts to walk through a door of the current room.</summary>
        public DoorUseResult TryUseDoor(RoomDoor door, PlayerResources resources, HealthPool health)
        {
            if (door.FromRoom != CurrentRoom) throw new InvalidOperationException("Door does not belong to the current room.");
            var state = GetDoorState(door);
            switch (state)
            {
                case DoorState.ClosedCombat: return DoorUseResult.BlockedByCombat;
                case DoorState.Hidden: return DoorUseResult.Hidden;
                case DoorState.Sealed: return DoorUseResult.Sealed;
                case DoorState.Locked:
                    if (resources == null || !resources.TrySpend(ResourceKind.Keys, 1)) return DoorUseResult.NeedsKey;
                    SetBothSides(door, DoorState.Open);
                    return DoorUseResult.KeyUsed;
            }

            var target = _rooms[door.ToRoom];
            if (door.Kind == DoorKind.Challenge && !target.Visited && health != null && health.Vigor < health.MaxVigor)
                return DoorUseResult.NeedsFullHealth;
            if (door.Kind == DoorKind.Curse && !target.Visited && health != null && target.Node.Type == RoomType.Curse)
            {
                health.TakeDamage(1, 0f, null, ignoreInvulnerability: true);
                Enter(door.ToRoom);
                return DoorUseResult.PaidHealth;
            }
            Enter(door.ToRoom);
            return DoorUseResult.Passed;
        }

        /// <summary>An explosion touched a wall of the current room: reveal hidden doors on that side.</summary>
        public bool RevealSecretDoor(RoomDoor door)
        {
            if (_rooms[door.FromRoom].Doors[door] != DoorState.Hidden) return false;
            SetBothSides(door, DoorState.Open);
            DoorRevealed?.Invoke(door);
            return true;
        }

        public void NotifyEnemyKilled()
        {
            var r = Current;
            if (r.Cleared) return;
            r.EnemiesAlive = Math.Max(0, r.EnemiesAlive - 1);
            if (r.EnemiesAlive == 0) MarkCleared();
        }

        /// <summary>Extra enemies spawned mid-fight (summoners, waves) keep the room locked.</summary>
        public void NotifyEnemySpawned() { if (!Current.Cleared) Current.EnemiesAlive++; }

        public void MarkCleared()
        {
            var r = Current;
            if (r.Cleared) return;
            r.Cleared = true;
            RoomCleared?.Invoke(r);
        }

        public void TeleportTo(int roomIndex) => Enter(roomIndex);

        /// <summary>Reveals every room on the map (map items); secret rooms only when includeSecrets.</summary>
        public void RevealMap(bool includeSecrets)
        {
            foreach (var r in _rooms)
            {
                bool secret = r.Node.Type == RoomType.Secret || r.Node.Type == RoomType.HiddenVault;
                if (!secret || includeSecrets) r.Revealed = true;
            }
        }

        private void Enter(int roomIndex)
        {
            CurrentRoom = roomIndex;
            var room = _rooms[roomIndex];
            bool first = !room.Visited;
            room.Visited = true;
            room.Revealed = true;
            foreach (var d in room.Node.Doors)
                if (d.Kind != DoorKind.Secret) _rooms[d.ToRoom].Revealed = true;
            if (first && room.EnemiesAlive == 0 && NeedsClearing(room.Node)) MarkClearedSilently(room);
            RoomEntered?.Invoke(room);
        }

        private static void MarkClearedSilently(RoomRuntime r) => r.Cleared = true;

        private void SetBothSides(RoomDoor door, DoorState state)
        {
            _rooms[door.FromRoom].Doors[door] = state;
            var other = _rooms[door.ToRoom];
            foreach (var d in other.Node.Doors)
                if (d.ToRoom == door.FromRoom && d.Kind == door.Kind) other.Doors[d] = state;
        }
    }
}
