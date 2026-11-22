using System;
using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;

namespace Hollowcrawl.Content
{
    /// <summary>Top-level shape of a content JSON file. Any section may be omitted.</summary>
    public sealed class ContentFile
    {
        public List<ItemDefinition> Items;
        public List<SynergyDefinition> Synergies;
        public List<TransformationDefinition> Transformations;
        public List<StatusEffectDefinition> Statuses;
        public List<RoomTemplate> Rooms;
        public List<FloorDefinition> Floors;
        public List<Enemies.EnemyDefinition> Enemies;
        public List<Enemies.BossDefinition> Bosses;
    }

    /// <summary>
    /// All loaded game content, keyed by id. Built from JSON text so the same data loads in the Unity
    /// runtime (TextAssets), the editor tools and headless tests. Content files are additive, which
    /// also leaves room for future expansions/mods to append files.
    /// </summary>
    public sealed class ContentDatabase
    {
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, SynergyDefinition> Synergies = new Dictionary<string, SynergyDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, TransformationDefinition> Transformations = new Dictionary<string, TransformationDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, StatusEffectDefinition> Statuses = new Dictionary<string, StatusEffectDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, FloorDefinition> Floors = new Dictionary<string, FloorDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Enemies.EnemyDefinition> Enemies = new Dictionary<string, Enemies.EnemyDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Enemies.BossDefinition> Bosses = new Dictionary<string, Enemies.BossDefinition>(StringComparer.Ordinal);
        public readonly List<RoomTemplate> RoomTemplates = new List<RoomTemplate>();
        public readonly List<string> LoadErrors = new List<string>();

        public void LoadJson(string sourceName, string json)
        {
            ContentFile file;
            try { file = JsonContent.Deserialize<ContentFile>(json); }
            catch (Exception ex) { LoadErrors.Add($"{sourceName}: invalid JSON ({ex.Message})"); return; }
            if (file == null) { LoadErrors.Add($"{sourceName}: empty."); return; }
            AddAll(sourceName, file.Items, Items, x => x.Id);
            AddAll(sourceName, file.Synergies, Synergies, x => x.Id);
            AddAll(sourceName, file.Transformations, Transformations, x => x.Id);
            AddAll(sourceName, file.Statuses, Statuses, x => x.Id);
            AddAll(sourceName, file.Floors, Floors, x => x.Id);
            AddAll(sourceName, file.Enemies, Enemies, x => x.Id);
            AddAll(sourceName, file.Bosses, Bosses, x => x.Id);
            if (file.Rooms != null) RoomTemplates.AddRange(file.Rooms);
        }

        private void AddAll<T>(string source, List<T> list, Dictionary<string, T> into, Func<T, string> id)
        {
            if (list == null) return;
            foreach (var x in list)
            {
                string key = id(x);
                if (string.IsNullOrEmpty(key)) { LoadErrors.Add($"{source}: {typeof(T).Name} without id."); continue; }
                if (into.ContainsKey(key)) { LoadErrors.Add($"{source}: duplicate {typeof(T).Name} id '{key}'."); continue; }
                into[key] = x;
            }
        }

        public RoomTemplateLibrary BuildRoomLibrary(out ValidationReport report)
        {
            var lib = new RoomTemplateLibrary();
            report = lib.AddRange(RoomTemplates);
            return lib;
        }

        public int CountItems(ItemKind kind)
        {
            int n = 0;
            foreach (var i in Items.Values) if (i.Kind == kind) n++;
            return n;
        }
    }

    /// <summary>Cross-reference validation for all content. Run by the editor menu and the test suite.</summary>
    public static class ContentValidator
    {
        public static ValidationReport Validate(ContentDatabase db, EffectRegistry effects, ConditionRegistry conditions)
        {
            var r = new ValidationReport();
            foreach (var e in db.LoadErrors) r.Error(e);

            foreach (var item in db.Items.Values)
            {
                string id = "item " + item.Id;
                if (string.IsNullOrWhiteSpace(item.Name)) r.Error($"{id}: missing name.");
                if (string.IsNullOrWhiteSpace(item.Description)) r.Error($"{id}: missing description.");
                if (item.Pools == null || item.Pools.Length == 0) r.Warn($"{id}: in no item pool (can never drop).");
                if (item.Kind == ItemKind.Active && (item.Active == null || item.Active.OnUse.Count == 0)) r.Error($"{id}: active item without on-use effects.");
                if (item.Kind != ItemKind.Active && item.Active != null) r.Error($"{id}: only active items may define 'active'.");
                if (item.Kind != ItemKind.Active && (item.Effects == null || item.Effects.Count == 0)) r.Error($"{id}: no effects (items must do something).");
                if (item.Stacking == ItemStacking.StackCapped && item.MaxStacks < 2) r.Error($"{id}: StackCapped needs maxStacks >= 2.");
                CheckEffects(item.Effects, id, effects, conditions, db, r);
                if (item.Active != null) CheckEffects(item.Active.OnUse, id + " (on use)", effects, conditions, db, r);
            }
            foreach (var s in db.Synergies.Values)
            {
                string id = "synergy " + s.Id;
                if (s.Requires == null || s.Requires.Count == 0) r.Error($"{id}: no requirements.");
                else CheckRequirements(s.Requires, id, db, r, minTotal: 2);
                if (s.Effects.Count == 0 && (s.Suppresses == null || s.Suppresses.Length == 0)) r.Error($"{id}: does nothing.");
                CheckEffects(s.Effects, id, effects, conditions, db, r);
                CheckCondition(s.Condition, id, conditions, r);
            }
            foreach (var t in db.Transformations.Values)
            {
                string id = "transformation " + t.Id;
                if (t.Requires == null || t.Requires.Count == 0) r.Error($"{id}: no requirements.");
                else CheckRequirements(t.Requires, id, db, r, minTotal: 2);
                if (t.Effects.Count == 0) r.Error($"{id}: no effects.");
                CheckEffects(t.Effects, id, effects, conditions, db, r);
                CheckCondition(t.Condition, id, conditions, r);
            }
            foreach (var f in db.Floors.Values)
            {
                string id = "floor " + f.Id;
                if (f.MinRooms < 5 || f.MaxRooms < f.MinRooms) r.Error($"{id}: invalid room counts.");
                if (f.Enemies.Count == 0) r.Warn($"{id}: empty enemy pool.");
                if (f.Bosses.Count == 0) r.Warn($"{id}: empty boss pool.");
                foreach (var e in f.Enemies) if (!db.Enemies.ContainsKey(e.EnemyId)) r.Error($"{id}: unknown enemy '{e.EnemyId}'.");
                foreach (var b in f.Bosses) if (!db.Bosses.ContainsKey(b.Id)) r.Error($"{id}: unknown boss '{b.Id}'.");
                foreach (var b in f.Minibosses) if (!db.Bosses.ContainsKey(b.Id)) r.Error($"{id}: unknown miniboss '{b.Id}'.");
            }
            foreach (var e in db.Enemies.Values) CheckEnemy(e, "enemy " + e.Id, db, r);
            foreach (var b in db.Bosses.Values)
            {
                string id = "boss " + b.Id;
                if (string.IsNullOrWhiteSpace(b.Name)) r.Error($"{id}: missing name.");
                if (b.Body == null) { r.Error($"{id}: missing body."); continue; }
                CheckEnemy(b.Body, id, db, r);
                if (b.Phases.Count == 0 && b.Body.Attacks.Count == 0) r.Error($"{id}: no attacks.");
                foreach (var p in b.Phases)
                {
                    if (p.HealthAtOrBelow <= 0f || p.HealthAtOrBelow > 1f) r.Error($"{id}: phase threshold must be in (0,1].");
                    foreach (var a in p.Attacks) CheckAttack(a, id, db, r);
                    foreach (var a in p.OnEnter) CheckAttack(a, id, db, r);
                }
                if (b.Tier != Enemies.BossTier.Miniboss && b.Phases.Count < 2) r.Warn($"{id}: major bosses should have phase transitions.");
            }
            return r;
        }

        private static void CheckEnemy(Enemies.EnemyDefinition e, string id, ContentDatabase db, ValidationReport r)
        {
            if (string.IsNullOrWhiteSpace(e.Name) && !e.IsBoss) r.Error($"{id}: missing name.");
            if (e.Health <= 0f) r.Error($"{id}: health must be positive.");
            if (e.ThinkInterval < 0.02f) r.Warn($"{id}: think interval below 20ms is wasteful.");
            if (!e.IsBoss && e.Attacks.Count == 0 && e.ContactDamage <= 0 && e.OnDeath.Count == 0) r.Error($"{id}: harmless enemy (no attacks, contact damage or death effect).");
            foreach (var a in e.Attacks) CheckAttack(a, id, db, r);
            foreach (var d in e.OnDeath)
                if (d.Kind == Enemies.DeathEffectKind.SpawnEnemies || d.Kind == Enemies.DeathEffectKind.Split)
                    if (d.EnemyId == null || !db.Enemies.ContainsKey(d.EnemyId)) r.Error($"{id}: death spawns unknown enemy '{d.EnemyId}'.");
        }

        private static void CheckAttack(Enemies.AttackSpec a, string id, ContentDatabase db, ValidationReport r)
        {
            if (a.Cooldown <= 0f) r.Error($"{id}: attack '{a.Id}' needs a positive cooldown.");
            if (a.Telegraph < 0.15f && a.Kind != Enemies.AttackKind.Teleport) r.Warn($"{id}: attack '{a.Id}' telegraph under 0.15s is hard to read.");
            if (a.Kind == Enemies.AttackKind.Summon && (a.SummonId == null || !db.Enemies.ContainsKey(a.SummonId))) r.Error($"{id}: summons unknown enemy '{a.SummonId}'.");
            if (a.MinRange > a.MaxRange) r.Error($"{id}: attack '{a.Id}' minRange > maxRange.");
        }

        private static void CheckRequirements(List<Requirement> reqs, string id, ContentDatabase db, ValidationReport r, int minTotal)
        {
            int total = 0;
            foreach (var q in reqs)
            {
                total += Math.Max(1, q.Count);
                if (q.ItemId == null && q.Tag == null) r.Error($"{id}: requirement needs itemId or tag.");
                if (q.ItemId != null && !db.Items.ContainsKey(q.ItemId)) r.Error($"{id}: unknown item '{q.ItemId}'.");
                if (q.Tag != null)
                {
                    int tagged = 0;
                    foreach (var i in db.Items.Values) if (i.HasTag(q.Tag)) tagged++;
                    if (tagged < q.Count) r.Error($"{id}: needs {q.Count} items tagged '{q.Tag}' but only {tagged} exist.");
                }
            }
            if (total < minTotal) r.Error($"{id}: combinations need at least {minTotal} ingredients.");
        }

        private static void CheckEffects(List<EffectSpec> specs, string id, EffectRegistry effects, ConditionRegistry conditions, ContentDatabase db, ValidationReport r)
        {
            if (specs == null) return;
            foreach (var e in specs)
            {
                if (e == null) { r.Error($"{id}: null effect entry."); continue; }
                if (!effects.IsRegistered(e.Type)) r.Error($"{id}: unknown effect '{e.Type}'.");
                CheckCondition(e.Condition, id, conditions, r);
                var status = e.Params.GetString("status");
                if (status != null && db.Statuses.Count > 0 && !db.Statuses.ContainsKey(status)) r.Error($"{id}: unknown status '{status}'.");
            }
        }

        private static void CheckCondition(string expr, string id, ConditionRegistry conditions, ValidationReport r)
        {
            foreach (var u in conditions.FindUnknown(expr)) r.Error($"{id}: unknown condition '{u}'.");
        }
    }
}
