using System.Linq;
using Hollowcrawl.Audio;
using Hollowcrawl.Content;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class AudioAndAssetTests
    {
        [Test]
        public void MusicFollowsMenuFloorBossAndRunEnd()
        {
            var m = new MusicDirector();
            Assert.That(m.Update(new MusicContext { InMenu = true }), Is.True);
            Assert.That(m.Current, Is.EqualTo(MusicDirector.MenuTrack));
            Assert.That(m.Update(new MusicContext { InMenu = true }), Is.False, "no restart of the same track");
            m.Update(new MusicContext { InRun = true, FloorMusicKey = "music_ashen_crypt" });
            Assert.That(m.Current, Is.EqualTo("music_ashen_crypt"));
            m.Update(new MusicContext { InRun = true, FloorMusicKey = "music_ashen_crypt", BossMusicKey = "music_boss_early" });
            Assert.That(m.Current, Is.EqualTo("music_boss_early"));
            m.Update(new MusicContext { InRun = true, RunWon = true, EndingMusicKey = "music_ending" });
            Assert.That(m.Current, Is.EqualTo("music_ending"));
            m.Update(new MusicContext { InRun = true, RunLost = true });
            Assert.That(m.Current, Is.EqualTo(MusicDirector.DefeatTrack));
        }

        [Test]
        public void PauseDucksMusicWithoutChangingTrack()
        {
            var m = new MusicDirector();
            m.Update(new MusicContext { InRun = true, FloorMusicKey = "a" });
            Assert.That(m.Update(new MusicContext { InRun = true, FloorMusicKey = "a", Paused = true }), Is.False);
            Assert.That(m.Duck, Is.LessThan(1f));
        }

        [Test]
        public void LimiterSpacesRepeatsAndCapsVoicesPerFrame()
        {
            var l = new SfxLimiter { MinInterval = 0.05f, MaxNewVoicesPerFrame = 3 };
            Assert.That(l.TryPlay("hit", 1f), Is.True);
            Assert.That(l.TryPlay("hit", 1f), Is.False, "same key, same instant");
            Assert.That(l.TryPlay("a", 1f) && l.TryPlay("b", 1f), Is.True);
            Assert.That(l.TryPlay("c", 1f), Is.False, "voice cap reached this frame");
            Assert.That(l.TryPlay("hit", 1.1f), Is.True);
        }

        [Test]
        public void ManifestCoversEveryContentSpriteAndMusicKey()
        {
            var db = TestContent.Database;
            var m = AssetManifest.Build(db);
            foreach (var e in db.Enemies.Values) Assert.That(m.Sprites.ContainsKey(e.SpriteKey ?? e.Id), e.Id);
            foreach (var b in db.Bosses.Values) Assert.That(m.Sprites.ContainsKey(b.Body.SpriteKey ?? b.Id), b.Id);
            foreach (var f in db.Floors.Values) if (f.MusicKey != null) Assert.That(m.Audio.ContainsKey(f.MusicKey), f.Id);
            foreach (var c in db.Companions.Values) Assert.That(m.Sprites.ContainsKey(c.SpriteKey ?? "companion_" + c.Id), c.Id);
            Assert.That(m.Themes.Count, Is.EqualTo(db.Floors.Values.Select(f => f.Theme).Distinct().Count()));
            Assert.That(m.Sprites.Count, Is.GreaterThan(500));
        }

        /// <summary>Writes docs/ASSET_MANIFEST.md when HOLLOWCRAWL_WRITE_MANIFEST=1 (otherwise just checks the markdown builds).</summary>
        [Test]
        public void AssetManifestMarkdownBuilds()
        {
            string md = AssetManifest.ToMarkdown(AssetManifest.Build(TestContent.Database));
            Assert.That(md, Does.Contain("## Sprites").And.Contain("## Audio"));
            if (System.Environment.GetEnvironmentVariable("HOLLOWCRAWL_WRITE_MANIFEST") == "1")
            {
                var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestContent.ContentRoot, "..", "..", ".."));
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "docs", "ASSET_MANIFEST.md"), md);
            }
        }
    }
}
