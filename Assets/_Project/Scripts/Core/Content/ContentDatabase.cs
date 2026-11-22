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
        public List<Consumables.ConsumableDefinition> Consumables;
        public List<Consumables.HexDefinition> Hexes;
        public List<Consumables.ShrineDefinition> Shrines;
        public List<Progression.CharacterDefinition> Characters;
        public List<Progression.AchievementDefinition> Achievements;
        public List<Progression.ChallengeDefinition> Challenges;
        public List<Simulation.CompanionDefinition> Companions;
        public List<Simulation.RoomModifierDefinition> RoomModifiers;
        public List<Progression.EndingDefinition> Endings;
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
        public readonly Dictionary<string, Consumables.ConsumableDefinition> Consumables = new Dictionary<string, Consumables.ConsumableDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Consumables.HexDefinition> Hexes = new Dictionary<string, Consumables.HexDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Consumables.ShrineDefinition> Shrines = new Dictionary<string, Consumables.ShrineDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Progression.CharacterDefinition> Characters = new Dictionary<string, Progression.CharacterDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Progression.AchievementDefinition> Achievements = new Dictionary<string, Progression.AchievementDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Progression.ChallengeDefinition> Challenges = new Dictionary<string, Progression.ChallengeDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Simulation.CompanionDefinition> Companions = new Dictionary<string, Simulation.CompanionDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Progression.EndingDefinition> Endings = new Dictionary<string, Progression.EndingDefinition>(StringComparer.Ordinal);
        public readonly Dictionary<string, Simulation.RoomModifierDefinition> RoomModifiers = new Dictionary<string, Simulation.RoomModifierDefinition>(StringComparer.Ordinal);
        /// <summary>Content order of characters as authored (menus list them in this order).</summary>
        public readonly List<string> CharacterOrder = new List<string>();
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
            AddAll(sourceName, file.Consumables, Consumables, x => x.Id);
            AddAll(sourceName, file.Hexes, Hexes, x => x.Id);
            AddAll(sourceName, file.Shrines, Shrines, x => x.Id);
            AddAll(sourceName, file.Characters, Characters, x => x.Id);
            if (file.Characters != null) foreach (var c in file.Characters) if (c?.Id != null && !CharacterOrder.Contains(c.Id)) CharacterOrder.Add(c.Id);
            AddAll(sourceName, file.Achievements, Achievements, x => x.Id);
            AddAll(sourceName, file.Challenges, Challenges, x => x.Id);
            AddAll(sourceName, file.Companions, Companions, x => x.Id);
            AddAll(sourceName, file.RoomModifiers, RoomModifiers, x => x.Id);
            AddAll(sourceName, file.Endings, Endings, x => x.Id);
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
            foreach (var c in db.Consumables.Values)
            {
                string id = "consumable " + c.Id;
                if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Description)) r.Error($"{id}: missing name or description.");
                if (c.OnUse.Count == 0) r.Error($"{id}: no on-use effects.");
                CheckEffects(c.OnUse, id, effects, conditions, db, r);
                foreach (var spec in c.OnUse)
                    if (effects.IsRegistered(spec.Type) && !(effects.Create(spec) is IActiveEffect)) r.Error($"{id}: '{spec.Type}' is not an on-use effect.");
            }
            foreach (var h in db.Hexes.Values)
            {
                string id = "hex " + h.Id;
                if (string.IsNullOrWhiteSpace(h.Name) || string.IsNullOrWhiteSpace(h.Description)) r.Error($"{id}: missing name or description.");
                if (h.FloorSizeMultiplier < 1f || h.FloorSizeMultiplier > 2f) r.Error($"{id}: floorSizeMultiplier must be 1..2.");
                bool any = h.HideMap || h.HideRoomIcons || h.FloorSizeMultiplier > 1f || h.VeilPedestals || h.VisionRadius > 0f || h.Effects.Count > 0;
                if (!any) r.Error($"{id}: does nothing.");
                CheckEffects(h.Effects, id, effects, conditions, db, r);
            }
            foreach (var s in db.Shrines.Values)
            {
                string id = "shrine " + s.Id;
                if (string.IsNullOrWhiteSpace(s.Name)) r.Error($"{id}: missing name.");
                if (s.Options.Count == 0) r.Error($"{id}: no options.");
                foreach (var o in s.Options)
                {
                    if (o.Outcomes.Count == 0) r.Error($"{id}: option '{o.Label}' has no outcomes.");
                    if (o.Cost != Consumables.ShrineCostKind.None && o.Amount <= 0) r.Error($"{id}: option '{o.Label}' cost needs a positive amount.");
                    if (o.MaxUses < 1) r.Error($"{id}: option '{o.Label}' maxUses must be >= 1.");
                    foreach (var oc in o.Outcomes)
                    {
                        if (oc.Kind == Consumables.ShrineOutcomeKind.Enemies && (oc.ContentId == null || !db.Enemies.ContainsKey(oc.ContentId))) r.Error($"{id}: spawns unknown enemy '{oc.ContentId}'.");
                        if (oc.Kind == Consumables.ShrineOutcomeKind.Hex && (oc.ContentId == null || !db.Hexes.ContainsKey(oc.ContentId))) r.Error($"{id}: unknown hex '{oc.ContentId}'.");
                        if (oc.Kind == Consumables.ShrineOutcomeKind.Consumable && oc.ContentId != null && !db.Consumables.ContainsKey(oc.ContentId)) r.Error($"{id}: unknown consumable '{oc.ContentId}'.");
                        if (oc.Kind == Consumables.ShrineOutcomeKind.Stat) { if (oc.Effect == null) r.Error($"{id}: stat outcome without effect."); else CheckEffects(new List<EffectSpec> { oc.Effect }, id, effects, conditions, db, r); }
                    }
                }
            }
            ValidateProgression(db, effects, conditions, r);
            ValidateCompanions(db, r);
            ValidateRoomModifiers(db, r);
            ValidateEndings(db, r);
            return r;
        }

        private static void ValidateEndings(ContentDatabase db, ValidationReport r)
        {
            if (db.Endings.Count == 0) return;
            bool fallback = false;
            foreach (var e in db.Endings.Values)
            {
                string id = "ending " + e.Id;
                if (string.IsNullOrWhiteSpace(e.Name)) r.Error($"{id}: missing name.");
                if (e.Lines == null || e.Lines.Count == 0) r.Error($"{id}: no epilogue lines.");
                if (e.RequiresBoss != null && !db.Bosses.ContainsKey(e.RequiresBoss)) r.Error($"{id}: unknown boss '{e.RequiresBoss}'.");
                if (e.RequiresCharacter != null && db.Characters.Count > 0 && !db.Characters.ContainsKey(e.RequiresCharacter)) r.Error($"{id}: unknown character '{e.RequiresCharacter}'.");
                if (e.RequiresBoss == null && e.RequiresCharacter == null && e.RequiresFlag == null && !e.RequiresMode.HasValue) fallback = true;
            }
            if (!fallback) r.Error("endings: one ending must have no conditions (the fallback).");
            foreach (var a in db.Achievements.Values)
                if (a.Trigger == Progression.AchievementTrigger.EndingReached && (a.Key == null || !db.Endings.ContainsKey(a.Key))) r.Error($"achievement {a.Id}: unknown ending '{a.Key}'.");
        }

        private static void ValidateRoomModifiers(ContentDatabase db, ValidationReport r)
        {
            foreach (var m in db.RoomModifiers.Values)
            {
                string id = "room modifier " + m.Id;
                if (string.IsNullOrWhiteSpace(m.Name)) r.Error($"{id}: missing name.");
                if (m.Weight < 0f) r.Error($"{id}: negative weight.");
                if (m.EnemyHealthMultiplier <= 0f || m.EnemySpeedMultiplier <= 0f || m.EnemyDamageTakenMultiplier <= 0f) r.Error($"{id}: multipliers must be positive.");
                if (m.EnemySpeedMultiplier > 2f) r.Warn($"{id}: enemies more than twice as fast is unreadable.");
                if (m.ExtraEnemies < 0f || m.ExtraEnemies > 1f) r.Error($"{id}: extraEnemies must be 0..1.");
                if (m.EnemyRegenPerSecond < 0f || m.EnemyRegenPerSecond > 0.2f) r.Error($"{id}: enemyRegenPerSecond must be 0..0.2.");
                if (m.RewardPickups < 0 || m.RewardPickups > 6) r.Error($"{id}: rewardPickups must be 0..6.");
                if (m.VisionRadius < 0f) r.Error($"{id}: negative visionRadius.");
            }
        }

        private static void ValidateCompanions(ContentDatabase db, ValidationReport r)
        {
            foreach (var c in db.Companions.Values)
            {
                string id = "companion " + c.Id;
                if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Description)) r.Error($"{id}: missing name or description.");
                if (c.Speed <= 0f || c.Radius <= 0f) r.Error($"{id}: speed and radius must be positive.");
                if ((c.Behavior == Simulation.CompanionBehavior.Shooter || c.Behavior == Simulation.CompanionBehavior.Mimic) && (c.FireInterval <= 0.05f || c.DamageScale <= 0f)) r.Error($"{id}: shooters need fireInterval > 0.05 and damageScale > 0.");
                if ((c.Behavior == Simulation.CompanionBehavior.Orbital || c.Behavior == Simulation.CompanionBehavior.Chaser) && c.ContactDps <= 0f && !c.BlocksShots) r.Error($"{id}: contact companion that neither hurts nor blocks.");
                if (c.Behavior == Simulation.CompanionBehavior.Provider && c.ProvideEveryRooms < 1) r.Error($"{id}: provideEveryRooms must be >= 1.");
                if (c.ShotStatus != null && !db.Statuses.ContainsKey(c.ShotStatus)) r.Error($"{id}: unknown status '{c.ShotStatus}'.");
            }
            // every companion id referenced by an effect must exist
            void Check(IEnumerable<EffectSpec> specs, string who)
            {
                if (specs == null) return;
                foreach (var e in specs)
                {
                    if (e == null || (e.Type != "companion" && e.Type != "active_companion_room")) continue;
                    var cid = e.Params.GetString("companion");
                    if (db.Companions.Count > 0 && (cid == null || !db.Companions.ContainsKey(cid))) r.Error($"{who}: unknown companion '{cid}'.");
                }
            }
            foreach (var i in db.Items.Values) { Check(i.Effects, "item " + i.Id); if (i.Active != null) Check(i.Active.OnUse, "item " + i.Id); }
            foreach (var s in db.Synergies.Values) Check(s.Effects, "synergy " + s.Id);
            foreach (var t in db.Transformations.Values) Check(t.Effects, "transformation " + t.Id);
        }

        private static void ValidateProgression(ContentDatabase db, EffectRegistry effects, ConditionRegistry conditions, ValidationReport r)
        {
            // every unlock id referenced by content must be granted by some achievement (or be a challenge reward)
            var granted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in db.Achievements.Values) foreach (var u in a.Unlocks) granted.Add(u);
            foreach (var ch in db.Challenges.Values) if (ch.RewardUnlock != null) granted.Add(ch.RewardUnlock);
            foreach (var e in db.Endings.Values) foreach (var u in e.Unlocks) granted.Add(u);
            void NeedsGrant(string unlockId, string who) { if (unlockId != null && !granted.Contains(unlockId)) r.Error($"{who}: unlock '{unlockId}' is never granted."); }

            foreach (var c in db.Characters.Values)
            {
                string id = "character " + c.Id;
                if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Description)) r.Error($"{id}: missing name or description.");
                if (!c.NoContainers && c.Containers < 1) r.Error($"{id}: needs at least one container (or noContainers).");
                if (c.NoContainers && c.Umbral + c.Ward <= 0) r.Error($"{id}: a character without containers needs Umbral or Ward to start.");
                foreach (var it in c.StartingItems) if (!db.Items.ContainsKey(it)) r.Error($"{id}: unknown starting item '{it}'.");
                if (c.StartingConsumable != null && !db.Consumables.ContainsKey(c.StartingConsumable)) r.Error($"{id}: unknown consumable '{c.StartingConsumable}'.");
                CheckEffects(c.Effects, id, effects, conditions, db, r);
                NeedsGrant(c.UnlockId, id);
            }
            foreach (var a in db.Achievements.Values)
            {
                string id = "achievement " + a.Id;
                if (string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Description)) r.Error($"{id}: missing name or description.");
                if (a.Target < 1) r.Error($"{id}: target must be >= 1.");
                switch (a.Trigger)
                {
                    case Progression.AchievementTrigger.BossDefeated: if (a.Key == null || !db.Bosses.ContainsKey(a.Key)) r.Error($"{id}: unknown boss '{a.Key}'."); break;
                    case Progression.AchievementTrigger.WinWithCharacter: if (a.Key == null || !db.Characters.ContainsKey(a.Key)) r.Error($"{id}: unknown character '{a.Key}'."); break;
                    case Progression.AchievementTrigger.Transformation: if (a.Key == null || !db.Transformations.ContainsKey(a.Key)) r.Error($"{id}: unknown transformation '{a.Key}'."); break;
                    case Progression.AchievementTrigger.ChallengeComplete: if (a.Key == null || !db.Challenges.ContainsKey(a.Key)) r.Error($"{id}: unknown challenge '{a.Key}'."); break;
                    case Progression.AchievementTrigger.Counter:
                    case Progression.AchievementTrigger.RunCondition: if (string.IsNullOrEmpty(a.Key)) r.Error($"{id}: needs a key."); break;
                }
            }
            foreach (var ch in db.Challenges.Values)
            {
                string id = "challenge " + ch.Id;
                if (string.IsNullOrWhiteSpace(ch.Name) || string.IsNullOrWhiteSpace(ch.Description)) r.Error($"{id}: missing name or description.");
                if (!db.Characters.ContainsKey(ch.CharacterId)) r.Error($"{id}: unknown character '{ch.CharacterId}'.");
                foreach (var it in ch.StartingItems) if (!db.Items.ContainsKey(it)) r.Error($"{id}: unknown starting item '{it}'.");
                if (ch.ForcedHex != null && !db.Hexes.ContainsKey(ch.ForcedHex)) r.Error($"{id}: unknown hex '{ch.ForcedHex}'.");
                if (ch.Goal == Progression.ChallengeGoal.ReachFloor && ch.GoalFloor < 1) r.Error($"{id}: goalFloor must be >= 1.");
                CheckEffects(ch.Effects, id, effects, conditions, db, r);
                NeedsGrant(ch.UnlockId, id);
            }
            foreach (var item in db.Items.Values) if (item.UnlockId != null && db.Achievements.Count > 0) NeedsGrant(item.UnlockId, "item " + item.Id);
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
