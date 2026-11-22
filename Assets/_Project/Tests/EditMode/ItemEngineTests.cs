using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Content;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;
using Hollowcrawl.Stats;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class ItemEngineTests
    {
        private static ProjectileSpec Shot(InteractionEngine e, float charge = 0f)
        {
            var spec = new ProjectileSpec();
            e.BuildShot(spec, new ShotContext { ChargeFraction = charge });
            return spec;
        }

        [Test]
        public void StatItemsApplyAndRemoveCleanly()
        {
            var e = TestContent.NewEngine(out _);
            float speed = e.Stats[StatId.MoveSpeed];
            e.AddItem(TestContent.Item("quickstep_boots"));
            Assert.AreEqual(speed + 0.25f, e.Stats[StatId.MoveSpeed], 1e-4);
            e.RemoveItem("quickstep_boots");
            Assert.AreEqual(speed, e.Stats[StatId.MoveSpeed], 1e-4);
            Assert.AreEqual(0, e.AllRecordsCount());
        }

        [Test]
        public void UniqueItemsRefuseDuplicatesAndStackableItemsStack()
        {
            var e = TestContent.NewEngine(out var world);
            Assert.AreEqual(AddItemResult.Added, e.AddItem(TestContent.Item("needle_quill")));
            Assert.AreEqual(AddItemResult.RefusedUnique, e.AddItem(TestContent.Item("needle_quill")));
            e.AddItem(TestContent.Item("lantern_wisp"));
            Assert.AreEqual(1, world.Companions.Count, "first wisp spawned: " + e.GetSnapshot());
            Assert.AreEqual(AddItemResult.Stacked, e.AddItem(TestContent.Item("lantern_wisp")));
            Assert.AreEqual(2, e.GetStacks("lantern_wisp"));
            Assert.AreEqual(2, world.Companions.Count, "one wisp per stack, rebuilt without duplicates");
            e.RemoveItem("lantern_wisp");
            Assert.AreEqual(1, world.Companions.Count);
        }

        [Test]
        public void ProjectilePipelineCombinesBehaviours()
        {
            var e = TestContent.NewEngine(out _, withCombos: false);
            e.AddItem(TestContent.Item("seeker_moth"));
            e.AddItem(TestContent.Item("cinder_flask"));
            e.AddItem(TestContent.Item("splinter_seed"));
            e.AddItem(TestContent.Item("echo_chamber"));
            var s = Shot(e);
            Assert.Greater(s.HomingStrength, 0f);
            Assert.Greater(s.ExplosionRadius, 0f);
            Assert.AreEqual(2, s.SplitCount);
            Assert.AreEqual(2, s.ShotsPerVolley);
            Assert.IsTrue((s.Types & DamageType.Explosive) != 0);
            Assert.AreEqual(e.Stats[StatId.Damage] * 0.85f, s.Damage, 1e-4);
        }

        [Test]
        public void SynergyReplacesDeliveryAndSuppressesIngredient()
        {
            var e = TestContent.NewEngine(out _);
            e.AddItem(TestContent.Item("needle_quill"));
            var before = Shot(e);
            Assert.AreEqual(1, before.PierceCount);
            Assert.AreEqual(ProjectileDelivery.Bolt, before.Delivery);

            string activated = null;
            e.SynergyActivated += s => activated = s.Id;
            e.AddItem(TestContent.Item("ember_sigil"));
            Assert.AreEqual("sunlance", activated);
            var after = Shot(e);
            Assert.AreEqual(ProjectileDelivery.Beam, after.Delivery);
            Assert.AreEqual(-1, after.PierceCount, "beams pierce everything");
            Assert.Contains("synergy:sunlance", after.VisualKeys);
            var snap = e.GetSnapshot();
            Assert.IsTrue(snap.Effects.Any(x => x.Type == "proj_pierce" && x.SuppressedBy == "sunlance"));

            e.RemoveItem("ember_sigil");
            Assert.IsFalse(e.IsSynergyActive("sunlance"));
            Assert.AreEqual(ProjectileDelivery.Bolt, Shot(e).Delivery);
            Assert.AreEqual(1, Shot(e).PierceCount, "suppression lifts when the synergy ends");
        }

        [Test]
        public void TagSynergyRewritesStatusesAndTransformationPersists()
        {
            var e = TestContent.NewEngine(out _);
            e.AddItem(TestContent.Item("ember_sigil"));
            e.AddItem(TestContent.Item("cinder_flask"));
            Assert.IsTrue(e.IsSynergyActive("pyre"));
            var s = Shot(e);
            Assert.IsTrue(s.Statuses.Any(x => x.StatusId == "scorch"));
            Assert.IsFalse(s.Statuses.Any(x => x.StatusId == "burn"));
            Assert.AreEqual(0, e.ActiveTransformationCount);

            // A third fire item triggers the transformation, which stays even if the items go.
            var extraFire = new ItemDefinition { Id = "test_fire", Name = "t", Description = "t", Tags = new[] { "fire" },
                Effects = { new EffectSpec("stat", new ParamBag().Set("stat", "Luck").Set("value", 0.0)) } };
            TransformationDefinition triggered = null;
            e.TransformationTriggered += t => triggered = t;
            float dmg = e.Stats[StatId.Damage];
            e.AddItem(extraFire);
            Assert.AreEqual("ashen_herald", triggered?.Id);
            Assert.AreEqual(dmg + 0.5f, e.Stats[StatId.Damage], 1e-4);
            e.RemoveItem("test_fire"); e.RemoveItem("cinder_flask");
            Assert.IsTrue(e.HasTransformation("ashen_herald"));
            Assert.IsFalse(e.IsSynergyActive("pyre"));

            var fire = new DamageInfo { Amount = 1, Types = DamageType.Fire };
            e.ModifyIncomingDamage(ref fire);
            Assert.IsTrue(fire.Cancelled, "Ashen Herald grants fire immunity");
        }

        [Test]
        public void ConditionalEffectsFollowGameState()
        {
            var e = TestContent.NewEngine(out _);
            e.AddItem(TestContent.Item("glass_fang"));
            float full = Shot(e).Damage;
            e.Health.HitInvulnerability = 0f;
            e.Health.TakeDamage(1, 0f, null);
            float hurt = Shot(e).Damage;
            Assert.AreEqual(full / 1.6f, hurt, 1e-3);
            e.Health.Heal(1);
            Assert.AreEqual(full, Shot(e).Damage, 1e-3);
        }

        [Test]
        public void ReactiveSpawningIsBoundedByGenerationAndBudget()
        {
            var e = TestContent.NewEngine(out var world, withCombos: false);
            var burst = new ItemDefinition { Id = "test_burst", Name = "t", Description = "t",
                Effects = { new EffectSpec("onhit_burst", new ParamBag().Set("chance", 1.0).Set("count", 8)) } };
            e.AddItem(burst);
            // Simulate every spawned projectile hitting immediately: a naive engine would recurse forever.
            int hits = 0;
            world.OnSpawn = spec =>
            {
                hits++;
                e.NotifyHit(new HitContext { Projectile = spec, Generation = spec.Generation, DamageDealt = spec.Damage });
            };
            var root = Shot(e);
            e.NotifyHit(new HitContext { Projectile = root, Generation = 0, DamageDealt = root.Damage });
            Assert.Greater(world.Projectiles.Count, 0);
            Assert.LessOrEqual(world.Projectiles.Count, e.SpawnBudgetPerFrame);
            Assert.IsTrue(world.Projectiles.All(p => p.Generation <= ProjectileSpec.MaxGeneration));
            Assert.Greater(e.Log.GuardCount, 0, "guards should report when they trip");

            e.Tick(0.016f); // new frame resets budget
            int before = world.Projectiles.Count;
            world.OnSpawn = null;
            e.NotifyHit(new HitContext { Projectile = root, Generation = 0, DamageDealt = 1 });
            Assert.AreEqual(before + 8, world.Projectiles.Count);
        }

        [Test]
        public void CompanionsInheritPlayerShotModifiers()
        {
            var e = TestContent.NewEngine(out _);
            e.AddItem(TestContent.Item("venom_gland"));
            var plain = new ProjectileSpec();
            e.BuildShot(plain, new ShotContext { IsCompanion = true, CompanionId = "wisp" });
            Assert.AreEqual(0, plain.Statuses.Count);
            e.AddItem(TestContent.Item("kin_bond"));
            var inherited = new ProjectileSpec();
            e.BuildShot(inherited, new ShotContext { IsCompanion = true, CompanionId = "wisp" });
            Assert.IsTrue(inherited.Statuses.Any(s => s.StatusId == "poison"));
        }

        [Test]
        public void ActiveItemChargesPerRoomAndSwaps()
        {
            var e = TestContent.NewEngine(out var world);
            e.AddItem(TestContent.Item("storm_bell"));
            Assert.IsTrue(e.ActiveReady);
            Assert.IsTrue(e.UseActive(new Vec2(), new Vec2(1, 0)));
            Assert.AreEqual(1, world.AreaDamage.Count);
            Assert.IsFalse(e.UseActive(new Vec2(), new Vec2(1, 0)));
            for (int i = 0; i < 3; i++) e.NotifyRoomCleared(new RoomContext { RoomType = "Combat" });
            Assert.IsTrue(e.ActiveReady);

            Assert.AreEqual(AddItemResult.Swapped, e.AddItem(TestContent.Item("mending_lantern"), out var displaced));
            Assert.AreEqual("storm_bell", displaced.Id);
            Assert.IsFalse(e.UseActive(new Vec2(), new Vec2(1, 0)), "full health: heal does nothing and keeps its charge");
            Assert.IsTrue(e.ActiveReady);
        }

        [Test]
        public void TrinketsEconomyAndBombs()
        {
            var e = TestContent.NewEngine(out _);
            var shopItem = TestContent.Item("iron_heart");
            Assert.AreEqual(15, e.GetShopPrice(shopItem, 15));
            e.AddItem(TestContent.Item("brass_tally"));
            Assert.AreEqual(13, e.GetShopPrice(shopItem, 15));
            Assert.AreEqual(AddItemResult.Swapped, e.AddItem(TestContent.Item("gambler_knuckle"), out var dropped));
            Assert.AreEqual("brass_tally", dropped.Id, "one trinket slot by default");
            Assert.AreEqual(1f, e.Stats[StatId.Luck], 1e-4);

            e.AddItem(TestContent.Item("sapper_satchel"));
            Assert.AreEqual(5, e.Resources.Get(ResourceKind.Bombs));
            var bomb = new BombSpec { Damage = 10f };
            e.ModifyBomb(bomb);
            Assert.AreEqual(25f, bomb.Damage, 1e-4);
            Assert.AreEqual(1.6f * 1.4f, bomb.Radius, 1e-4);
            // Re-acquiring after loss must not grant the one-time bombs again.
            e.RemoveItem("sapper_satchel");
            e.AddItem(TestContent.Item("sapper_satchel"));
            Assert.AreEqual(5, e.Resources.Get(ResourceKind.Bombs));
        }

        [Test]
        public void HealthContainerItems()
        {
            var e = TestContent.NewEngine(out _);
            e.AddItem(TestContent.Item("iron_heart"));
            Assert.AreEqual(4, e.Health.Containers);
            Assert.AreEqual(8, e.Health.Vigor);
        }

        [Test]
        public void UnknownEffectTypesAreLoggedNotThrown()
        {
            var e = TestContent.NewEngine(out _);
            var bad = new ItemDefinition { Id = "bad", Name = "b", Description = "b", Effects = { new EffectSpec("does_not_exist") } };
            Assert.DoesNotThrow(() => e.AddItem(bad));
            Assert.AreEqual(1, e.Log.ErrorCount);
        }

        [Test]
        public void SameSeedSameProcs()
        {
            int Run(uint seed)
            {
                var e = TestContent.NewEngine(out var w, seed, withCombos: false);
                e.AddItem(new ItemDefinition { Id = "x", Name = "x", Description = "x",
                    Effects = { new EffectSpec("onkill_drop", new ParamBag().Set("chance", 0.3)) } });
                for (int i = 0; i < 200; i++) e.NotifyKill(new KillContext());
                return w.Pickups.Count;
            }
            Assert.AreEqual(Run(77), Run(77));
        }
    }

    internal static class EngineTestExtensions
    {
        public static int AllRecordsCount(this InteractionEngine e) => e.GetSnapshot().Effects.Count;
    }
}
