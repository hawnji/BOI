using System;
using Hollowcrawl.Combat;
using Hollowcrawl.Items;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Simulation
{
    /// <summary>Companion behaviours (data in <see cref="CompanionDefinition"/>).</summary>
    public sealed partial class RoomSimulation
    {
        private static readonly CompanionDefinition DefaultCompanion = new CompanionDefinition { Id = "default", Name = "Companion", Description = "-" };
        private bool _playerFiredThisFrame;

        private void StepCompanions(float dt)
        {
            if (Player == null) { _playerFiredThisFrame = false; return; }
            int followers = 0, orbitals = 0;
            foreach (var c in Companions)
            {
                var def = c.Definition ?? DefaultCompanion;
                if (def.Behavior == CompanionBehavior.Orbital) orbitals++; else followers++;
            }
            int fi = 0, oi = 0;
            for (int i = 0; i < Companions.Count; i++)
            {
                var c = Companions[i];
                var def = c.Definition ?? DefaultCompanion;
                c.Cooldown -= dt;
                switch (def.Behavior)
                {
                    case CompanionBehavior.Orbital:
                    {
                        c.OrbitAngle += dt * def.OrbitSpeed;
                        c.Position = Player.Position + new Vec2(def.OrbitRadius, 0f).Rotated(c.OrbitAngle + 360f / Math.Max(1, orbitals) * oi++);
                        ContactDamage(c, def, dt);
                        break;
                    }
                    case CompanionBehavior.Chaser:
                    {
                        var target = FindEnemy(c.TargetId);
                        if (target == null && FindNearestEnemies(c.Position, def.Range * 2f, -1, _nearest) > 0) { c.TargetId = _nearest[0]; target = FindEnemy(c.TargetId); }
                        var goal = target != null ? target.Position : FollowPoint(c, fi++, followers);
                        MoveToward(c, goal, def.Speed, dt);
                        if (target != null) ContactDamage(c, def, dt);
                        break;
                    }
                    case CompanionBehavior.Collector:
                    {
                        PickupActor best = null;
                        float bestD = def.Range;
                        foreach (var p in Pickups)
                        {
                            if (p.Removed || p.Pedestal || p.Price > 0 || !p.CanCollect) continue;
                            float d = (p.Position - c.Position).Length;
                            if (d < bestD) { bestD = d; best = p; }
                        }
                        MoveToward(c, best != null ? best.Position : FollowPoint(c, fi++, followers), def.Speed, dt);
                        if (best != null && (best.Position - c.Position).Length < c.Radius + best.Radius && _host.TryCollect(best))
                        {
                            best.Removed = true;
                            Engine.NotifyPickup(best.Pickup);
                            Emit(new SimEvent { Kind = SimEventKind.PickupCollected, ActorId = best.Id, Position = best.Position, Key = best.Pickup.ToString() });
                        }
                        break;
                    }
                    case CompanionBehavior.Guardian:
                    {
                        ShotActor threat = null;
                        float bestD = 6f;
                        foreach (var s in Shots)
                        {
                            if (s.Removed || s.Team != Team.Enemy) continue;
                            float d = (s.Position - Player.Position).Length;
                            if (d < bestD) { bestD = d; threat = s; }
                        }
                        var goal = threat != null ? Player.Position + (threat.Position - Player.Position).Normalized * 0.9f : FollowPoint(c, fi++, followers);
                        MoveToward(c, goal, def.Speed, dt);
                        break;
                    }
                    case CompanionBehavior.Bomber:
                    {
                        MoveToward(c, FollowPoint(c, fi++, followers), def.Speed, dt);
                        if (c.Cooldown > 0f || LivingEnemies == 0) break;
                        if (FindNearestEnemies(c.Position, def.Range, -1, _nearest) == 0 || !TryGetEnemyPosition(_nearest[0], out var ep)) break;
                        c.Cooldown = def.FireInterval;
                        // companion bombs never hurt the player
                        Bombs.Add(new BombActor { Id = _ids.Next(), Position = ep, Spec = new BombSpec { Damage = def.BombDamage * Math.Max(0.5f, Engine.Stats[StatId.Damage] / 3.5f), Radius = def.BombRadius, Fuse = 0.9f, Types = DamageType.Explosive }, Fuse = 0.9f, Radius = 0.25f, ViewKey = "bomb_companion", PlayerSafe = true });
                        break;
                    }
                    case CompanionBehavior.Provider:
                        MoveToward(c, FollowPoint(c, fi++, followers), def.Speed, dt);
                        break;
                    case CompanionBehavior.Mimic:
                    {
                        MoveToward(c, FollowPoint(c, fi++, followers), def.Speed, dt);
                        if (_playerFiredThisFrame && c.Cooldown <= 0f)
                        {
                            c.Cooldown = def.FireInterval;
                            FireCompanionShot(c, def, Player.Aim);
                        }
                        break;
                    }
                    default: // Shooter
                    {
                        MoveToward(c, FollowPoint(c, fi++, followers), def.Speed, dt);
                        if (c.Cooldown > 0f) break;
                        if (FindNearestEnemies(c.Position, def.Range, -1, _nearest) == 0 || !TryGetEnemyPosition(_nearest[0], out var ep)) break;
                        c.Cooldown = def.FireInterval;
                        FireCompanionShot(c, def, ep - c.Position);
                        break;
                    }
                }
            }
            _playerFiredThisFrame = false;
        }

        private void FireCompanionShot(CompanionActor c, CompanionDefinition def, Vec2 dir)
        {
            Engine.BuildShot(_companionSpec, new ShotContext { IsCompanion = true, CompanionId = c.CompanionId });
            _companionSpec.Damage *= def.DamageScale;
            if (def.ShotStatus != null) _companionSpec.Statuses.Add(new StatusApplication { StatusId = def.ShotStatus, Chance = def.ShotStatusChance <= 0f ? 1f : def.ShotStatusChance, Duration = 3f, Potency = 1f });
            SpawnPlayerShot(_companionSpec, c.Position, dir, true);
        }

        private Vec2 FollowPoint(CompanionActor c, int index, int count)
        {
            // trail behind the player, fanned out
            var back = Player.Movement.Facing.Length > 0.1f ? Player.Movement.Facing * -1f : new Vec2(0f, 1f);
            float spread = (index - (count - 1) * 0.5f) * 35f;
            return Player.Position + back.Rotated(spread) * (0.9f + 0.35f * index);
        }

        private static void MoveToward(CompanionActor c, Vec2 goal, float speed, float dt)
        {
            var d = goal - c.Position;
            float len = d.Length;
            if (len < 1e-3f) return;
            float step = Math.Min(len, speed * dt * Math.Max(1f, len * 0.6f)); // catch up faster when far behind
            c.Position = c.Position + d * (step / len);
        }

        private void ContactDamage(CompanionActor c, CompanionDefinition def, float dt)
        {
            if (def.ContactDps <= 0f) return;
            float dmg = def.ContactDps * dt * Math.Max(0.5f, Engine.Stats[StatId.Damage] / 3.5f);
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Removed || e.LastIntent.Invisible) continue;
                if ((e.Position - c.Position).Length < e.Radius + c.Radius)
                    DamageEnemy(e, dmg, DamageType.Physical, false, default, 0f, null, 1, true, dot: true);
            }
        }

        private bool CompanionBlocks(ShotActor s)
        {
            foreach (var c in Companions)
            {
                var def = c.Definition ?? DefaultCompanion;
                bool blocks = def.BlocksShots || def.Behavior == CompanionBehavior.Guardian;
                if (blocks && (c.Position - s.Position).Length < c.Radius + s.Radius) return true;
            }
            return false;
        }
    }
}
