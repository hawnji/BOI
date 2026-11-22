using System;
using System.Collections.Generic;
using Hollowcrawl.Simulation;

namespace Hollowcrawl.Content
{
    /// <summary>
    /// Every art and audio key the game will ask for, gathered from content plus the built-in presentation keys.
    /// Artists drop files at Resources/Sprites/&lt;key&gt; and Resources/Audio/&lt;key&gt;; anything missing falls back
    /// to a generated placeholder shape or silence. Used by the asset manifest export and its tests.
    /// </summary>
    public static class AssetManifest
    {
        public static readonly string[] BuiltInSprites =
        {
            "player", "shot_player", "shot_enemy", "bomb", "bomb_companion", "pedestal", "exit_trapdoor", "exit_victory",
            "pickup_coin", "pickup_bomb", "pickup_key", "pickup_vigorhalf", "pickup_vigorfull", "pickup_ward", "pickup_umbral", "pickup_ember",
            "pickup_consumable", "pickup_trinket", "pickup_item",
        };

        /// <summary>Tile art: Resources/Tiles/&lt;theme&gt;/&lt;kind&gt; or Resources/Tiles/&lt;kind&gt; (any square size, resampled to 64x64; Read/Write enabled).</summary>
        public static readonly string[] TileKinds = { "floor", "rock", "block", "pit", "spikes", "prop", "marked_rock", "fire", "water", "explosive", "wall", "door_open", "door_closed" };

        public static readonly string[] BuiltInAudio =
        {
            "sfx_shot", "sfx_hit", "sfx_enemy_death", "sfx_player_hurt", "sfx_explosion", "sfx_pickup", "sfx_item", "sfx_door", "sfx_secret",
            "sfx_room_clear", "sfx_boss_phase", "sfx_active", "sfx_consumable", "sfx_shrine", "sfx_hex", "sfx_room_modifier", "sfx_purchase_fail",
            "ui_move", "ui_select", "ui_back",
            "music_boss", Audio.MusicDirector.MenuTrack, Audio.MusicDirector.DefeatTrack, Audio.MusicDirector.DefaultVictoryTrack, Audio.MusicDirector.DefaultFloorTrack,
        };

        public sealed class Manifest
        {
            public readonly SortedDictionary<string, string> Sprites = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, string> Audio = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly SortedSet<string> Themes = new SortedSet<string>(StringComparer.Ordinal);
        }

        public static Manifest Build(ContentDatabase db)
        {
            var m = new Manifest();
            void S(string key, string use) { if (!string.IsNullOrEmpty(key) && !m.Sprites.ContainsKey(key)) m.Sprites[key] = use; }
            void A(string key, string use) { if (!string.IsNullOrEmpty(key) && !m.Audio.ContainsKey(key)) m.Audio[key] = use; }

            foreach (var k in BuiltInSprites) S(k, "built-in");
            foreach (var k in BuiltInAudio) A(k, "built-in");
            foreach (var i in db.Items.Values) S(i.IconKey ?? "item_" + i.Id, "item icon: " + i.Name);
            foreach (var e in db.Enemies.Values)
            {
                S(e.SpriteKey ?? e.Id, "enemy: " + e.Name);
                foreach (var a in e.Attacks) { S(a.TelegraphKey, "telegraph"); A(a.AudioKey, "enemy attack"); }
            }
            foreach (var b in db.Bosses.Values)
            {
                S(b.Body?.SpriteKey ?? b.Id, (b.Tier == Enemies.BossTier.Miniboss ? "miniboss: " : "boss: ") + b.Name);
                A(b.MusicKey, "boss music"); S(b.IntroKey, "boss intro card"); A(b.DeathKey, "boss death");
                foreach (var p in b.Phases) { S(p.TransitionKey, "boss phase transition"); foreach (var a in p.Attacks) { S(a.TelegraphKey, "telegraph"); A(a.AudioKey, "boss attack"); } }
            }
            foreach (var f in db.Floors.Values) { A(f.MusicKey, "floor music: " + f.Name); A(f.AmbienceKey, "floor ambience: " + f.Name); if (f.Theme != null) m.Themes.Add(f.Theme); }
            foreach (var sh in db.Shrines.Values) S(sh.SpriteKey ?? "shrine_" + sh.Id, "shrine: " + sh.Name);
            foreach (var c in db.Companions.Values) S(c.SpriteKey ?? "companion_" + c.Id, "companion: " + c.Name);
            foreach (var c in db.Characters.Values) S("character_" + c.Id, "character: " + c.Name);
            foreach (var t in db.Transformations.Values) S(t.VisualKey, "transformation: " + t.Name);
            foreach (var s in db.Synergies.Values) S(s.VisualKey, "synergy: " + s.Name);
            foreach (var h in db.Hexes.Values) S("hex_" + h.Id, "hex icon: " + h.Name);
            foreach (var m2 in db.RoomModifiers.Values) S(m2.IconKey, "room modifier icon: " + m2.Name);
            foreach (var e in db.Endings.Values) { S(e.ArtKey, "ending art: " + e.Name); A(e.MusicKey, "ending music"); }
            foreach (var a in db.Achievements.Values) S(a.IconKey ?? "achievement_" + a.Id, "achievement icon");
            foreach (var h in new[] { "fire", "spikes", "acid" }) S("hazard_" + h, "hazard");
            return m;
        }
    
        /// <summary>Markdown listing of every key, for artists and audio designers (docs/ASSET_MANIFEST.md).</summary>
        public static string ToMarkdown(Manifest m)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Asset manifest");
            sb.AppendLine();
            sb.AppendLine("Generated from content by `AssetManifest` (menu Hollowcrawl > Export Asset Manifest, or the");
            sb.AppendLine("`HOLLOWCRAWL_WRITE_MANIFEST=1 dotnet test Tests/CoreTests --filter AssetManifest` test). Do not edit by hand.");
            sb.AppendLine();
            sb.AppendLine("Drop files at these paths; anything missing falls back to a generated shape or silence:");
            sb.AppendLine();
            sb.AppendLine("- Sprites: `Assets/Resources/Sprites/<key>.png` (square, pivot centre; the image width is drawn as one world unit).");
            sb.AppendLine("- Tiles: `Assets/Resources/Tiles/<theme>/<kind>.png` or `Assets/Resources/Tiles/<kind>.png`, square (64x64), Read/Write enabled.");
            sb.AppendLine("- Most of these are rendered from procedural Blender models by `Tools/ArtGen` (see `docs/ART_PIPELINE.md`).");
            sb.AppendLine("- Audio: `Assets/Resources/Audio/<key>.ogg` (music loops; effects one-shot).");
            sb.AppendLine();
            sb.AppendLine($"## Tiles ({TileKinds.Length} kinds x {m.Themes.Count} themes)");
            sb.AppendLine();
            sb.AppendLine("Kinds: " + string.Join(", ", TileKinds) + ".");
            sb.AppendLine();
            sb.AppendLine("Themes: " + string.Join(", ", m.Themes) + ".");
            sb.AppendLine();
            sb.AppendLine($"## Sprites ({m.Sprites.Count})");
            sb.AppendLine();
            sb.AppendLine("| Key | Used for |");
            sb.AppendLine("|---|---|");
            foreach (var kv in m.Sprites) sb.AppendLine($"| `{kv.Key}` | {kv.Value.Replace("|", "/")} |");
            sb.AppendLine();
            sb.AppendLine($"## Audio ({m.Audio.Count})");
            sb.AppendLine();
            sb.AppendLine("| Key | Used for |");
            sb.AppendLine("|---|---|");
            foreach (var kv in m.Audio) sb.AppendLine($"| `{kv.Key}` | {kv.Value.Replace("|", "/")} |");
            return sb.ToString();
        }
    }
}
