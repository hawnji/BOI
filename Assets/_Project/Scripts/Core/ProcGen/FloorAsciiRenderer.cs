using System.Text;

namespace Hollowcrawl.ProcGen
{
    /// <summary>Text preview of a floor (used by the editor previewer, tests and debug overlay).</summary>
    public static class FloorAsciiRenderer
    {
        public static char Symbol(RoomType t)
        {
            switch (t)
            {
                case RoomType.Start: return 'S';
                case RoomType.Combat: return 'o';
                case RoomType.Treasure: return 'T';
                case RoomType.Shop: return '$';
                case RoomType.Boss: return 'B';
                case RoomType.Miniboss: return 'M';
                case RoomType.Secret: return '?';
                case RoomType.HiddenVault: return '!';
                case RoomType.Challenge: return 'C';
                case RoomType.Event: return 'E';
                case RoomType.Reward: return 'V';
                case RoomType.Healing: return 'H';
                case RoomType.Curse: return 'X';
                default: return '*';
            }
        }

        public static string Render(FloorLayout layout)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in layout.CellToRoom.Keys)
            {
                if (c.X < minX) minX = c.X; if (c.Y < minY) minY = c.Y;
                if (c.X > maxX) maxX = c.X; if (c.Y > maxY) maxY = c.Y;
            }
            var sb = new StringBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var room = layout.RoomAt(new Cell(x, y));
                    sb.Append(room == null ? ' ' : Symbol(room.Type));
                    // horizontal connection
                    var right = layout.RoomAt(new Cell(x + 1, y));
                    if (room != null && right != null)
                    {
                        if (right.Index == room.Index) sb.Append('=');
                        else
                        {
                            var door = room.Doors.Find(d => d.ToRoom == right.Index && room.Anchor.X + d.Socket.LocalCell.X == x && room.Anchor.Y + d.Socket.LocalCell.Y == y && d.Socket.Side == Direction.East);
                            sb.Append(door == null ? ' ' : (door.Kind == DoorKind.Secret ? ':' : '-'));
                        }
                    }
                    else sb.Append(' ');
                }
                sb.AppendLine();
                for (int x = minX; x <= maxX; x++)
                {
                    var room = layout.RoomAt(new Cell(x, y));
                    var below = layout.RoomAt(new Cell(x, y + 1));
                    if (room != null && below != null)
                    {
                        if (below.Index == room.Index) sb.Append('"');
                        else
                        {
                            var door = room.Doors.Find(d => d.ToRoom == below.Index && room.Anchor.X + d.Socket.LocalCell.X == x && room.Anchor.Y + d.Socket.LocalCell.Y == y && d.Socket.Side == Direction.South);
                            sb.Append(door == null ? ' ' : (door.Kind == DoorKind.Secret ? '.' : '|'));
                        }
                    }
                    else sb.Append(' ');
                    sb.Append(' ');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
