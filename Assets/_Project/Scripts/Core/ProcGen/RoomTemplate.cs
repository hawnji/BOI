using System;
using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    public enum RoomType
    {
        Start = 0,
        Combat = 1,
        Treasure = 2,     // "Reliquary": item pedestal
        Shop = 3,         // "Bazaar"
        Boss = 4,
        Miniboss = 5,     // "Lair"
        Secret = 6,       // hidden, bomb to reveal; borders several rooms
        HiddenVault = 7,  // rarer secret: borders exactly one room
        Challenge = 8,    // "Gauntlet": waves for a reward, entry needs full health
        Event = 9,        // "Shrine": choices, risk/reward
        Reward = 10,      // "Vault": key-locked chest room
        Healing = 11,     // "Spring": restore health once
        Curse = 12,       // "Hex chamber": pay health to enter, better rewards
    }

    /// <summary>Tile legend for template layouts. Keep in sync with docs/ROOM_TEMPLATES.md.</summary>
    public static class TileCodes
    {
        public const char Floor = '.';
        public const char Void = ' ';          // outside an L-shaped room
        public const char Rock = '#';          // destructible by bombs, blocks movement and shots
        public const char Block = 'X';         // indestructible pillar
        public const char Pit = 'P';           // blocks walking (not flying), not shots
        public const char Spikes = 'S';        // hazard: damages walkers
        public const char Prop = 'B';          // destructible prop (barrel/crate/urn), may drop pickups
        public const char MarkedRock = 'T';    // rare rock with a guaranteed reward
        public const char Enemy = 'E';         // enemy spawn point
        public const char Pickup = 'C';        // pickup spawn point
        public const char Interact = 'I';      // pedestal / shop slot / shrine / chest
        public const char Fire = 'F';          // burning hazard, can be extinguished by shots
        public const char Water = 'W';         // slows movement
        public const char Explosive = 'K';     // explosive prop, chain reacts

        public const string All = ". #XPSBTECIFWK";

        /// <summary>Can the player walk on it (without bombs, without flight, avoiding hazards)?</summary>
        public static bool IsSafeWalkable(char c) =>
            c == Floor || c == Enemy || c == Pickup || c == Interact || c == Water;

        public static bool IsKnown(char c) => All.IndexOf(c) >= 0;
    }

    /// <summary>
    /// A handcrafted room layout. Authored as JSON text (Assets/_Project/Content/Rooms/*.json) and
    /// validated by <see cref="RoomTemplateValidator"/>. Procedural generation picks a template per
    /// room by shape/type/theme/doors and then populates its markers.
    /// </summary>
    public sealed class RoomTemplate
    {
        public string Id;
        public RoomShape Shape = RoomShape.Single;
        public RoomType[] Types = { RoomType.Combat };
        /// <summary>Floor theme ids this template suits; "*" = any.</summary>
        public string[] Themes = { "*" };
        /// <summary>1 (trivial) .. 10 (brutal). Selection keeps it within the floor's range.</summary>
        public int Difficulty = 1;
        public float Weight = 1f;
        public string[] Tags = Array.Empty<string>();
        /// <summary>Door sockets that are walled off in this layout (e.g. "0,0:N").</summary>
        public string[] BlockedDoors = Array.Empty<string>();
        /// <summary>Rows from north to south; each row is TileWidth characters.</summary>
        public string[] Layout = Array.Empty<string>();

        public int Width => RoomShapes.TileWidth(Shape);
        public int Height => RoomShapes.TileHeight(Shape);

        public char TileAt(int x, int y)
        {
            if (y < 0 || y >= Layout.Length) return TileCodes.Void;
            var row = Layout[y];
            return x >= 0 && x < row.Length ? row[x] : TileCodes.Void;
        }

        public bool AllowsType(RoomType t) { foreach (var x in Types) if (x == t) return true; return false; }

        public bool SuitsTheme(string theme)
        {
            foreach (var t in Themes) if (t == "*" || string.Equals(t, theme, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool HasTag(string tag) { foreach (var t in Tags) if (t == tag) return true; return false; }

        public bool IsDoorOpen(DoorSocket socket)
        {
            string id = socket.Id;
            foreach (var b in BlockedDoors) if (string.Equals(b, id, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public List<(int x, int y)> FindTiles(char code)
        {
            var list = new List<(int, int)>();
            for (int y = 0; y < Layout.Length; y++)
                for (int x = 0; x < Layout[y].Length; x++)
                    if (Layout[y][x] == code) list.Add((x, y));
            return list;
        }

        public int Count(char code)
        {
            int n = 0;
            foreach (var row in Layout) foreach (var c in row) if (c == code) n++;
            return n;
        }

        public override string ToString() => Id;
    }
}
