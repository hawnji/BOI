using System;
using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Items
{
    public enum AddItemResult { Added, Stacked, RefusedUnique, RefusedStackCap, Swapped, Invalid }

    /// <summary>Runtime state of an item the player holds.</summary>
    public sealed class OwnedItem
    {
        public ItemDefinition Definition { get; internal set; }
        public int Stacks { get; internal set; }
        public long AcquiredOrder { get; internal set; }
        internal readonly List<EffectRecord> Records = new List<EffectRecord>();
        internal readonly List<EffectRecord> ActiveUseRecords = new List<EffectRecord>();
        public string Id => Definition.Id;
        public override string ToString() => Stacks > 1 ? $"{Id} x{Stacks}" : Id;
    }

    internal sealed class ActiveCombo
    {
        public string Id;
        public object Definition;               // SynergyDefinition or TransformationDefinition
        public readonly List<EffectRecord> Records = new List<EffectRecord>();
    }

    /// <summary>
    /// The central item interaction engine. Owns every effect instance from items, synergies,
    /// transformations, characters and curses; discovers which hooks each effect implements; and
    /// dispatches combat/room/economy events to them in a deterministic order.
    ///
    /// Safeguards built in:
    ///  * Recursion: nested dispatch is capped at <see cref="MaxProcDepth"/>.
    ///  * Spawning: reactive spawns share a per-frame budget and a projectile generation limit.
    ///  * Duplicates: an effect instance is attached once; hook lists are rebuilt from the record set.
    ///  * Stacking: items declare Unique / Stack / StackCapped; stat sheet caps modifier count per stat.
    ///  * Conflicts: exclusive projectile delivery by priority; synergy exclusive groups; synergy suppression.
    /// </summary>
    public sealed class InteractionEngine
    {
        public const int MaxProcDepth = 4;
        public const float BaseShotSpeed = 9f;

        public StatSheet Stats { get; }
        public HealthPool Health { get; }
        public PlayerResources Resources { get; }
        public IGameWorld World { get; set; }
        public SeededRandom Rng { get; }
        public EffectRegistry Registry { get; }
        public ConditionRegistry Conditions { get; }
        public InteractionLog Log { get; } = new InteractionLog(128);

        public int SpawnBudgetPerFrame { get; set; } = 160;
        public int TrinketCapacity { get; set; } = 1;

        // Context used by conditions
        public string CharacterId { get; set; }
        public bool InCombat { get; set; }
        public string CurrentRoomType { get; set; }
        public int FloorIndex { get; set; }
        private readonly HashSet<string> _curses = new HashSet<string>(StringComparer.Ordinal);

        private readonly List<OwnedItem> _passives = new List<OwnedItem>();
        private readonly List<OwnedItem> _trinkets = new List<OwnedItem>();
        private OwnedItem _active;
        private readonly Dictionary<string, OwnedItem> _held = new Dictionary<string, OwnedItem>(StringComparer.Ordinal);
        private readonly HashSet<string> _everAcquired = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ItemDefinition> _collectedHistory = new List<ItemDefinition>();

        private readonly List<SynergyDefinition> _synergyDefs = new List<SynergyDefinition>();
        private readonly List<TransformationDefinition> _transformDefs = new List<TransformationDefinition>();
        private readonly List<ActiveCombo> _activeSynergies = new List<ActiveCombo>();
        private readonly List<ActiveCombo> _activeTransformations = new List<ActiveCombo>();
        private readonly HashSet<string> _discoveredSynergies = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<object, List<EffectRecord>> _misc = new Dictionary<object, List<EffectRecord>>();

        private readonly List<EffectRecord> _allRecords = new List<EffectRecord>();
        private readonly HookCache _hooks = new HookCache();
        private long _sequence;
        private long _acquireCounter;
        private int _procDepth;
        private int _spawnedThisFrame;
        private bool _reevaluating;

        // Active item charge
        public int ActiveCharge { get; private set; }
        public float ActiveTimer { get; private set; }

        public event Action<OwnedItem> ItemAdded;
        public event Action<ItemDefinition> ItemRemoved;
        public event Action<SynergyDefinition> SynergyActivated;
        public event Action<SynergyDefinition> SynergyDeactivated;
        public event Action<TransformationDefinition> TransformationTriggered;
        public event Action<TransformationDefinition> TransformationReplaced;
        public event Action<string, string> GuardTripped; // guard, source
        public event Action ActiveChargeChanged;

        public InteractionEngine(EffectRegistry registry, StatSheet stats, HealthPool health, PlayerResources resources,
            IGameWorld world, SeededRandom rng, ConditionRegistry conditions = null)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Health = health;
            Resources = resources ?? new PlayerResources();
            World = world;
            Rng = rng ?? new SeededRandom(1);
            Conditions = conditions ?? new ConditionRegistry();
            // Keep conditional and scaling modifiers current.
            Resources.Changed += (k, o, n) => Stats.Invalidate();
            if (Health != null) Health.Changed += Stats.Invalidate;
        }

        // ------------------------------------------------------------------ content

        public void SetCombos(IEnumerable<SynergyDefinition> synergies, IEnumerable<TransformationDefinition> transformations)
        {
            _synergyDefs.Clear();
            if (synergies != null) _synergyDefs.AddRange(synergies);
            _synergyDefs.Sort((a, b) => b.Priority != a.Priority ? b.Priority.CompareTo(a.Priority) : string.CompareOrdinal(a.Id, b.Id));
            _transformDefs.Clear();
            if (transformations != null) _transformDefs.AddRange(transformations);
            ReevaluateCombos();
        }

        // ------------------------------------------------------------------ queries

        public IReadOnlyList<OwnedItem> Passives => _passives;
        public IReadOnlyList<OwnedItem> Trinkets => _trinkets;
        public OwnedItem ActiveItem => _active;
        public IReadOnlyCollection<string> DiscoveredSynergies => _discoveredSynergies;
        public int ActiveTransformationCount => _activeTransformations.Count;
        public IReadOnlyList<ItemDefinition> CollectedHistory => _collectedHistory;

        public bool HasItem(string id) => _held.ContainsKey(id);
        public int GetStacks(string id) => _held.TryGetValue(id, out var o) ? o.Stacks : 0;
        public bool HasCurse(string id) => _curses.Contains(id);
        public bool HasTransformation(string id) => _activeTransformations.Exists(c => c.Id == id);
        public bool IsSynergyActive(string id) => _activeSynergies.Exists(c => c.Id == id);

        public int CountHeldWithTag(string tag)
        {
            int n = 0;
            foreach (var kv in _held) if (kv.Value.Definition.HasTag(tag)) n++;
            return n;
        }

        public bool CanAccept(ItemDefinition def)
        {
            if (def == null) return false;
            if (!_held.TryGetValue(def.Id, out var owned)) return true;
            switch (def.Stacking)
            {
                case ItemStacking.Unique: return false;
                case ItemStacking.StackCapped: return owned.Stacks < Math.Max(1, def.MaxStacks);
                default: return true;
            }
        }

        // ------------------------------------------------------------------ inventory

        /// <summary>
        /// Adds an item. For actives and trinkets beyond capacity the displaced item is returned in
        /// <paramref name="displaced"/> so the gameplay layer can drop it on the floor.
        /// </summary>
        public AddItemResult AddItem(ItemDefinition def, out ItemDefinition displaced)
        {
            displaced = null;
            if (def == null || string.IsNullOrEmpty(def.Id)) return AddItemResult.Invalid;
            bool first = _everAcquired.Add(def.Id);
            AddItemResult result;

            if (_held.TryGetValue(def.Id, out var owned))
            {
                if (def.Stacking == ItemStacking.Unique) { if (first) _everAcquired.Remove(def.Id); return AddItemResult.RefusedUnique; }
                if (def.Stacking == ItemStacking.StackCapped && owned.Stacks >= Math.Max(1, def.MaxStacks)) return AddItemResult.RefusedStackCap;
                owned.Stacks++;
                RebuildItemEffects(owned, false);
                result = AddItemResult.Stacked;
            }
            else
            {
                owned = new OwnedItem { Definition = def, Stacks = 1, AcquiredOrder = ++_acquireCounter };
                result = AddItemResult.Added;
                switch (def.Kind)
                {
                    case ItemKind.Active:
                        if (_active != null) { displaced = _active.Definition; RemoveOwned(_active); result = AddItemResult.Swapped; }
                        _active = owned;
                        ResetActiveCharge(def);
                        break;
                    case ItemKind.Trinket:
                        if (_trinkets.Count >= Math.Max(1, TrinketCapacity))
                        {
                            displaced = _trinkets[0].Definition;
                            RemoveOwned(_trinkets[0]);
                            result = AddItemResult.Swapped;
                        }
                        _trinkets.Add(owned);
                        break;
                    default:
                        _passives.Add(owned);
                        break;
                }
                _held[def.Id] = owned;
                RebuildItemEffects(owned, first);
            }

            if (first) _collectedHistory.Add(def);
            Log.Add(InteractionLogKind.Inventory, def.Id, result.ToString());
            ItemAdded?.Invoke(owned);
            ReevaluateCombos();
            return result;
        }

        public AddItemResult AddItem(ItemDefinition def) => AddItem(def, out _);

        /// <summary>Removes one stack (or the whole item when allStacks is true).</summary>
        public bool RemoveItem(string id, bool allStacks = false)
        {
            if (!_held.TryGetValue(id, out var owned)) return false;
            if (!allStacks && owned.Stacks > 1)
            {
                owned.Stacks--;
                RebuildItemEffects(owned, false);
            }
            else
            {
                RemoveOwned(owned);
            }
            Log.Add(InteractionLogKind.Inventory, id, "removed");
            ReevaluateCombos();
            return true;
        }

        private void RemoveOwned(OwnedItem owned)
        {
            DetachAll(owned.Records);
            DetachAll(owned.ActiveUseRecords);
            _held.Remove(owned.Id);
            _passives.Remove(owned);
            _trinkets.Remove(owned);
            if (_active == owned) _active = null;
            ItemRemoved?.Invoke(owned.Definition);
        }

        private void RebuildItemEffects(OwnedItem owned, bool firstAcquisition)
        {
            DetachAll(owned.Records);
            DetachAll(owned.ActiveUseRecords);
            AttachSpecs(owned.Definition.Effects, EffectSourceKind.Item, owned.Id, owned, owned.Stacks, firstAcquisition, owned.Records);
            if (owned.Definition.Active != null)
                AttachSpecs(owned.Definition.Active.OnUse, EffectSourceKind.ActiveUse, owned.Id, owned, owned.Stacks, false, owned.ActiveUseRecords);
        }

        // ------------------------------------------------------------------ generic sources (characters, curses, temporary)

        /// <summary>Attaches effects from a non-item source (character kit, curse, room modifier, temporary buff).</summary>
        public void AttachSource(object owner, string sourceId, EffectSourceKind kind, IReadOnlyList<EffectSpec> specs)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            DetachSource(owner);
            var list = new List<EffectRecord>();
            AttachSpecs(specs, kind, sourceId, owner, 1, true, list);
            _misc[owner] = list;
            if (kind == EffectSourceKind.Curse) _curses.Add(sourceId);
            ReevaluateCombos();
        }

        public bool DetachSource(object owner)
        {
            if (owner == null || !_misc.TryGetValue(owner, out var list)) return false;
            string sourceId = list.Count > 0 ? list[0].SourceId : null;
            var kind = list.Count > 0 ? list[0].Context.SourceKind : EffectSourceKind.Temporary;
            DetachAll(list);
            _misc.Remove(owner);
            if (kind == EffectSourceKind.Curse && sourceId != null) _curses.Remove(sourceId);
            ReevaluateCombos();
            return true;
        }

        public void AddCurseFlag(string curseId) { _curses.Add(curseId); Stats.Invalidate(); }
        public void RemoveCurseFlag(string curseId) { _curses.Remove(curseId); Stats.Invalidate(); }

        // ------------------------------------------------------------------ attach / detach

        private void AttachSpecs(IReadOnlyList<EffectSpec> specs, EffectSourceKind kind, string sourceId, object owner, int stacks,
            bool first, List<EffectRecord> into, bool hookable = true)
        {
            if (specs == null) return;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null) continue;
                var effect = Registry.Create(spec);
                if (effect == null)
                {
                    Log.Add(InteractionLogKind.Error, sourceId, "unknown effect type '" + spec.Type + "'");
                    continue;
                }
                var ctx = new EffectContext
                {
                    Engine = this,
                    Spec = spec,
                    SourceKind = kind,
                    SourceId = sourceId,
                    Owner = owner,
                    Stacks = Math.Max(1, stacks),
                    IsFirstAcquisition = first,
                };
                var rec = new EffectRecord
                {
                    Effect = effect,
                    Context = ctx,
                    Condition = spec.Condition,
                    Sequence = ++_sequence,
                    Conditions = Conditions,
                    Engine = this,
                };
                ctx.Record = rec;
                if (hookable) _allRecords.Add(rec);
                into.Add(rec);
                try { effect.OnAttach(ctx); }
                catch (Exception ex) { Log.Add(InteractionLogKind.Error, sourceId, "attach failed: " + ex.Message); }
            }
            _hooks.MarkDirty();
            Stats.Invalidate();
        }

        private void DetachAll(List<EffectRecord> records)
        {
            if (records.Count == 0) return;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var rec = records[i];
                try { rec.Effect.OnDetach(rec.Context); }
                catch (Exception ex) { Log.Add(InteractionLogKind.Error, rec.SourceId, "detach failed: " + ex.Message); }
                Stats.RemoveAllFromSource(rec);
                _allRecords.Remove(rec);
            }
            records.Clear();
            _hooks.MarkDirty();
            Stats.Invalidate();
        }

        // ------------------------------------------------------------------ synergies & transformations

        /// <summary>Re-checks every synergy and transformation. Called automatically on inventory changes;
        /// call manually when a combo condition's inputs change (health, room type, curses).</summary>
        public void ReevaluateCombos()
        {
            if (_reevaluating) return;
            _reevaluating = true;
            try
            {
                EvaluateSynergies();
                EvaluateTransformations();
                ApplySuppression();
                Stats.Invalidate();
            }
            finally { _reevaluating = false; }
        }

        private void EvaluateSynergies()
        {
            var wanted = new List<SynergyDefinition>();
            var usedGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _synergyDefs.Count; i++) // already sorted by priority desc
            {
                var s = _synergyDefs[i];
                if (!RequirementsMetByHeld(s.Requires)) continue;
                if (!string.IsNullOrEmpty(s.Condition) && !Conditions.Evaluate(s.Condition, this, null)) continue;
                if (!string.IsNullOrEmpty(s.ExclusiveGroup) && !usedGroups.Add(s.ExclusiveGroup)) continue;
                wanted.Add(s);
            }

            for (int i = _activeSynergies.Count - 1; i >= 0; i--)
            {
                var combo = _activeSynergies[i];
                if (wanted.Exists(w => w.Id == combo.Id)) continue;
                DetachAll(combo.Records);
                _activeSynergies.RemoveAt(i);
                Log.Add(InteractionLogKind.Synergy, combo.Id, "deactivated");
                SynergyDeactivated?.Invoke((SynergyDefinition)combo.Definition);
            }

            foreach (var s in wanted)
            {
                if (_activeSynergies.Exists(c => c.Id == s.Id)) continue;
                var combo = new ActiveCombo { Id = s.Id, Definition = s };
                _activeSynergies.Add(combo);
                bool firstTime = _discoveredSynergies.Add(s.Id);
                AttachSpecs(s.Effects, EffectSourceKind.Synergy, s.Id, combo, 1, firstTime, combo.Records);
                Log.Add(InteractionLogKind.Synergy, s.Id, "activated");
                SynergyActivated?.Invoke(s);
            }
            // keep evaluation order = priority order for deterministic suppression
            _activeSynergies.Sort((a, b) =>
            {
                var da = (SynergyDefinition)a.Definition; var db = (SynergyDefinition)b.Definition;
                return da.Priority != db.Priority ? db.Priority.CompareTo(da.Priority) : string.CompareOrdinal(da.Id, db.Id);
            });
        }

        private void EvaluateTransformations()
        {
            for (int i = 0; i < _transformDefs.Count; i++)
            {
                var t = _transformDefs[i];
                if (_activeTransformations.Exists(c => c.Id == t.Id)) continue;
                if (!RequirementsMetByHistory(t.Requires)) continue;
                if (!string.IsNullOrEmpty(t.Condition) && !Conditions.Evaluate(t.Condition, this, null)) continue;

                if (!string.IsNullOrEmpty(t.ExclusiveGroup))
                {
                    for (int k = _activeTransformations.Count - 1; k >= 0; k--)
                    {
                        var other = (TransformationDefinition)_activeTransformations[k].Definition;
                        if (other.ExclusiveGroup != t.ExclusiveGroup) continue;
                        DetachAll(_activeTransformations[k].Records);
                        _activeTransformations.RemoveAt(k);
                        Log.Add(InteractionLogKind.Transformation, other.Id, "replaced by " + t.Id);
                        TransformationReplaced?.Invoke(other);
                    }
                }
                var combo = new ActiveCombo { Id = t.Id, Definition = t };
                _activeTransformations.Add(combo);
                AttachSpecs(t.Effects, EffectSourceKind.Transformation, t.Id, combo, 1, true, combo.Records);
                Log.Add(InteractionLogKind.Transformation, t.Id, "triggered");
                TransformationTriggered?.Invoke(t);
            }
        }

        private bool RequirementsMetByHeld(List<Requirement> reqs)
        {
            if (reqs == null || reqs.Count == 0) return false;
            for (int i = 0; i < reqs.Count; i++)
            {
                var r = reqs[i];
                if (r.ItemId != null) { if (GetStacks(r.ItemId) < Math.Max(1, r.Count)) return false; }
                else if (r.Tag != null) { if (CountHeldWithTag(r.Tag) < Math.Max(1, r.Count)) return false; }
                else return false;
            }
            return true;
        }

        private bool RequirementsMetByHistory(List<Requirement> reqs)
        {
            if (reqs == null || reqs.Count == 0) return false;
            for (int i = 0; i < reqs.Count; i++)
            {
                var r = reqs[i];
                if (r.ItemId != null) { if (!_everAcquired.Contains(r.ItemId)) return false; }
                else if (r.Tag != null)
                {
                    int n = 0;
                    for (int k = 0; k < _collectedHistory.Count; k++) if (_collectedHistory[k].HasTag(r.Tag)) n++;
                    if (n < Math.Max(1, r.Count)) return false;
                }
                else return false;
            }
            return true;
        }

        private void ApplySuppression()
        {
            for (int i = 0; i < _allRecords.Count; i++) { _allRecords[i].Suppressed = false; _allRecords[i].SuppressedBy = null; }
            // Higher-priority synergies are processed first; a synergy that is itself suppressed cannot suppress others.
            for (int s = 0; s < _activeSynergies.Count; s++)
            {
                var combo = _activeSynergies[s];
                var def = (SynergyDefinition)combo.Definition;
                if (combo.Records.Count > 0 && combo.Records.TrueForAll(r => r.Suppressed)) continue;
                if (def.Suppresses == null) continue;
                foreach (var pattern in def.Suppresses)
                {
                    if (string.IsNullOrEmpty(pattern)) continue;
                    for (int i = 0; i < _allRecords.Count; i++)
                    {
                        var rec = _allRecords[i];
                        if (rec.Context.Owner == combo || rec.Suppressed) continue;
                        if (MatchesSuppression(pattern, rec))
                        {
                            rec.Suppressed = true;
                            rec.SuppressedBy = def.Id;
                        }
                    }
                }
            }
        }

        private static bool MatchesSuppression(string pattern, EffectRecord rec)
        {
            if (pattern.StartsWith("synergy:", StringComparison.Ordinal))
                return rec.Context.SourceKind == EffectSourceKind.Synergy && rec.SourceId == pattern.Substring(8);
            int slash = pattern.IndexOf('/');
            if (slash < 0) return false;
            string src = pattern.Substring(0, slash);
            string eff = pattern.Substring(slash + 1);
            bool srcOk = src == "*" ? rec.Context.SourceKind == EffectSourceKind.Item : rec.SourceId == src;
            if (!srcOk) return false;
            return eff == "*" || rec.Type == eff || (rec.EffectId != null && rec.EffectId == eff);
        }

        // ------------------------------------------------------------------ guards

        internal void ReportGuard(string guard, string source)
        {
            Log.Add(InteractionLogKind.Guard, source, guard);
            GuardTripped?.Invoke(guard, source);
        }

        internal bool TryConsumeSpawnBudget(int count, string source)
        {
            if (_spawnedThisFrame + count > SpawnBudgetPerFrame)
            {
                ReportGuard("spawn-budget", source);
                return false;
            }
            _spawnedThisFrame += count;
            return true;
        }

        private bool EnterProc(string what)
        {
            if (_procDepth >= MaxProcDepth)
            {
                ReportGuard("proc-depth:" + what, null);
                return false;
            }
            _procDepth++;
            return true;
        }

        private void ExitProc() => _procDepth--;

        // ------------------------------------------------------------------ dispatch

        /// <summary>Call once per frame from the gameplay loop.</summary>
        public void Tick(float dt)
        {
            _spawnedThisFrame = 0;
            TickActiveCharge(dt);
            var hooks = _hooks.Get(this).Tick;
            if (hooks.Length == 0 || !EnterProc("tick")) return;
            try
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    if (!hooks[i].Rec.IsEnabled) continue;
                    try { hooks[i].Hook.Tick(dt); }
                    catch (Exception ex) { Log.Add(InteractionLogKind.Error, hooks[i].Rec.SourceId, ex.Message); }
                }
            }
            finally { ExitProc(); }
        }

        /// <summary>Builds the player's (or a companion's) shot: stats, then all projectile modifiers by Order.</summary>
        public void BuildShot(ProjectileSpec spec, ShotContext shot)
        {
            spec.Reset();
            spec.Team = Team.Player;
            spec.Damage = Stats[StatId.Damage];
            spec.Speed = Stats[StatId.ShotSpeed] * BaseShotSpeed;
            spec.Range = Stats[StatId.Range];
            spec.Size = Stats[StatId.ShotSize];
            spec.Knockback = Stats[StatId.Knockback];
            spec.CritChance = Stats[StatId.CritChance];
            spec.CritMultiplier = Stats[StatId.CritDamage];

            if (!shot.IsCompanion) ApplyPlayerProjectileModifiers(spec, shot);
            else
            {
                var comp = _hooks.Get(this).CompanionShot;
                for (int i = 0; i < comp.Length; i++)
                    if (comp[i].Rec.IsEnabled) comp[i].Hook.ModifyCompanionShot(spec, shot.CompanionId);
            }
            spec.Sanitize();
        }

        /// <summary>Applies the player's projectile modifiers to a spec (used by BuildShot and by "inherit" effects).</summary>
        public void ApplyPlayerProjectileModifiers(ProjectileSpec spec, ShotContext shot)
        {
            var mods = _hooks.Get(this).Projectile;
            for (int i = 0; i < mods.Length; i++)
            {
                var m = mods[i];
                if (!m.Rec.IsEnabled) continue;
                m.Hook.ModifyProjectile(spec, shot);
                spec.AppliedModifiers.Add(m.Rec.SourceId + "/" + m.Rec.Type);
            }
        }

        public void ModifyOutgoingDamage(ref DamageInfo damage, in HitContext hit)
        {
            var mods = _hooks.Get(this).Outgoing;
            for (int i = 0; i < mods.Length; i++) if (mods[i].Rec.IsEnabled) mods[i].Hook.ModifyOutgoingDamage(ref damage, hit);
            if (damage.Amount < 0f) damage.Amount = 0f;
        }

        public void ModifyIncomingDamage(ref DamageInfo damage)
        {
            var mods = _hooks.Get(this).Incoming;
            for (int i = 0; i < mods.Length; i++) if (mods[i].Rec.IsEnabled) mods[i].Hook.ModifyIncomingDamage(ref damage);
            if (damage.Amount < 0f) damage.Amount = 0f;
        }

        public void NotifyHit(in HitContext hit)
        {
            var hooks = _hooks.Get(this).OnHit;
            if (hooks.Length == 0 || !EnterProc("hit")) return;
            try
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    var h = hooks[i];
                    if (!h.Rec.IsEnabled) continue;
                    try { h.Hook.OnHit(hit); }
                    catch (Exception ex) { Log.Add(InteractionLogKind.Error, h.Rec.SourceId, ex.Message); }
                }
            }
            finally { ExitProc(); }
        }

        public void NotifyKill(in KillContext kill)
        {
            var hooks = _hooks.Get(this).OnKill;
            if (_active?.Definition.Active?.ChargeType == ActiveChargeType.Kills) AddActiveCharge(1);
            if (hooks.Length == 0 || !EnterProc("kill")) return;
            try
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    var h = hooks[i];
                    if (!h.Rec.IsEnabled) continue;
                    try { h.Hook.OnKill(kill); }
                    catch (Exception ex) { Log.Add(InteractionLogKind.Error, h.Rec.SourceId, ex.Message); }
                }
            }
            finally { ExitProc(); }
        }

        public void NotifyPlayerDamaged(int halvesLost)
        {
            Stats.Invalidate();
            var hooks = _hooks.Get(this).OnDamaged;
            if (hooks.Length == 0 || !EnterProc("damaged")) return;
            try { for (int i = 0; i < hooks.Length; i++) if (hooks[i].Rec.IsEnabled) Safe(hooks[i].Rec, () => hooks[i].Hook.OnPlayerDamaged(halvesLost)); }
            finally { ExitProc(); }
            ReevaluateCombos();
        }

        public void NotifyRoomEntered(in RoomContext room)
        {
            CurrentRoomType = room.RoomType;
            var hooks = _hooks.Get(this).RoomEntered;
            if (EnterProc("room-enter"))
            {
                try
                {
                    for (int i = 0; i < hooks.Length; i++)
                    {
                        if (!hooks[i].Rec.IsEnabled) continue;
                        var r = room;
                        Safe(hooks[i].Rec, () => hooks[i].Hook.OnRoomEntered(r));
                    }
                }
                finally { ExitProc(); }
            }
            ReevaluateCombos();
        }

        public void NotifyRoomCleared(in RoomContext room)
        {
            InCombat = false;
            if (_active?.Definition.Active?.ChargeType == ActiveChargeType.Rooms) AddActiveCharge(1);
            var hooks = _hooks.Get(this).RoomCleared;
            if (hooks.Length == 0 || !EnterProc("room-clear")) return;
            try
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    if (!hooks[i].Rec.IsEnabled) continue;
                    var r = room;
                    Safe(hooks[i].Rec, () => hooks[i].Hook.OnRoomCleared(r));
                }
            }
            finally { ExitProc(); }
        }

        public void NotifyFloorStarted(int floorIndex)
        {
            FloorIndex = floorIndex;
            var hooks = _hooks.Get(this).FloorStarted;
            if (EnterProc("floor"))
            {
                try { for (int i = 0; i < hooks.Length; i++) if (hooks[i].Rec.IsEnabled) Safe(hooks[i].Rec, () => hooks[i].Hook.OnFloorStarted(floorIndex)); }
                finally { ExitProc(); }
            }
            ReevaluateCombos();
        }

        public void ModifyBomb(BombSpec bomb)
        {
            var mods = _hooks.Get(this).Bomb;
            for (int i = 0; i < mods.Length; i++) if (mods[i].Rec.IsEnabled) mods[i].Hook.ModifyBomb(bomb);
            if (bomb.ClusterCount > 6) bomb.ClusterCount = 6;
            if (bomb.Radius > 6f) bomb.Radius = 6f;
        }

        public void NotifyBombExploded(Vec2 position, BombSpec bomb)
        {
            var hooks = _hooks.Get(this).BombExploded;
            if (hooks.Length == 0 || !EnterProc("bomb")) return;
            try { for (int i = 0; i < hooks.Length; i++) if (hooks[i].Rec.IsEnabled) Safe(hooks[i].Rec, () => hooks[i].Hook.OnBombExploded(position, bomb)); }
            finally { ExitProc(); }
        }

        public void NotifyPickup(PickupKind kind)
        {
            var hooks = _hooks.Get(this).Pickup;
            if (hooks.Length > 0 && EnterProc("pickup"))
            {
                try { for (int i = 0; i < hooks.Length; i++) if (hooks[i].Rec.IsEnabled) Safe(hooks[i].Rec, () => hooks[i].Hook.OnPickup(kind)); }
                finally { ExitProc(); }
            }
            Stats.Invalidate();
        }

        public int GetShopPrice(ItemDefinition item, int basePrice)
        {
            int price = basePrice;
            var mods = _hooks.Get(this).Shop;
            for (int i = 0; i < mods.Length; i++) if (mods[i].Rec.IsEnabled) mods[i].Hook.ModifyPrice(ref price, item);
            return Math.Max(1, price);
        }

        public float GetDropChance(PickupKind kind, float baseChance)
        {
            float c = baseChance;
            var mods = _hooks.Get(this).Drop;
            for (int i = 0; i < mods.Length; i++) if (mods[i].Rec.IsEnabled) mods[i].Hook.ModifyDropChance(ref c, kind);
            return Math.Max(0f, Math.Min(1f, c));
        }

        // ------------------------------------------------------------------ active item

        public int ActiveMaxCharge => _active?.Definition.Active?.MaxCharge ?? 0;
        public bool ActiveReady => _active != null && _active.Definition.Active != null && ActiveCharge >= ActiveMaxCharge;

        private void ResetActiveCharge(ItemDefinition def)
        {
            ActiveTimer = 0f;
            ActiveCharge = def.Active != null && def.Active.StartsCharged ? def.Active.MaxCharge : 0;
            ActiveChargeChanged?.Invoke();
        }

        public void AddActiveCharge(int amount)
        {
            if (_active?.Definition.Active == null || amount == 0) return;
            int old = ActiveCharge;
            ActiveCharge = Math.Max(0, Math.Min(ActiveMaxCharge, ActiveCharge + amount));
            if (old != ActiveCharge) ActiveChargeChanged?.Invoke();
        }

        private void TickActiveCharge(float dt)
        {
            var spec = _active?.Definition.Active;
            if (spec == null || spec.ChargeType != ActiveChargeType.Seconds || ActiveCharge >= spec.MaxCharge) return;
            ActiveTimer += dt;
            while (ActiveTimer >= 1f && ActiveCharge < spec.MaxCharge) { ActiveTimer -= 1f; AddActiveCharge(1); }
        }

        /// <summary>Uses the held active item. Returns true if it fired (charge is spent only on success).</summary>
        public bool UseActive(Vec2 position, Vec2 aim)
        {
            if (!ActiveReady) return false;
            var owned = _active;
            var use = new ActiveUseContext { ItemId = owned.Id, Position = position, Aim = aim, Stacks = owned.Stacks };
            bool any = false;
            if (!EnterProc("active")) return false;
            try
            {
                foreach (var rec in owned.ActiveUseRecords)
                {
                    if (rec.Effect is IActiveEffect a && rec.ConditionMet)
                    {
                        try { any |= a.Use(use); }
                        catch (Exception ex) { Log.Add(InteractionLogKind.Error, rec.SourceId, ex.Message); }
                    }
                }
            }
            finally { ExitProc(); }
            if (!any) return false;

            Log.Add(InteractionLogKind.Active, owned.Id, "used");
            if (owned.Definition.Active.ChargeType == ActiveChargeType.OneShot)
            {
                RemoveOwned(owned);
                ReevaluateCombos();
            }
            else
            {
                ActiveCharge = 0;
                ActiveTimer = 0f;
                ActiveChargeChanged?.Invoke();
            }
            var hooks = _hooks.Get(this).ActiveUsed;
            for (int i = 0; i < hooks.Length; i++) if (hooks[i].Rec.IsEnabled) Safe(hooks[i].Rec, () => hooks[i].Hook.OnActiveUsed(use.ItemId));
            return true;
        }

        private void Safe(EffectRecord rec, Action a)
        {
            try { a(); }
            catch (Exception ex) { Log.Add(InteractionLogKind.Error, rec.SourceId, ex.Message); }
        }

        // ------------------------------------------------------------------ debugging

        public InteractionSnapshot GetSnapshot()
        {
            var snap = new InteractionSnapshot();
            foreach (var o in _passives) snap.Items.Add(o.ToString());
            foreach (var o in _trinkets) snap.Items.Add("[trinket] " + o);
            if (_active != null) snap.Items.Add($"[active] {_active} ({ActiveCharge}/{ActiveMaxCharge})");
            foreach (var r in _allRecords)
                snap.Effects.Add(new InteractionSnapshot.EffectLine
                {
                    Source = r.Context.SourceKind + ":" + r.SourceId,
                    Type = r.Type,
                    Enabled = r.IsEnabled,
                    SuppressedBy = r.SuppressedBy,
                    ConditionMet = r.ConditionMet,
                    Condition = r.Condition,
                });
            foreach (var s in _activeSynergies) snap.Synergies.Add(s.Id);
            foreach (var t in _activeTransformations) snap.Transformations.Add(t.Id);
            foreach (var m in _hooks.Get(this).Projectile) snap.ProjectilePipeline.Add($"{m.Hook.Order,4} {m.Rec.SourceId}/{m.Rec.Type}{(m.Rec.IsEnabled ? "" : " (off)")}");
            snap.Log.AddRange(Log.Entries());
            return snap;
        }

        internal IReadOnlyList<EffectRecord> AllRecords => _allRecords;

        // ------------------------------------------------------------------ hook cache

        private sealed class HookCache
        {
            public struct H<T> { public EffectRecord Rec; public T Hook; }

            public H<IProjectileModifier>[] Projectile = Array.Empty<H<IProjectileModifier>>();
            public H<ICompanionShotModifier>[] CompanionShot = Array.Empty<H<ICompanionShotModifier>>();
            public H<IOutgoingDamageModifier>[] Outgoing = Array.Empty<H<IOutgoingDamageModifier>>();
            public H<IIncomingDamageModifier>[] Incoming = Array.Empty<H<IIncomingDamageModifier>>();
            public H<IOnHitHook>[] OnHit = Array.Empty<H<IOnHitHook>>();
            public H<IOnKillHook>[] OnKill = Array.Empty<H<IOnKillHook>>();
            public H<IOnPlayerDamagedHook>[] OnDamaged = Array.Empty<H<IOnPlayerDamagedHook>>();
            public H<IOnRoomEnteredHook>[] RoomEntered = Array.Empty<H<IOnRoomEnteredHook>>();
            public H<IOnRoomClearedHook>[] RoomCleared = Array.Empty<H<IOnRoomClearedHook>>();
            public H<IOnFloorStartedHook>[] FloorStarted = Array.Empty<H<IOnFloorStartedHook>>();
            public H<IBombModifier>[] Bomb = Array.Empty<H<IBombModifier>>();
            public H<IOnBombExplodedHook>[] BombExploded = Array.Empty<H<IOnBombExplodedHook>>();
            public H<IOnPickupHook>[] Pickup = Array.Empty<H<IOnPickupHook>>();
            public H<IOnActiveUsedHook>[] ActiveUsed = Array.Empty<H<IOnActiveUsedHook>>();
            public H<IShopPriceModifier>[] Shop = Array.Empty<H<IShopPriceModifier>>();
            public H<IDropChanceModifier>[] Drop = Array.Empty<H<IDropChanceModifier>>();
            public H<ITickHook>[] Tick = Array.Empty<H<ITickHook>>();

            private bool _dirty = true;
            public void MarkDirty() => _dirty = true;

            public HookCache Get(InteractionEngine engine)
            {
                if (_dirty) Rebuild(engine._allRecords);
                return this;
            }

            private static H<T>[] Collect<T>(List<EffectRecord> records) where T : class
            {
                var list = new List<H<T>>();
                var seen = new HashSet<IEffect>();
                foreach (var r in records)
                    if (r.Effect is T t && seen.Add(r.Effect)) list.Add(new H<T> { Rec = r, Hook = t });
                return list.ToArray();
            }

            private void Rebuild(List<EffectRecord> records)
            {
                // records are already in registration order, so ties are deterministic
                var proj = Collect<IProjectileModifier>(records);
                Array.Sort(proj, (a, b) => a.Hook.Order != b.Hook.Order ? a.Hook.Order.CompareTo(b.Hook.Order) : a.Rec.Sequence.CompareTo(b.Rec.Sequence));
                Projectile = proj;
                CompanionShot = Collect<ICompanionShotModifier>(records);
                Outgoing = Collect<IOutgoingDamageModifier>(records);
                Incoming = Collect<IIncomingDamageModifier>(records);
                OnHit = Collect<IOnHitHook>(records);
                OnKill = Collect<IOnKillHook>(records);
                OnDamaged = Collect<IOnPlayerDamagedHook>(records);
                RoomEntered = Collect<IOnRoomEnteredHook>(records);
                RoomCleared = Collect<IOnRoomClearedHook>(records);
                FloorStarted = Collect<IOnFloorStartedHook>(records);
                Bomb = Collect<IBombModifier>(records);
                BombExploded = Collect<IOnBombExplodedHook>(records);
                Pickup = Collect<IOnPickupHook>(records);
                ActiveUsed = Collect<IOnActiveUsedHook>(records);
                Shop = Collect<IShopPriceModifier>(records);
                Drop = Collect<IDropChanceModifier>(records);
                Tick = Collect<ITickHook>(records);
                _dirty = false;
            }
        }
    }
}
