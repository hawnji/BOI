namespace Hollowcrawl.Items.Effects
{
    /// <summary>Registers every built-in effect type. Keys are the "type" strings used in content JSON.</summary>
    public static class BuiltInEffects
    {
        public static void RegisterAll(EffectRegistry r)
        {
            // Stats
            r.Register("stat", s => new StatEffect(), "stat, op (Flat|PercentAdd|Multiply|Floor|Ceiling|OverrideBase), value, perStack");
            r.Register("stat_scaling", s => new StatScalingEffect(), "stat, perUnit, source (coins|bombs|keys|companions|missing_health|items|tag), tag, max");

            // Projectile modifiers
            r.Register("proj_pierce", s => new PierceEffect(), "count (-1 = infinite)");
            r.Register("proj_homing", s => new HomingEffect(), "strength");
            r.Register("proj_bounce", s => new BounceEffect(), "count");
            r.Register("proj_split", s => new SplitEffect(), "count, damageScale");
            r.Register("proj_explode", s => new ExplodeEffect(), "radius, damageScale");
            r.Register("proj_chain", s => new ChainEffect(), "jumps, range, damageScale");
            r.Register("proj_element", s => new ElementEffect(), "type, status, chance, duration, potency");
            r.Register("proj_size", s => new ShotSizeEffect(), "mult");
            r.Register("proj_volley", s => new VolleyEffect(), "shots, spread, damageScale");
            r.Register("proj_delivery", s => new DeliveryEffect(), "delivery (Bolt|Lobbed|Beam|Orbiting|Boomerang|Wave), priority");
            r.Register("proj_spectral", s => new SpectralEffect());
            r.Register("proj_charge", s => new ChargeEffect(), "time, damageMult");
            r.Register("proj_accelerate", s => new AccelerateEffect(), "perSecond");
            r.Register("proj_wavy", s => new WavyEffect(), "amplitude");
            r.Register("proj_damage_mult", s => new ShotDamageMultEffect(), "mult");
            r.Register("proj_visual", s => new ProjectileVisualEffect(), "key");
            r.Register("proj_convert_status", s => new ConvertStatusEffect(), "fromStatus, toStatus, potencyMult, durationMult");

            // Combat triggers
            r.Register("onhit_explode", s => new OnHitExplodeEffect(), "chance, radius, damageScale");
            r.Register("onhit_status_aoe", s => new OnHitStatusAoeEffect(), "chance, radius, status, duration, potency");
            r.Register("onhit_burst", s => new OnHitBurstEffect(), "chance, count, damageScale");
            r.Register("onkill_drop", s => new OnKillDropEffect(), "chance, pickup");
            r.Register("onkill_heal", s => new OnKillHealEffect(), "chance, halves");
            r.Register("onkill_explode", s => new OnKillExplodeEffect(), "chance, radius, damage");
            r.Register("onkill_spread_status", s => new OnKillSpreadStatusEffect(), "status, radius, duration, potency, requireType");
            r.Register("ondamaged_burst", s => new OnDamagedBurstEffect(), "chance, radius, damage, type, status, duration");
            r.Register("ondamaged_ward", s => new OnDamagedWardEffect(), "chance, halves");
            r.Register("ondamaged_charge", s => new OnDamagedChargeEffect(), "amount");
            r.Register("invuln_on_hit", s => new InvulnOnHitEffect(), "seconds");
            r.Register("outgoing_mult", s => new OutgoingMultEffect(), "mult, vsBoss, crit, types");
            r.Register("incoming_immunity", s => new IncomingImmunityEffect(), "types");
            r.Register("incoming_block_chance", s => new IncomingBlockEffect(), "chance");

            // Rooms, floors, economy
            r.Register("roomclear_drop", s => new RoomClearDropEffect(), "chance, pickup, requireNoDamage");
            r.Register("roomclear_heal", s => new RoomClearHealEffect(), "chance, halves");
            r.Register("roomclear_charge", s => new RoomClearChargeEffect(), "amount");
            r.Register("roomenter_status", s => new RoomEnterStatusEffect(), "status, duration, radius, chance");
            r.Register("floorstart_resource", s => new FloorStartResourceEffect(), "resource, amount");
            r.Register("grant_resource", s => new GrantResourceEffect(), "resource, amount");
            r.Register("grant_health", s => new GrantHealthEffect(), "containers, heal, ward, umbral");
            r.Register("pickup_bonus", s => new PickupBonusEffect(), "pickup, resource, amount, chance");
            r.Register("shop_discount", s => new ShopDiscountEffect(), "percent, flat");
            r.Register("drop_chance", s => new DropChanceEffect(), "pickup, mult, add");
            r.Register("trinket_slots", s => new TrinketSlotsEffect(), "add");

            // Bombs
            r.Register("bomb_mod", s => new BombModEffect(), "radiusMult, damageAdd, damageMult, fuseMult, homing, sticky, cluster, safe, type, status, statusDuration");
            r.Register("bomb_explode_shots", s => new BombShotsEffect(), "count, damageScale");

            // Companions
            r.Register("companion", s => new CompanionEffect(), "companion (companion id), count");
            r.Register("companion_inherit", s => new CompanionInheritEffect(), "scale");
            r.Register("companion_boost", s => new CompanionBoostEffect(), "damageMult, sizeMult");

            // Over time
            r.Register("regen", s => new RegenEffect(), "interval, halves, inCombatOnly");
            r.Register("aura_status", s => new AuraStatusEffect(), "radius, status, interval, duration, potency");
            r.Register("active_used_status", s => new ActiveUsedStatusEffect(), "status, radius, duration");

            // Active item uses
            r.Register("active_room_damage", s => new ActiveRoomDamageEffect(), "damage, damageStatScale, type, status, duration, radius");
            r.Register("active_heal", s => new ActiveHealEffect(), "halves, ward, umbral");
            r.Register("active_temp_stat", s => new ActiveTempStatEffect(), "stat, op, value, duration (0 = this room)");
            r.Register("active_projectile_ring", s => new ActiveProjectileRingEffect(), "count, damageScale");
            r.Register("active_teleport", s => new ActiveTeleportEffect(), "target");
            r.Register("active_spawn_pickups", s => new ActiveSpawnPickupsEffect(), "pickup, count");
            r.Register("active_companion_room", s => new ActiveCompanionRoomEffect(), "companion, count");
            r.Register("active_explosions", s => new ActiveExplosionsEffect(), "count, radius, damage");
            r.Register("active_perm_stat", s => new ActivePermanentStatEffect(), "stat, op=Flat, value");
            r.Register("active_resource", s => new ActiveResourceEffect(), "resource (Coins|Bombs|Keys|Embers), amount (may be negative)");
            r.Register("active_hurt_self", s => new ActiveHurtSelfEffect(), "halves (never lethal)");
            r.Register("active_reveal_map", s => new ActiveRevealMapEffect(), "secrets=false");
        }
    }
}
