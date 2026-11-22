using System;
using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Enemies;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Movement;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Simulation
{
    /// <summary>
    /// Deterministic gameplay for one room: player movement and shooting, enemies, projectiles, bombs,
    /// hazards, pickups and destructible tiles. It is the item engine's <see cref="IGameWorld"/> while the
    /// player is inside. Unity only feeds input and draws the actors, so the whole loop runs headless in
    /// tests (including scripted bot play).
    /// </summary>
    public sealed partial class RoomSimulation : IGameWorld, IProjectileQueries, Items.Effects.IRunWorld
    {
        public const int MaxShots = 600;
        public const int MaxEnemies = 60;
        public const int MaxPickups = 80;
        public const float BaseShotRadius = 0.16f;
        public const float CompanionDamageScale = 0.6f;

        public RoomNode Node { get; }
        public RoomGrid Grid { get; }
        public PlayerActor Player { get; private set; }
        public InteractionEngine Engine { get; }
        public SimEventQueue Events { get; }
        public DifficultyDefinition Difficulty { get; }
        public int FloorIndex { get; }
        public float Time { get; private set; }

        public readonly List<EnemyActor> Enemies = new List<EnemyActor>();
        public readonly List<ShotActor> Shots = new List<ShotActor>();
        public readonly List<PickupActor> Pickups = new List<PickupActor>();
        public readonly List<BombActor> Bombs = new List<BombActor>();
        public readonly List<HazardActor> Hazards = new List<HazardActor>();
        public readonly List<ExitActor> Exits = new List<ExitActor>();
        public readonly List<ShrineActor> Shrines = new List<ShrineActor>();
        /// <summary>Owned by the run (companions follow the player between rooms).</summary>
        public List<CompanionActor> Companions = new List<CompanionActor>();
        /// <summary>Room rule rolled when the room was first populated (null for most rooms).</summary>
        public RoomModifierDefinition Modifier;

        /// <summary>Set when the player steps into an open doorway this frame. The run performs the transition.</summary>
        public RoomDoor ExitRequested { get; private set; }

        private readonly IRoomHost _host;
        private readonly SeededRandom _rng;
        private readonly IdSource _ids;
        private readonly ProjectileSpec _shotSpec = new ProjectileSpec();
        private readonly ProjectileSpec _companionSpec = new ProjectileSpec();
        private readonly List<Vec2> _dirs = new List<Vec2>(32);
        private readonly int[] _nearest = new int[8];
        private readonly Queue<(Vec2 pos, float radius, float damage, Team team, DamageType types)> _pendingExplosions = new Queue<(Vec2, float, float, Team, DamageType)>();
        private readonly Dictionary<RoomDoor, bool> _doorOpen = new Dictionary<RoomDoor, bool>();
        private bool _processingExplosions;
        private float _doorBumpCooldown;
        private float _hazardTickCooldown;

        public RoomSimulation(IRoomHost host, RoomNode node, RoomGrid grid, InteractionEngine engine, SeededRandom rng,
            IdSource ids, SimEventQueue events, DifficultyDefinition difficulty, int floorIndex)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Node = node;
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _rng = rng ?? new SeededRandom(1);
            _ids = ids ?? new IdSource();
            Events = events ?? new SimEventQueue();
            Difficulty = difficulty ?? DifficultyDefinition.Normal();
            FloorIndex = floorIndex;
        }

        // ================================================================== setup

        public void PlacePlayer(PlayerActor player, Vec2 position)
        {
            Player = player;
            player.Position = position;
            player.Movement.Stop();
            player.Fire.Reset();
            ExitRequested = null;
            foreach (var c in Companions) c.Position = position;
        }

        /// <summary>Sets how a door looks and behaves from inside this room (hidden doors stay solid wall).</summary>
        public void SetDoor(RoomDoor door, DoorVisual visual)
        {
            if (visual == DoorVisual.Hidden) { RoomGrid.DoorTile(door.Socket, out int x, out int r); Grid.Set(x, r, TileKind.Wall); _doorOpen.Remove(door); return; }
            bool open = visual == DoorVisual.Open;
            bool changed = !_doorOpen.TryGetValue(door, out bool was) || was != open;
            _doorOpen[door] = open;
            Grid.SetDoor(door.Socket, open);
            if (changed)
            {
                RoomGrid.DoorTile(door.Socket, out int x, out int r);
                Emit(new SimEvent { Kind = SimEventKind.DoorChanged, TileX = x, TileRow = r, Flag = open, Key = door.Kind.ToString() });
            }
        }

        public EnemyActor SpawnEnemy(string id, Vec2 position, bool elite, string summonedBy = null)
        {
            if (Enemies.Count >= MaxEnemies || string.IsNullOrEmpty(id)) return null;
            var boss = _host.FindBoss(id);
            var def = boss?.Body ?? _host.FindEnemy(id);
            if (def == null) return null;
            if (Modifier != null && Modifier.AllElite && boss == null && summonedBy == null) elite = true;
            var scaling = EnemyScaling.Compute(def, FloorIndex, Difficulty, elite && boss == null);
            if (Modifier != null && boss == null) scaling.MaxHealth *= Modifier.EnemyHealthMultiplier;
            var e = new EnemyActor
            {
                Id = _ids.Next(), Position = position, Radius = def.Radius, Definition = def, Boss = boss, Scaling = scaling,
                Health = scaling.MaxHealth, MaxHealth = scaling.MaxHealth, Elite = elite && boss == null,
                Status = new StatusController(def.IsBoss || boss != null), Room = this, SummonedBy = summonedBy,
                ViewKey = string.IsNullOrEmpty(def.SpriteKey) ? id : def.SpriteKey,
            };
            var erng = _rng.Fork("enemy", e.Id);
            if (boss != null)
            {
                e.BossController = new BossController(boss, scaling, erng);
                e.Brain = e.BossController.Brain;
                var captured = e;
                e.BossController.PhaseStarted += (p, i) => Emit(new SimEvent { Kind = SimEventKind.BossPhaseStarted, ActorId = captured.Id, Position = captured.Position, Value = i, Key = p.TransitionKey });
                e.BossController.PhaseStarted += (p, i) => { foreach (var a in p.OnEnter) ExecuteAttack(captured, a, captured.Position - Player.Position); };
            }
            else e.Brain = new EnemyBrain(def, scaling, erng);
            e.Status.DamageTick += info => DamageEnemy(e, info.Amount, info.Types, false, default, 0f, null, 0, false, dot: true);
            Enemies.Add(e);
            return e;
        }

        public PickupActor SpawnPickupActor(PickupKind kind, Vec2 position, string contentId = null, int price = 0, bool pedestal = false, bool scatter = true)
        {
            if (Pickups.Count >= MaxPickups) return null;
            if (scatter) position = NearestOpen(position + new Vec2(_rng.Range(-0.5f, 0.5f), _rng.Range(-0.5f, 0.5f)));
            var p = new PickupActor
            {
                Id = _ids.Next(), Position = position, Pickup = kind, ContentId = contentId, Price = price, Pedestal = pedestal,
                Radius = pedestal ? 0.45f : 0.28f, ViewKey = pedestal ? "pedestal" : "pickup_" + kind.ToString().ToLowerInvariant(),
            };
            Pickups.Add(p);
            return p;
        }

        public ExitActor SpawnExit(Vec2 position, bool victory)
        {
            var x = new ExitActor { Id = _ids.Next(), Position = NearestOpen(position), Radius = 0.5f, IsVictory = victory, ViewKey = victory ? "exit_victory" : "exit_trapdoor" };
            Exits.Add(x);
            return x;
        }

        public ShrineActor SpawnShrine(Consumables.ShrineDefinition def, int option, Vec2 position)
        {
            var sh = new ShrineActor { Id = _ids.Next(), Definition = def, OptionIndex = option, Position = NearestOpen(position), Radius = 0.45f, ViewKey = def.SpriteKey ?? "shrine_" + def.Id };
            Shrines.Add(sh);
            return sh;
        }

        public HazardActor SpawnHazard(string hazardId, Vec2 position, float radius, float duration, int damage, bool hurtsPlayer = true)
        {
            var h = new HazardActor { Id = _ids.Next(), Position = position, Radius = radius, HazardId = hazardId, Remaining = duration, Damage = damage, HurtsPlayer = hurtsPlayer, ViewKey = "hazard_" + hazardId };
            Hazards.Add(h);
            return h;
        }

        public int LivingEnemies { get { int n = 0; foreach (var e in Enemies) if (!e.Removed && e.CountsForClear) n++; return n; } }

        public int CountAlive(string enemyId, int summonerId)
        {
            int n = 0;
            foreach (var e in Enemies) if (!e.Removed && e.Definition.Id == enemyId) n++;
            foreach (var e in Enemies) if (!e.Removed && e.Boss != null && e.Boss.Id == enemyId) n++;
            return n;
        }

        /// <summary>Leaving the room: transient actors vanish, persistent ones (pickups, tiles) remain for revisits.</summary>
        public void OnPlayerLeft()
        {
            Shots.Clear();
            Bombs.Clear();
            Hazards.RemoveAll(h => h.Remaining < 999f);
            _pendingExplosions.Clear();
            Player = null;
        }

        // ================================================================== step

        public void Step(float dt, in PlayerInputFrame input)
        {
            if (Player == null) return;
            Time += dt;
            ExitRequested = null;
            if (_doorBumpCooldown > 0f) _doorBumpCooldown -= dt;
            if (_hazardTickCooldown > 0f) _hazardTickCooldown -= dt;

            Engine.World = this;
            Engine.Tick(dt);
            Engine.Health?.Tick(dt);

            bool alive = Engine.Health == null || !Engine.Health.IsDead;
            if (alive) StepPlayer(dt, input);
            StepCompanions(dt);
            StepShots(dt);
            StepEnemies(dt);
            StepBombs(dt);
            StepHazards(dt);
            if (alive) StepPickups(dt);
            if (alive) StepShrines(dt);
            ProcessExplosions();
            Cleanup();
        }

        private void StepPlayer(float dt, in PlayerInputFrame input)
        {
            var p = Player;
            var stats = Engine.Stats;
            var surface = Grid.At(p.Position) == TileKind.Water && !p.Flying
                ? new SurfaceModifier { SpeedMultiplier = 0.65f, TractionMultiplier = 0.8f }
                : SurfaceModifier.Normal;
            var velocity = p.Movement.Step(dt, input.Move, stats[StatId.MoveSpeed], 1f, surface);
            var before = p.Position;
            p.Position = Grid.MoveCircle(p.Position, p.Radius, velocity * dt, p.Flying, out bool blocked);

            // doorways
            var tile = Grid.At(p.Position);
            if (tile == TileKind.DoorOpen) ExitRequested = DoorAt(p.Position);
            else if (blocked && _doorBumpCooldown <= 0f && input.Move.Length > 0.3f)
            {
                var probe = p.Position + input.Move.Normalized * (p.Radius + 0.2f);
                if (Grid.At(probe) == TileKind.DoorClosed)
                {
                    var door = DoorAt(probe);
                    if (door != null) { _doorBumpCooldown = 0.4f; _host.OnDoorBumped(door); }
                }
            }

            // firing
            if (input.Aim.Length > 0.1f) p.Aim = input.Aim.Normalized;
            Engine.BuildShot(_shotSpec, new ShotContext { ChargeFraction = 1f });
            // The platform layer resolves shoot modes (hold / toggle / aim-to-fire) into FireHeld via ShootingIntent.
            bool wantsFire = input.FireHeld;
            if (p.Fire.Update(dt, wantsFire, stats[StatId.FireRate], _shotSpec.ChargeTime, out float charge))
                FireVolley(p.Position, p.Aim, charge, velocity);

            if (input.ActiveItemPressed && Engine.ActiveItem != null)
            {
                if (Engine.UseActive(p.Position, p.Aim))
                    Emit(new SimEvent { Kind = SimEventKind.ActiveUsed, ActorId = p.Id, Position = p.Position, Key = Engine.ActiveItem?.Id });
            }

            if (input.BombPressed && Engine.Resources.TrySpend(ResourceKind.Bombs, 1)) { PlaceBomb(p.Position, true); _host.OnBombPlaced(); }

            // floor hazards
            if (!p.Flying && Grid.CircleOverlapsKind(p.Position, p.Radius * 0.6f, TileKind.Spikes)) HurtPlayer(1, DamageType.Physical, p.Position);
            if (Grid.CircleOverlapsKind(p.Position, p.Radius * 0.8f, TileKind.Fire)) HurtPlayer(1, DamageType.Fire, p.Position);
            foreach (var x in Exits)
                if (x.Active && Overlaps(p, x)) { x.Active = false; _host.OnExitReached(x); }
        }

        private void FireVolley(Vec2 origin, Vec2 aim, float charge, Vec2 shooterVelocity)
        {
            Engine.BuildShot(_shotSpec, new ShotContext { ChargeFraction = charge });
            FireController.VolleyDirections(aim, Math.Max(1, _shotSpec.ShotsPerVolley), _shotSpec.SpreadDegrees, _dirs);
            for (int i = 0; i < _dirs.Count; i++)
            {
                Engine.BuildShot(_shotSpec, new ShotContext { ChargeFraction = charge, VolleyIndex = i });
                SpawnPlayerShot(_shotSpec, origin + _dirs[i] * 0.3f, _dirs[i], false);
            }
            Emit(new SimEvent { Kind = SimEventKind.ShotFired, ActorId = Player.Id, Position = origin, Value = charge });
            _playerFiredThisFrame = true;
        }

        public ShotActor SpawnPlayerShot(ProjectileSpec spec, Vec2 origin, Vec2 dir, bool fromCompanion)
        {
            if (Shots.Count >= MaxShots) return null;
            var sim = new ProjectileSimulation();
            sim.Launch(spec, origin, dir);
            var s = new ShotActor
            {
                Id = _ids.Next(), Position = origin, Team = Team.Player, Sim = sim, FromCompanion = fromCompanion,
                Radius = BaseShotRadius * Math.Max(0.3f, spec.Size), ViewKey = spec.VisualKeys.Count > 0 ? spec.VisualKeys[0] : "shot_player",
            };
            Shots.Add(s);
            return s;
        }

        public ShotActor SpawnEnemyShot(Vec2 origin, Vec2 dir, float speed, int damage, float range = 14f, string viewKey = "shot_enemy")
        {
            if (Shots.Count >= MaxShots) return null;
            var s = new ShotActor { Id = _ids.Next(), Position = origin, Team = Team.Enemy, Velocity = dir.Normalized * speed, Damage = Math.Max(1, damage), RangeLeft = range, Radius = 0.2f, ViewKey = viewKey };
            Shots.Add(s);
            return s;
        }

        public void PlaceBomb(Vec2 position, bool byPlayer)
        {
            var spec = new BombSpec { Damage = Engine.Stats[StatId.BombDamage] };
            if (byPlayer) Engine.ModifyBomb(spec);
            Bombs.Add(new BombActor { Id = _ids.Next(), Position = position, Spec = spec, Fuse = spec.Fuse, PlacedByPlayer = byPlayer, Radius = 0.3f, ViewKey = "bomb" });
        }

        private void StepShots(float dt)
        {
            float luck = Engine.Stats[StatId.Luck];
            for (int i = 0; i < Shots.Count; i++)
            {
                var s = Shots[i];
                if (s.Removed) continue;
                if (s.Team == Team.Player) StepPlayerShot(s, dt, luck);
                else StepEnemyShot(s, dt);
            }
        }

        private void StepPlayerShot(ShotActor s, float dt, float luck)
        {
            var sim = s.Sim;
            var prev = sim.Position;
            if (!sim.Step(dt, this)) { ResolveOutcome(sim, s); s.Removed = true; return; }
            s.Position = sim.Position;
            if (!sim.IsAirborne)
            {
                Grid.WorldToTile(s.Position, out int tx, out int tr);
                var tile = Grid.Get(tx, tr);
                if (tile == TileKind.Fire) { BreakTile(tx, tr, byShot: true); sim.HitWall(default, false); ResolveOutcome(sim, s); s.Removed = !sim.Alive; if (s.Removed) return; }
                else if (RoomGrid.BlocksShot(tile, out bool boundary) && (!sim.Spec.Spectral || boundary))
                {
                    if (tile == TileKind.Prop) BreakTile(tx, tr, byShot: true);
                    sim.HitWall(Grid.HitNormal(prev, s.Position), boundary);
                    ResolveOutcome(sim, s);
                    if (!sim.Alive) { s.Removed = true; Emit(new SimEvent { Kind = SimEventKind.ShotImpact, Position = s.Position, ActorId = s.Id }); return; }
                    s.Position = prev; // bounced
                    return;
                }
                for (int e = 0; e < Enemies.Count && sim.Alive; e++)
                {
                    var en = Enemies[e];
                    if (en.Removed || en.LastIntent.Invisible) continue;
                    if ((en.Position - s.Position).Length > en.Radius + s.Radius) continue;
                    if (!sim.HitEnemy(en.Id, en.Position, _rng, luck, this)) continue;
                    var spec = sim.Spec;
                    DamageEnemy(en, sim.Outcome.Damage, spec.Types | DamageType.Physical, sim.Outcome.Critical, sim.Direction, spec.Knockback, spec, spec.Generation, s.FromCompanion);
                    ApplyShotStatuses(en, spec, luck);
                    float chain = sim.Outcome.ChainDamage;
                    foreach (var id in sim.Outcome.ChainTargets)
                    {
                        var t = FindEnemy(id);
                        if (t != null) DamageEnemy(t, chain, DamageType.Shock, false, default, 0f, spec, spec.Generation + 1, s.FromCompanion);
                    }
                    ResolveOutcome(sim, s);
                }
                if (!sim.Alive) s.Removed = true;
            }
        }

        private void ResolveOutcome(ProjectileSimulation sim, ShotActor s)
        {
            var o = sim.Outcome;
            if (o.Explode) QueueExplosion(o.ExplosionPosition, o.ExplosionRadius, o.ExplosionDamage, Team.Player, DamageType.Explosive);
            foreach (var req in o.Spawns) SpawnPlayerShot(req.Spec, req.Origin, req.Direction, s.FromCompanion);
            o.Spawns.Clear();
        }

        private void ApplyShotStatuses(EnemyActor e, ProjectileSpec spec, float luck)
        {
            if (e.Removed) return;
            foreach (var st in spec.Statuses)
            {
                float chance = st.Chance * (1f + Math.Max(0f, luck) * 0.1f);
                if (!_rng.Chance(chance)) continue;
                var def = _host.FindStatus(st.StatusId);
                if (def != null) e.Status.Apply(def, st.Duration, st.Potency <= 0f ? 1f : st.Potency, Player?.Id ?? 0);
            }
        }

        private void StepEnemyShot(ShotActor s, float dt)
        {
            var step = s.Velocity * dt;
            s.Position = s.Position + step;
            s.RangeLeft -= step.Length;
            if (s.RangeLeft <= 0f) { s.Removed = true; return; }
            if (RoomGrid.BlocksShot(Grid.At(s.Position), out bool boundary) && (!s.Spectral || boundary)) { s.Removed = true; Emit(new SimEvent { Kind = SimEventKind.ShotImpact, Position = s.Position }); return; }
            if (CompanionBlocks(s)) { s.Removed = true; Emit(new SimEvent { Kind = SimEventKind.ShotImpact, Position = s.Position }); return; }
            if (Player != null && (Player.Position - s.Position).Length < Player.Radius + s.Radius)
            {
                s.Removed = true;
                HurtPlayer(s.Damage, DamageType.Physical, s.Position - s.Velocity);
            }
        }

        private void StepEnemies(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Removed) continue;
                if (e.FlashTimer > 0f) e.FlashTimer -= dt;
                e.Status.Tick(dt);
                if (e.Removed) continue;
                if (Modifier != null && Modifier.EnemyRegenPerSecond > 0f && !e.IsBoss)
                    e.Health = Math.Min(e.MaxHealth, e.Health + e.MaxHealth * Modifier.EnemyRegenPerSecond * dt);
                if (e.BossController != null)
                {
                    e.BossController.Tick(dt);
                    if (e.BossController.UpdateHealth(e.HealthFraction)) { }
                }
                if (Player == null) continue;
                var intent = e.Brain.Step(dt, e);
                e.LastIntent = intent;
                if (intent.TelegraphStarted != null)
                    Emit(new SimEvent { Kind = SimEventKind.EnemyTelegraph, ActorId = e.Id, Position = e.Position, Key = intent.TelegraphStarted.TelegraphKey, AudioKey = intent.TelegraphStarted.AudioKey });
                if (intent.AttackFired != null) ExecuteAttack(e, intent.AttackFired, intent.AttackDirection);
                if (e.Removed) continue;

                if (Modifier != null && Modifier.EnemySpeedMultiplier != 1f) intent.Velocity = intent.Velocity * Modifier.EnemySpeedMultiplier;
                e.Velocity = intent.Velocity;
                var delta = (intent.Velocity + e.Knockback) * dt;
                e.Position = Grid.MoveCircle(e.Position, e.Radius, delta, e.Flying || intent.Invisible, out _);
                float decay = Math.Max(0f, 1f - dt * 8f);
                e.Knockback = e.Knockback * decay;

                if (intent.Contact && !intent.Invisible && e.Definition.ContactDamage > 0 && Overlaps(Player, e))
                    HurtPlayer(e.Definition.ContactDamage, DamageType.Physical, e.Position);
            }
        }

        private void ExecuteAttack(EnemyActor e, AttackSpec a, Vec2 aim)
        {
            if (Player == null) return;
            Emit(new SimEvent { Kind = SimEventKind.EnemyAttack, ActorId = e.Id, Position = e.Position, Key = a.Id, AudioKey = a.AudioKey });
            switch (a.Kind)
            {
                case AttackKind.Projectile:
                {
                    bool scalable = a.Pattern == ShotPattern.Spread || a.Pattern == ShotPattern.Ring || a.Pattern == ShotPattern.Spiral || a.Pattern == ShotPattern.RandomBurst;
                    int count = a.Count + (scalable ? e.Scaling.ExtraProjectiles : 0);
                    AttackPatterns.Directions(a.Pattern, aim, count, a.Spread, e.Brain.SpiralAngle, _rng, _dirs);
                    float speed = a.ProjectileSpeed * e.Scaling.ProjectileSpeedMultiplier;
                    for (int i = 0; i < _dirs.Count; i++)
                    {
                        var origin = e.Position + _dirs[i] * (e.Radius * 0.8f);
                        if (a.Pattern == ShotPattern.Wall) origin = origin + AttackPatterns.WallOffset(aim, i, _dirs.Count, 0.7f);
                        SpawnEnemyShot(origin, _dirs[i], speed, DifficultyDamage(a.Damage));
                    }
                    break;
                }
                case AttackKind.Summon:
                {
                    for (int i = 0; i < Math.Max(1, a.SummonCount); i++)
                    {
                        if (CountAlive(a.SummonId, e.Id) >= a.MaxAlive) break;
                        var pos = NearestOpen(e.Position + new Vec2(1f, 0f).Rotated(_rng.Range(0f, 360f)) * (e.Radius + 0.6f));
                        var spawned = SpawnEnemy(a.SummonId, pos, false, e.Definition.Id);
                        if (spawned != null) { spawned.Brain.Activate(); _host.OnEnemySpawned(spawned); }
                    }
                    break;
                }
                case AttackKind.Slam:
                    QueueExplosion(e.Position, a.Radius, 0f, Team.Enemy, DamageType.Physical, a.Damage);
                    break;
                case AttackKind.Explode:
                    QueueExplosion(e.Position, a.Radius, 0f, Team.Enemy, DamageType.Explosive, a.Damage);
                    Kill(e, DamageType.Explosive, 0, silentDrops: true);
                    break;
                case AttackKind.Hazard:
                    SpawnHazard(a.HazardId ?? "fire", e.Position, a.Radius, Math.Max(2f, a.Duration > 0f ? a.Duration : 6f), DifficultyDamage(Math.Max(1, a.Damage)));
                    break;
                case AttackKind.Teleport:
                    if (Grid.TryFindOpenTile(_rng, Player.Position, 3.5f, out var to)) e.Position = to;
                    break;
                case AttackKind.Beam:
                {
                    var dir = aim.Normalized;
                    var end = e.Position;
                    for (int k = 0; k < 120; k++) { var next = end + dir * 0.25f; if (RoomGrid.BlocksShot(Grid.At(next), out _)) break; end = next; }
                    if (DistanceToSegment(Player.Position, e.Position, end) < Player.Radius + 0.25f) HurtPlayer(DifficultyDamage(a.Damage), DamageType.Fire, e.Position);
                    Emit(new SimEvent { Kind = SimEventKind.Feedback, Position = e.Position, Key = "beam", Value = (end - e.Position).Length, ActorId = e.Id });
                    break;
                }
                case AttackKind.Dash:
                    break; // movement is driven by the brain
            }
        }

        private int DifficultyDamage(int baseDamage) => _rng.Chance(Difficulty.EnemyDamageBonusChance) ? baseDamage + 1 : baseDamage;

        private void StepBombs(float dt)
        {
            foreach (var b in Bombs)
            {
                if (b.Removed) continue;
                b.Fuse -= dt;
                if (b.Fuse > 0f) continue;
                b.Removed = true;
                QueueExplosion(b.Position, b.Spec.Radius, b.Spec.Damage, b.PlayerSafe ? Team.Player : Team.Neutral, b.Spec.Types | DamageType.Explosive);
                if (b.PlacedByPlayer) Engine.NotifyBombExploded(b.Position, b.Spec);
            }
        }

        private void StepHazards(float dt)
        {
            foreach (var h in Hazards)
            {
                if (h.Removed) continue;
                h.Remaining -= dt;
                if (h.Remaining <= 0f) { h.Removed = true; continue; }
                if (h.HurtsPlayer && Player != null && !Player.Flying && Overlaps(Player, h)) HurtPlayer(h.Damage, DamageType.Fire, h.Position);
            }
        }

        private void StepPickups(float dt)
        {
            for (int i = 0; i < Pickups.Count; i++) // collecting may spawn pickups (swaps), so index rather than foreach
            {
                var p = Pickups[i];
                if (p.Removed) continue;
                if (p.SpawnProtection > 0f) { p.SpawnProtection -= dt; continue; }
                if (!Overlaps(Player, p)) continue;
                if (_host.TryCollect(p))
                {
                    p.Removed = true;
                    Engine.NotifyPickup(p.Pickup);
                    Emit(new SimEvent { Kind = p.Pickup == PickupKind.Item ? SimEventKind.ItemCollected : SimEventKind.PickupCollected, ActorId = p.Id, Position = p.Position, Key = p.ContentId ?? p.Pickup.ToString() });
                }
            }
        }

        private void StepShrines(float dt)
        {
            foreach (var sh in Shrines)
            {
                if (sh.Removed) continue;
                if (sh.Cooldown > 0f) { sh.Cooldown -= dt; continue; }
                if (!Overlaps(Player, sh)) continue;
                sh.Cooldown = 1f;
                _host.UseShrine(sh);
            }
        }

        private void Cleanup()
        {
            Shrines.RemoveAll(x => x.Removed);
            Shots.RemoveAll(s => s.Removed);
            Bombs.RemoveAll(b => b.Removed);
            Hazards.RemoveAll(h => h.Removed);
            Pickups.RemoveAll(p => p.Removed);
            Enemies.RemoveAll(e => e.Removed);
        }

        // ================================================================== damage

        public void DamageEnemy(EnemyActor e, float amount, DamageType types, bool crit, Vec2 knockDir, float knockback,
            ProjectileSpec projectile, int generation, bool fromCompanion, bool dot = false)
        {
            if (e == null || e.Removed || amount <= 0f) return;
            var info = new DamageInfo { Amount = amount * e.Status.DamageTakenMultiplier, Types = types, SourceTeam = Team.Player, IsCritical = crit, Knockback = knockback, IsDamageOverTime = dot };
            var hit = new HitContext { TargetId = e.Id, Position = e.Position, IsCritical = crit, TargetIsBoss = e.IsBoss || e.Definition.IsBoss, Types = types, Projectile = projectile, Generation = generation, FromCompanion = fromCompanion };
            if (!dot) Engine.ModifyOutgoingDamage(ref info, hit);
            if (e.BossController != null && e.BossController.IsInvulnerable) info.Amount = 0f;
            if (Modifier != null && !e.IsBoss) info.Amount *= Modifier.EnemyDamageTakenMultiplier;
            if (info.Amount <= 0f) return;
            e.Health -= info.Amount;
            e.FlashTimer = 0.08f;
            if (knockback > 0f && knockDir.Length > 1e-4f)
                e.Knockback = e.Knockback + knockDir.Normalized * (knockback * 3f * (1f - Math.Max(0f, Math.Min(1f, e.Definition.KnockbackResistance))));
            hit.DamageDealt = info.Amount;
            hit.Killed = e.Health <= 0f;
            Emit(new SimEvent { Kind = SimEventKind.EnemyHit, ActorId = e.Id, Position = e.Position, Value = info.Amount, Flag = crit });
            if (e.BossController != null) e.BossController.UpdateHealth(Math.Max(0f, e.HealthFraction));
            if (!dot) Engine.NotifyHit(hit);
            if (e.Health <= 0f && !e.Removed) Kill(e, types, generation);
        }

        private void Kill(EnemyActor e, DamageType types, int generation, bool silentDrops = false)
        {
            if (e.Removed) return;
            e.Removed = true;
            if (Modifier != null && Modifier.ExplodeOnDeath && !e.IsBoss)
                QueueExplosion(e.Position, Modifier.DeathExplosionRadius, 0f, Team.Enemy, DamageType.Explosive, 1);
            foreach (var d in e.Definition.OnDeath)
            {
                switch (d.Kind)
                {
                    case DeathEffectKind.Explode:
                        QueueExplosion(e.Position, d.Radius, 0f, Team.Enemy, DamageType.Explosive, d.Damage);
                        break;
                    case DeathEffectKind.SpawnEnemies:
                    case DeathEffectKind.Split:
                        for (int i = 0; i < d.Count; i++)
                        {
                            var pos = NearestOpen(e.Position + new Vec2(0.5f, 0f).Rotated(360f / Math.Max(1, d.Count) * i));
                            var child = SpawnEnemy(d.EnemyId, pos, false, e.Definition.Id);
                            if (child != null) { child.Brain.Activate(); _host.OnEnemySpawned(child); }
                        }
                        break;
                    case DeathEffectKind.ProjectileRing:
                        AttackPatterns.Directions(ShotPattern.Ring, new Vec2(0f, 1f), d.Count + e.Scaling.ExtraProjectiles, 0f, 0f, _rng, _dirs);
                        foreach (var dir in _dirs) SpawnEnemyShot(e.Position, dir, d.ProjectileSpeed * e.Scaling.ProjectileSpeedMultiplier, d.Damage);
                        break;
                    case DeathEffectKind.Hazard:
                        SpawnHazard(d.HazardId ?? "fire", e.Position, d.Radius, 5f, Math.Max(1, d.Damage));
                        break;
                }
            }
            Emit(new SimEvent { Kind = SimEventKind.EnemyKilled, ActorId = e.Id, Position = e.Position, Key = e.Definition.Id, Flag = e.IsBoss });
            Engine.NotifyKill(new KillContext { TargetId = e.Id, EnemyId = e.Boss?.Id ?? e.Definition.Id, Position = e.Position, WasBoss = e.IsBoss || e.Definition.IsBoss, KillingTypes = types, Generation = generation });
            _host.OnEnemyKilled(e);
        }

        public void HurtPlayer(int halves, DamageType types, Vec2 from)
        {
            var health = Engine.Health;
            if (Player == null || health == null || health.IsDead || halves <= 0) return;
            if (health.InvulnerableTime > 0f) return;
            var info = DamageInfo.Simple(halves, types, Team.Enemy);
            Engine.ModifyIncomingDamage(ref info);
            if (info.Cancelled || info.Amount <= 0f) { Emit(new SimEvent { Kind = SimEventKind.PlayerBlocked, Position = Player.Position }); return; }
            int lost = health.TakeDamage((int)Math.Ceiling(info.Amount), Engine.Stats[StatId.Defense], _rng);
            if (lost <= 0) { Emit(new SimEvent { Kind = SimEventKind.PlayerBlocked, Position = Player.Position }); return; }
            var away = (Player.Position - from);
            if (away.Length > 1e-3f) Player.Movement.AddKnockback(away.Normalized * 7f);
            Emit(new SimEvent { Kind = SimEventKind.PlayerHurt, ActorId = Player.Id, Position = Player.Position, Value = lost });
            Engine.NotifyPlayerDamaged(lost);
            _host.OnPlayerDamaged(lost);
        }

        private void QueueExplosion(Vec2 pos, float radius, float damage, Team team, DamageType types, int playerHalves = 2)
        {
            _pendingExplosions.Enqueue((pos, radius, damage, team, types));
            _explosionPlayerDamage.Enqueue(playerHalves);
            if (!_processingExplosions) ProcessExplosions();
        }

        private readonly Queue<int> _explosionPlayerDamage = new Queue<int>();

        private void ProcessExplosions()
        {
            if (_processingExplosions) return;
            _processingExplosions = true;
            int guard = 0;
            try
            {
                while (_pendingExplosions.Count > 0 && guard++ < 48)
                {
                    var (pos, radius, damage, team, types) = _pendingExplosions.Dequeue();
                    int playerHalves = _explosionPlayerDamage.Count > 0 ? _explosionPlayerDamage.Dequeue() : 2;
                    Emit(new SimEvent { Kind = SimEventKind.Explosion, Position = pos, Value = radius, Flag = team == Team.Enemy });
                    if (team != Team.Enemy && damage > 0f)
                        for (int i = 0; i < Enemies.Count; i++)
                        {
                            var e = Enemies[i];
                            if (!e.Removed && (e.Position - pos).Length <= radius + e.Radius)
                                DamageEnemy(e, damage, types | DamageType.Explosive, false, e.Position - pos, 4f, null, 1, false);
                        }
                    if (team != Team.Player && Player != null && (Player.Position - pos).Length <= radius + Player.Radius)
                        HurtPlayer(playerHalves, types | DamageType.Explosive, pos);
                    if (team != Team.Enemy || radius >= 1f)
                        Grid.ForEachTileInRadius(pos, radius, (x, r) => { if (RoomGrid.IsBombable(Grid.Get(x, r))) BreakTile(x, r, byShot: false); });
                    if (team != Team.Enemy) RevealDoorsNear(pos, radius);
                }
                _pendingExplosions.Clear();
                _explosionPlayerDamage.Clear();
            }
            finally { _processingExplosions = false; }
        }

        private void BreakTile(int x, int row, bool byShot)
        {
            var kind = Grid.Get(x, row);
            if (byShot)
            {
                if (!Grid.DamageTile(x, row)) return;
            }
            else Grid.Set(x, row, TileKind.Floor);
            Emit(new SimEvent { Kind = SimEventKind.TileChanged, TileX = x, TileRow = row, Key = kind.ToString() });
            if (kind == TileKind.Explosive) QueueExplosion(Grid.TileCenter(x, row), 1.6f, 15f, Team.Neutral, DamageType.Explosive);
            _host.OnTileDestroyed(x, row, kind);
        }

        private void RevealDoorsNear(Vec2 pos, float radius)
        {
            if (Node == null) return;
            foreach (var d in Node.Doors)
            {
                if (d.Kind != DoorKind.Secret) continue;
                RoomGrid.DoorTile(d.Socket, out int x, out int r);
                if ((Grid.TileCenter(x, r) - pos).Length <= radius + 0.9f) _host.OnBlastNearDoor(d);
            }
        }

        // ================================================================== helpers

        private RoomDoor DoorAt(Vec2 p)
        {
            Grid.WorldToTile(p, out int x, out int r);
            if (Node == null) return null;
            foreach (var d in Node.Doors)
            {
                RoomGrid.DoorTile(d.Socket, out int dx, out int dr);
                if (dx == x && dr == r) return d;
            }
            return null;
        }

        public Vec2 NearestOpen(Vec2 p)
        {
            Grid.WorldToTile(p, out int x, out int r);
            if (Grid.IsWalkable(x, r)) return p;
            for (int radius = 1; radius < 12; radius++)
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        if (Grid.IsWalkable(x + dx, r + dy)) return Grid.TileCenter(x + dx, r + dy);
                    }
            return Grid.Center;
        }

        private static bool Overlaps(Actor a, Actor b) => (a.Position - b.Position).Length < a.Radius + b.Radius;

        private static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            float len2 = ab.X * ab.X + ab.Y * ab.Y;
            float t = len2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2));
            return (p - (a + ab * t)).Length;
        }

        public EnemyActor FindEnemy(int id)
        {
            foreach (var e in Enemies) if (e.Id == id && !e.Removed) return e;
            return null;
        }

        private void Emit(SimEvent e) => Events.Add(e);

        // ================================================================== IGameWorld / IProjectileQueries

        public Vec2 PlayerPosition => Player?.Position ?? Grid.Center;
        public Vec2 AimDirection => Player?.Aim ?? new Vec2(0f, -1f);
        public Vec2 ShooterPosition => PlayerPosition;

        public void SpawnProjectile(ProjectileSpec spec, Vec2 origin, Vec2 direction)
        {
            if (spec.Team == Team.Player) SpawnPlayerShot(spec, origin, direction, false);
            else SpawnEnemyShot(origin, direction, spec.Speed, Math.Max(1, (int)Math.Round(spec.Damage)), spec.Range);
        }

        public void SpawnExplosion(Vec2 position, float radius, float damage, Team team, DamageType types) => QueueExplosion(position, radius, damage, team, types);

        public void DamageEnemiesInRadius(Vec2 center, float radius, float damage, DamageType types)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Removed) continue;
                if (radius <= 0f || (e.Position - center).Length <= radius + e.Radius) DamageEnemy(e, damage, types, false, default, 0f, null, 1, false);
            }
        }

        public void ApplyStatusToEnemy(int enemyId, string statusId, float duration, float potency)
        {
            var e = FindEnemy(enemyId);
            var def = _host.FindStatus(statusId);
            if (e != null && def != null) e.Status.Apply(def, duration, potency, Player?.Id ?? 0);
        }

        public void ApplyStatusToEnemiesInRadius(Vec2 center, float radius, string statusId, float duration, float potency)
        {
            var def = _host.FindStatus(statusId);
            if (def == null) return;
            foreach (var e in Enemies)
                if (!e.Removed && (radius <= 0f || (e.Position - center).Length <= radius + e.Radius)) e.Status.Apply(def, duration, potency, Player?.Id ?? 0);
        }

        public void SpawnPickup(PickupKind kind, Vec2 position, string contentId = null) => SpawnPickupActor(kind, position, contentId);
        public void SpawnCompanion(string companionId, object source) => _host.SpawnCompanion(companionId, source);
        public void RemoveCompanionsFromSource(object source) => _host.RemoveCompanions(source);
        public void TeleportPlayer(TeleportTarget target) => _host.TeleportPlayer(target);
        public void RevealMap(bool includeSecrets) => _host.RevealMap(includeSecrets);
        public void PlayFeedback(string visualKey, string audioKey, Vec2 position) => Emit(new SimEvent { Kind = SimEventKind.Feedback, Key = visualKey, AudioKey = audioKey, Position = position });

        private readonly List<(float dist, int id)> _nearestScratch = new List<(float, int)>(16);

        public int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results)
        {
            _nearestScratch.Clear();
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Removed || e.Id == excludeId || e.LastIntent.Invisible) continue;
                float d = (e.Position - point).Length;
                if (d <= range) _nearestScratch.Add((d, e.Id));
            }
            _nearestScratch.Sort((x, y) => x.dist != y.dist ? x.dist.CompareTo(y.dist) : x.id.CompareTo(y.id));
            int count = Math.Min(results.Length, _nearestScratch.Count);
            for (int i = 0; i < count; i++) results[i] = _nearestScratch[i].id;
            return count;
        }

        public bool TryGetEnemyPosition(int enemyId, out Vec2 position)
        {
            var e = FindEnemy(enemyId);
            position = e?.Position ?? default;
            return e != null;
        }
    }

    public enum DoorVisual { Open, Closed, Locked, Hidden }
}
