using System;
using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Content;
using Hollowcrawl.Enemies;
using Hollowcrawl.Save;
using Hollowcrawl.Simulation;

namespace Hollowcrawl.Progression
{
    /// <summary>What a finished run changed in the profile (for the run summary screen and toasts).</summary>
    public sealed class ProgressionResult
    {
        public readonly List<AchievementDefinition> NewAchievements = new List<AchievementDefinition>();
        public readonly List<string> NewUnlocks = new List<string>();
        public readonly List<string> NewMarks = new List<string>();
        public int Score;
        public bool Recorded;
    }

    /// <summary>
    /// Applies finished runs to a profile: lifetime statistics, discoveries, completion marks, achievements and
    /// unlocks. Pure logic; the caller saves the profile afterwards. Practice runs change nothing; custom-seed
    /// runs update statistics but cannot earn achievements, marks or unlocks.
    /// </summary>
    public static class ProgressionService
    {
        public const string MarkVictory = "victory";
        public const float SpeedRunSeconds = 20f * 60f;

        public static int Score(RunSimulation run)
        {
            var s = run.Stats;
            int score = s.Kills * 10 + s.BossKills * 500 + s.FloorsCompleted * 1000 + s.RoomsCleared * 25 + s.ItemsCollected * 50 + s.SecretsFound * 200;
            score -= s.DamageTakenHalves * 40;
            if (run.State == RunState.Won) score += 5000 + Math.Max(0, (int)(SpeedRunSeconds - run.RunTime)) * 2;
            return Math.Max(0, score);
        }

        /// <summary>Per-run flags usable by RunCondition achievements.</summary>
        public static HashSet<string> RunFlags(RunSimulation run)
        {
            var flags = new HashSet<string>(StringComparer.Ordinal);
            if (run.State != RunState.Won) return flags;
            if (run.Stats.DamageTakenHalves == 0) flags.Add("flawless");
            if (run.Stats.ItemsCollected == 0) flags.Add("no_items");
            if (run.RunTime <= SpeedRunSeconds) flags.Add("speed");
            if (run.Stats.ShrinesUsed == 0 && run.Stats.ConsumablesUsed == 0) flags.Add("ascetic");
            if (run.Setup.Difficulty.Level >= 2) flags.Add("hard");
            if (run.Setup.Difficulty.Level >= 3) flags.Add("nightmare");
            return flags;
        }

        public static ProgressionResult RecordRun(ProfileData profile, ContentDatabase content, RunSimulation run, DateTime utcNow)
        {
            var result = new ProgressionResult { Score = Score(run) };
            var mode = run.Setup.Mode;
            if (mode == GameMode.Practice || run.State == RunState.Playing) return result;
            result.Recorded = true;

            var st = profile.Stats;
            var rs = run.Stats;
            bool won = run.State == RunState.Won;
            string ch = run.Setup.CharacterId;

            st.TotalRuns++;
            if (won) { st.Wins++; st.CurrentStreak++; st.BestStreak = Math.Max(st.BestStreak, st.CurrentStreak); }
            else { st.Deaths++; st.CurrentStreak = 0; }
            st.EnemiesDefeated += rs.Kills;
            st.BossesDefeated += rs.BossKills;
            st.ItemsCollected += rs.ItemsCollected;
            st.FloorsCompleted += rs.FloorsCompleted;
            st.SecretsFound += rs.SecretsFound;
            st.BombsUsed += rs.BombsUsed;
            st.CoinsCollected += rs.CoinsCollected;
            st.DamageReceived += rs.DamageTakenHalves;
            st.SecondsPlayed += run.RunTime;
            st.DeepestFloor = Math.Max(st.DeepestFloor, run.FloorIndex + 1);
            Bump(st.RunsByCharacter, ch);
            if (won) Bump(st.WinsByCharacter, ch);
            foreach (var b in rs.BossesDefeated) Bump(st.BossKills, b);
            st.Increment("shrines_used", rs.ShrinesUsed);
            st.Increment("consumables_used", rs.ConsumablesUsed);
            st.Increment("rooms_cleared", rs.RoomsCleared);
            if (mode == GameMode.Challenge && won) st.ChallengeCompletions++;

            foreach (var id in rs.Items) profile.DiscoveredItems.Add(id);
            foreach (var id in run.Engine.DiscoveredSynergies) profile.DiscoveredSynergies.Add(id);
            foreach (var id in rs.Transformations) profile.DiscoveredTransformations.Add(id);
            foreach (var id in rs.EnemiesSeen) profile.SeenEnemies.Add(id);

            if (mode == GameMode.Daily)
            {
                string key = utcNow.ToString("yyyy-MM-dd");
                profile.DailyResults.TryGetValue(key, out int best);
                profile.DailyResults[key] = Math.Max(best, result.Score);
            }

            if (mode == GameMode.Custom) return result; // statistics only

            // completion marks: one per notable boss defeated, plus victory
            foreach (var b in rs.BossesDefeated)
                if (content.Bosses.TryGetValue(b, out var boss) && boss.Tier != BossTier.Miniboss && boss.Tier != BossTier.Early && profile.AddMark(ch, "boss:" + b))
                    result.NewMarks.Add("boss:" + b);
            if (won && profile.AddMark(ch, MarkVictory)) result.NewMarks.Add(MarkVictory);
            if (won && mode == GameMode.Challenge && run.Setup.ChallengeId != null && profile.ChallengesCompleted.Add(run.Setup.ChallengeId))
            {
                if (content.Challenges.TryGetValue(run.Setup.ChallengeId, out var chd) && chd.RewardUnlock != null && profile.Unlocks.Add(chd.RewardUnlock))
                    result.NewUnlocks.Add(chd.RewardUnlock);
            }

            EvaluateAchievements(profile, content, run, won, result, utcNow);
            return result;
        }

        /// <summary>Re-checks every achievement (also used after content updates to grant retroactive ones).</summary>
        public static void EvaluateAchievements(ProfileData profile, ContentDatabase content, RunSimulation run, bool won, ProgressionResult result, DateTime utcNow)
        {
            var flags = run != null ? RunFlags(run) : new HashSet<string>();
            // unlocks can satisfy other achievements (e.g. "win with every character"), so iterate to a fixed point
            for (int pass = 0; pass < 4; pass++)
            {
                bool changed = false;
                foreach (var a in content.Achievements.Values.OrderBy(a => a.Id, StringComparer.Ordinal))
                {
                    if (profile.Achievements.ContainsKey(a.Id) || !IsMet(a, profile, content, run, won, flags)) continue;
                    profile.Achievements[a.Id] = utcNow.Ticks;
                    result.NewAchievements.Add(a);
                    foreach (var u in a.Unlocks) if (profile.Unlocks.Add(u)) result.NewUnlocks.Add(u);
                    changed = true;
                }
                if (!changed) break;
            }
        }

        public static bool IsMet(AchievementDefinition a, ProfileData p, ContentDatabase content, RunSimulation run, bool won, HashSet<string> flags)
        {
            var st = p.Stats;
            switch (a.Trigger)
            {
                case AchievementTrigger.Counter: return Counter(st, a.Key) >= a.Target;
                case AchievementTrigger.BossDefeated: return st.BossKills.TryGetValue(a.Key, out long n) && n >= a.Target;
                case AchievementTrigger.WinWithCharacter: return st.WinsByCharacter.TryGetValue(a.Key, out long w) && w >= a.Target;
                case AchievementTrigger.ReachFloor: return st.DeepestFloor >= a.Target;
                case AchievementTrigger.Transformation: return p.DiscoveredTransformations.Contains(a.Key);
                case AchievementTrigger.ChallengeComplete: return p.ChallengesCompleted.Contains(a.Key);
                case AchievementTrigger.DiscoverItems: return p.DiscoveredItems.Count >= a.Target;
                case AchievementTrigger.DiscoverSynergies: return p.DiscoveredSynergies.Count >= a.Target;
                case AchievementTrigger.RunCondition: return won && flags.Contains(a.Key);
                case AchievementTrigger.AllCharacterWins:
                    return content.Characters.Count > 0 && content.Characters.Keys.All(c => st.WinsByCharacter.TryGetValue(c, out long x) && x > 0);
                default: return false;
            }
        }

        /// <summary>Lifetime counters by name: built-in statistics plus open-ended counters.</summary>
        public static long Counter(LifetimeStatistics st, string key)
        {
            switch (key)
            {
                case "runs": return st.TotalRuns;
                case "wins": return st.Wins;
                case "deaths": return st.Deaths;
                case "kills": return st.EnemiesDefeated;
                case "bosses": return st.BossesDefeated;
                case "items": return st.ItemsCollected;
                case "floors": return st.FloorsCompleted;
                case "secrets": return st.SecretsFound;
                case "bombs": return st.BombsUsed;
                case "coins": return st.CoinsCollected;
                case "challenges": return st.ChallengeCompletions;
                case "streak": return st.BestStreak;
                default: return st.Counters.TryGetValue(key, out long v) ? v : 0;
            }
        }

        private static void Bump(Dictionary<string, long> d, string key)
        {
            if (key == null) return;
            d.TryGetValue(key, out long v);
            d[key] = v + 1;
        }
    }
}
