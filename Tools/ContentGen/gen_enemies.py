#!/usr/bin/env python3
"""Generates enemies, bosses, minibosses and floors for depths 2-15 (plus one extra crypt enemy).

Outputs:
  Content/Enemies/expansion_enemies.json   (enemies + bosses incl. minibosses)
  Content/Floors/expansion_floors.json     (14 floors)
Run from the repo root: python3 Tools/ContentGen/gen_enemies.py
Each floor gets 8 enemies built from archetypes, 2 major bosses and 1 miniboss. All names are original.
"""
import json

# ------------------------------------------------------------------ archetypes
# Each returns an enemy dict (without id/name/description/sprite) given depth scaling.

def atk(id, kind, **kw):
    d = {"id": id, "kind": kind}
    d.update(kw)
    return d

def proj(id, pattern, count=1, speed=6.0, cd=2.2, tele=0.5, **kw):
    return atk(id, "Projectile", pattern=pattern, count=count, projectileSpeed=speed, cooldown=cd, telegraph=tele, recovery=kw.pop("recovery", 0.35), damage=kw.pop("damage", 1), **kw)

def mv(kind, speed, **kw):
    d = {"kind": kind, "speed": speed}
    d.update(kw)
    return d

def A_chaser(s):      return dict(health=10 * s, radius=0.42, tags=["melee"], movement=mv("Chase", 1.9))
def A_runner(s):      return dict(health=6 * s, radius=0.32, tags=["melee", "fast"], movement=mv("Chase", 2.9))
def A_brute(s):       return dict(health=22 * s, radius=0.6, knockbackResistance=0.7, tags=["melee", "heavy"], movement=mv("Chase", 1.3),
                                  attacks=[atk("charge", "Dash", cooldown=3.6, telegraph=0.7, duration=0.5, recovery=0.7, minRange=2, dashSpeed=9, telegraphKey="tele_stomp")])
def A_shooter(s):     return dict(health=9 * s, radius=0.4, tags=["ranged"], retreatBelowHealth=0.3, movement=mv("KeepDistance", 1.9, preferredDistance=5),
                                  attacks=[proj("shot", "Aimed", 1, 6.5, 2.0, maxRange=9, telegraphKey="tele_windup")])
def A_spreader(s):    return dict(health=11 * s, radius=0.42, tags=["ranged"], movement=mv("KeepDistance", 1.7, preferredDistance=4.5),
                                  attacks=[proj("fan", "Spread", 3, 5.5, 2.5, spread=18, maxRange=9, telegraphKey="tele_windup")])
def A_turret(s):      return dict(health=14 * s, radius=0.45, knockbackResistance=1.0, tags=["ranged", "stationary"], movement=mv("Idle", 0),
                                  attacks=[proj("ring", "Ring", 6, 4.5, 3.0, 0.6, requiresLineOfSight=False, telegraphKey="tele_pulse")])
def A_crosser(s):     return dict(health=12 * s, radius=0.45, knockbackResistance=0.9, tags=["ranged", "stationary"], movement=mv("Idle", 0),
                                  attacks=[proj("cross", "Cross", 1, 5.0, 2.6, 0.55, requiresLineOfSight=False), proj("xcross", "X", 1, 5.0, 2.6, 0.55, requiresLineOfSight=False)])
def A_flier(s):       return dict(health=6 * s, radius=0.3, tags=["flying"], movement=mv("Orbit", 3.0, preferredDistance=2.5, flying=True),
                                  attacks=[atk("swoop", "Dash", cooldown=2.8, telegraph=0.45, duration=0.35, recovery=0.4, maxRange=4.5, dashSpeed=9, telegraphKey="tele_flash")])
def A_drifter(s):     return dict(health=8 * s, radius=0.35, tags=["flying", "ranged"], movement=mv("Wander", 1.6, flying=True),
                                  attacks=[proj("spit", "Aimed", 1, 5.0, 2.4, requiresLineOfSight=False)])
def A_hopper(s):      return dict(health=10 * s, radius=0.4, tags=["melee"], movement=mv("Hop", 2.4, pauseTime=0.7, hopDistance=2.2),
                                  onDeath=[{"kind": "ProjectileRing", "count": 4, "projectileSpeed": 4, "damage": 1}])
def A_burrower(s):    return dict(health=12 * s, radius=0.42, tags=["burrow"], movement=mv("Burrow", 2.0, pauseTime=1.2),
                                  attacks=[proj("surface_ring", "Ring", 8, 4.0, 3.2, 0.5, requiresLineOfSight=False)])
def A_strafer(s):     return dict(health=9 * s, radius=0.38, tags=["ranged"], movement=mv("Strafe", 2.4, preferredDistance=4),
                                  attacks=[proj("burst", "Aimed", 1, 7.0, 2.6, 0.4, duration=0.5, waves=3)])
def A_bomber(s):      return dict(health=7 * s, radius=0.38, tags=["melee", "explosive"], movement=mv("Chase", 2.3),
                                  attacks=[atk("burst", "Explode", cooldown=1.0, telegraph=0.8, maxRange=1.3, radius=1.6, damage=2, telegraphKey="tele_fuse")])
def A_splitter(s, child): return dict(health=16 * s, radius=0.55, tags=["melee", "splitter"], movement=mv("Chase", 1.5),
                                  onDeath=[{"kind": "Split", "enemyId": child, "count": 2}])
def A_summoner(s, minion): return dict(health=15 * s, radius=0.48, tags=["summoner"], retreatBelowHealth=0.4, movement=mv("KeepDistance", 1.4, preferredDistance=6),
                                  attacks=[atk("call", "Summon", cooldown=4.5, telegraph=0.7, recovery=0.5, requiresLineOfSight=False, summonId=minion, summonCount=2, maxAlive=4, telegraphKey="tele_pulse")])
def A_slammer(s):     return dict(health=18 * s, radius=0.55, knockbackResistance=0.8, tags=["melee", "heavy"], movement=mv("Chase", 1.5),
                                  attacks=[atk("slam", "Slam", cooldown=3.2, telegraph=0.8, recovery=0.6, maxRange=2.0, radius=1.8, damage=1, telegraphKey="tele_stomp")])
def A_hazarder(s, hz): return dict(health=11 * s, radius=0.42, tags=["hazard"], movement=mv("Wander", 1.8),
                                  attacks=[atk("puddle", "Hazard", cooldown=3.0, telegraph=0.4, recovery=0.3, requiresLineOfSight=False, radius=1.0, hazardId=hz, duration=4, damage=1)])
def A_blinker(s):     return dict(health=9 * s, radius=0.4, tags=["ranged", "teleport"], movement=mv("KeepDistance", 1.5, preferredDistance=5),
                                  attacks=[atk("blink", "Teleport", cooldown=3.5, telegraph=0.5, recovery=0.2, requiresLineOfSight=False),
                                           proj("volley", "Spread", 4, 5.0, 2.5, spread=22)])
def A_beamer(s):      return dict(health=13 * s, radius=0.45, knockbackResistance=0.9, tags=["ranged", "stationary"], movement=mv("Idle", 0),
                                  attacks=[atk("beam", "Beam", cooldown=4.0, telegraph=1.0, duration=1.2, recovery=0.6, maxRange=12, damage=1, telegraphKey="tele_beam")])
def A_spiraler(s):    return dict(health=16 * s, radius=0.5, knockbackResistance=0.9, tags=["ranged"], movement=mv("Wander", 1.0),
                                  attacks=[proj("spiral", "Spiral", 2, 4.0, 4.5, 0.7, duration=1.6, waves=8, spiralStep=20, requiresLineOfSight=False)])
def A_waller(s):      return dict(health=14 * s, radius=0.45, tags=["ranged"], movement=mv("Patrol", 1.8),
                                  attacks=[proj("wall", "Wall", 5, 3.8, 3.8, 0.7, spread=10)])

# ------------------------------------------------------------------ floors
# (floor id, name, theme, element status immunity, hazard id, music, [8 enemies], [2 bosses], miniboss)
# enemy entry: (id, name, description, archetype key, extra-args)
FLOORS = [
 ("sunken_ossuary", "Sunken Ossuary", "ossuary", [], "spikes", [
   ("rib_crawler", "Rib Crawler", "A ribcage scuttling on finger bones.", "runner"),
   ("skull_lantern", "Skull Lantern", "A hollow skull with a candle inside; it spits wax.", "drifter"),
   ("marrow_brute", "Marrow Brute", "A heap of thigh bones that charges blindly.", "brute"),
   ("bone_reliquary", "Bone Reliquary", "A box of relics that fires shards in every direction.", "turret"),
   ("knuckle_hopper", "Knuckle Hopper", "Leaps on fused knuckles and bursts into chips.", "hopper"),
   ("ossuary_acolyte", "Ossuary Acolyte", "Calls rib crawlers from the walls.", "summoner", "rib_crawler"),
   ("vertebra_snake", "Vertebra Snake", "A spine that slithers sideways and spits.", "strafer"),
   ("crypt_bellows", "Crypt Bellows", "A bloated corpse that bursts into smaller ones.", "splitter", "rib_crawler")],
   [("osseous_matron", "The Osseous Matron", "Mother of the Stacked Dead"), ("hollow_sexton", "The Hollow Sexton", "Who Buries the Buriers")], ("knucklebone_king", "Knucklebone King")),
 ("mire_hollows", "Mire Hollows", "mire", ["poison"], "acid", [
   ("bog_leech", "Bog Leech", "A fat leech that hunts by heat.", "chaser"),
   ("mire_toad", "Mire Toad", "Hops close and bursts into spit.", "hopper"),
   ("spore_puff", "Spore Puff", "Floats about, puffing poison spores.", "drifter"),
   ("rot_wader", "Rot Wader", "Leaves acid pools wherever it walks.", "hazarder", "acid"),
   ("sludge_mound", "Sludge Mound", "Splits into smaller blobs when struck down.", "splitter", "bog_leech"),
   ("reed_spitter", "Reed Spitter", "Hides in the reeds and spits three darts.", "spreader"),
   ("mire_burrower", "Mire Burrower", "Swims under the mud and surfaces with a splash.", "burrower"),
   ("bloat_fly", "Bloat Fly", "A swollen fly that swoops and stings.", "flier")],
   [("the_drowned_mother", "The Drowned Mother", "Queen of the Black Water"), ("gutmaw", "Gutmaw", "The Bottomless Bog")], ("swamp_hag", "Swamp Hag")),
 ("ember_forge", "Ember Forge", "forge", ["burn", "scorch"], "fire", [
   ("slag_imp", "Slag Imp", "A dripping imp that flings molten slag.", "shooter"),
   ("anvil_golem", "Anvil Golem", "Slams the ground hard enough to ring.", "slammer"),
   ("bellows_bat", "Bellows Bat", "A leather bat that gusts sparks.", "flier"),
   ("cinder_hound", "Cinder Hound", "A hound of coals, all teeth and heat.", "runner"),
   ("forge_turret", "Forge Turret", "A furnace mouth that coughs fire in rings.", "turret"),
   ("magma_wader", "Magma Wader", "Leaves burning slag where it walks.", "hazarder", "fire"),
   ("powder_monk", "Powder Monk", "Runs at you lit and laughing.", "bomber"),
   ("smelter", "Smelter", "Splits into molten droplets.", "splitter", "slag_imp")],
   [("the_ironmonger", "The Ironmonger", "Hammer of the Deep Forge"), ("crucible", "Crucible", "The Living Furnace")], ("bellows_giant", "Bellows Giant")),
 ("frost_catacomb", "Frost Catacomb", "frost", ["chill", "freeze"], "spikes", [
   ("rime_walker", "Rime Walker", "A frozen corpse that walks the halls.", "chaser"),
   ("icicle_turret", "Icicle Turret", "A crystal that sheds icicles in a cross.", "crosser"),
   ("snow_wisp", "Snow Wisp", "Drifting cold light that spits sleet.", "drifter"),
   ("glacier_brute", "Glacier Brute", "A wall of ice that shoulders forward.", "brute"),
   ("frost_hopper", "Frost Hopper", "Hops on frozen legs and shatters.", "hopper"),
   ("hoarfrost_witch", "Hoarfrost Witch", "Calls rime walkers from the drifts.", "summoner", "rime_walker"),
   ("shard_skater", "Shard Skater", "Skates sideways, firing bursts of shards.", "strafer"),
   ("blizzard_eye", "Blizzard Eye", "A spinning eye of wind and snow.", "spiraler")],
   [("the_winter_widow", "The Winter Widow", "She Who Waits in Snow"), ("permafrost", "Permafrost", "The Frozen Choir")], ("yeti_bones", "Yeti Bones")),
 ("gloam_gardens", "Gloam Gardens", "garden", ["poison"], "acid", [
   ("thorn_crawler", "Thorn Crawler", "A creeping knot of thorns.", "chaser"),
   ("pod_spitter", "Pod Spitter", "A pod plant that spits seeds in a fan.", "spreader"),
   ("moth_wraith", "Moth Wraith", "A moth the size of a dog, swooping.", "flier"),
   ("root_burrower", "Root Burrower", "Moves under the soil and bursts out.", "burrower"),
   ("bloom_turret", "Bloom Turret", "A flower that blooms into a ring of petals.", "turret"),
   ("seed_mother", "Seed Mother", "Sows thorn crawlers.", "summoner", "thorn_crawler"),
   ("mushroom_bloat", "Mushroom Bloat", "Pops into spores and smaller caps.", "splitter", "thorn_crawler"),
   ("vine_lasher", "Vine Lasher", "Stands still and lashes a beam of vines.", "beamer")],
   [("the_gardener", "The Gardener", "Pruner of the Living"), ("rotbloom", "Rotbloom", "The Corpse Flower")], ("hedge_beast", "Hedge Beast")),
 ("storm_belfry", "Storm Belfry", "belfry", ["shock"], "spikes", [
   ("bell_bat", "Bell Bat", "A bat that rings when it dives.", "flier"),
   ("spark_sprite", "Spark Sprite", "Blinks about and fires crackling shots.", "blinker"),
   ("rope_hanger", "Rope Hanger", "Swings sideways, firing bursts.", "strafer"),
   ("chime_turret", "Chime Turret", "A bell that tolls rings of sound.", "turret"),
   ("rafter_runner", "Rafter Runner", "A gaunt thing that sprints at you.", "runner"),
   ("lightning_eye", "Lightning Eye", "A glass eye that fires a bolt beam.", "beamer"),
   ("thunder_monk", "Thunder Monk", "Slams the floor with a sparking staff.", "slammer"),
   ("storm_crow", "Storm Crow", "Patrols the beams, dropping walls of shot.", "waller")],
   [("the_bellringer", "The Bellringer", "Toll of the Last Hour"), ("tempest", "Tempest", "The Caged Storm")], ("great_clapper", "Great Clapper")),
 ("drowned_chapel", "Drowned Chapel", "chapel", [], "acid", [
   ("drowned_pilgrim", "Drowned Pilgrim", "A soaked pilgrim still walking.", "chaser"),
   ("eel_swimmer", "Eel Swimmer", "Darts under water and surfaces.", "burrower"),
   ("chapel_gargoyle", "Chapel Gargoyle", "Stone that spits water in a cross.", "crosser"),
   ("tide_priest", "Tide Priest", "Calls drowned pilgrims to mass.", "summoner", "drowned_pilgrim"),
   ("bubble_fish", "Bubble Fish", "Floats about, spitting bubbles.", "drifter"),
   ("barnacle_brute", "Barnacle Brute", "Crusted and slow, but it charges.", "brute"),
   ("hymn_swirl", "Hymn Swirl", "A whirlpool of drowned hymns.", "spiraler"),
   ("bloated_deacon", "Bloated Deacon", "Bursts into pilgrims when popped.", "splitter", "drowned_pilgrim")],
   [("the_abbess_below", "The Abbess Below", "Keeper of the Flooded Nave"), ("leviathan_font", "Leviathan Font", "The Font That Drinks")], ("choir_eel", "Choir Eel")),
 ("bone_orchard", "Bone Orchard", "orchard", [], "spikes", [
   ("orchard_scarecrow", "Orchard Scarecrow", "A scarecrow that throws spreading seeds.", "spreader"),
   ("harvest_runner", "Harvest Runner", "A thin thing that sprints between the trees.", "runner"),
   ("rot_apple", "Rot Apple", "A fallen fruit that bursts when it lands.", "hopper"),
   ("bone_tree", "Bone Tree", "A tree of bones shedding a ring of thorns.", "turret"),
   ("crow_reaper", "Crow Reaper", "Swoops with a sickle beak.", "flier"),
   ("root_digger", "Root Digger", "Digs under the orchard and bursts up.", "burrower"),
   ("husk_farmer", "Husk Farmer", "Calls rot apples from the branches.", "summoner", "rot_apple"),
   ("thresher", "Thresher", "Slams the earth with a flail of bone.", "slammer")],
   [("the_harvester", "The Harvester", "Reaper of the Pale Crop"), ("mother_bough", "Mother Bough", "The Hanging Tree")], ("scarecrow_lord", "Scarecrow Lord")),
 ("hive_depths", "Hive Depths", "hive", ["poison"], "acid", [
   ("drone_wasp", "Drone Wasp", "A buzzing wasp that dives in.", "flier"),
   ("larva_crawler", "Larva Crawler", "A soft grub that chases warmth.", "chaser"),
   ("honey_spitter", "Honey Spitter", "Spits sticky honey from afar.", "shooter"),
   ("brood_nurse", "Brood Nurse", "Tends larvae and releases them.", "summoner", "larva_crawler"),
   ("comb_turret", "Comb Turret", "A wax comb spraying stingers in a ring.", "turret"),
   ("sting_strafer", "Sting Strafer", "Darts side to side, loosing stingers.", "strafer"),
   ("royal_jelly", "Royal Jelly", "A wobbling mass that splits apart.", "splitter", "larva_crawler"),
   ("hive_bomber", "Hive Bomber", "A fat bee packed with venom that bursts.", "bomber")],
   [("the_hive_queen", "The Hive Queen", "Mother of the Thousand"), ("swarm_engine", "Swarm Engine", "The Humming Machine")], ("wax_knight", "Wax Knight")),
 ("glass_labyrinth", "Glass Labyrinth", "glass", [], "spikes", [
   ("shard_sentry", "Shard Sentry", "A glass statue firing beams of light.", "beamer"),
   ("prism_wisp", "Prism Wisp", "Blinks between mirrors, firing splinters.", "blinker"),
   ("mirror_runner", "Mirror Runner", "Your reflection running the wrong way.", "runner"),
   ("glass_golem", "Glass Golem", "Heavy and sharp; it charges.", "brute"),
   ("splinter_turret", "Splinter Turret", "Sheds glass in a cross.", "crosser"),
   ("refraction", "Refraction", "A spinning shard that spirals light.", "spiraler"),
   ("pane_walker", "Pane Walker", "Patrols the halls and fires walls of glass.", "waller"),
   ("shatterling", "Shatterling", "Breaks into smaller shards.", "splitter", "mirror_runner")],
   [("the_glazier", "The Glazier", "Maker of the Maze"), ("kaleidos", "Kaleidos", "The Thousand Reflections")], ("mirror_knight", "Mirror Knight")),
 ("umbral_vault", "Umbral Vault", "void", ["dread"], "acid", [
   ("shade_stalker", "Shade Stalker", "A shadow that chases fast and silent.", "runner"),
   ("void_eye", "Void Eye", "Stares, and fires a beam of nothing.", "beamer"),
   ("gloom_wisp", "Gloom Wisp", "Drifts about, dripping darkness.", "drifter"),
   ("hollow_priest", "Hollow Priest", "Calls shade stalkers from the dark.", "summoner", "shade_stalker"),
   ("null_blinker", "Null Blinker", "Steps through shadows to shoot.", "blinker"),
   ("abyss_turret", "Abyss Turret", "A dark hole that exhales a ring of void.", "turret"),
   ("umbral_brute", "Umbral Brute", "A shadow given weight. It charges.", "brute"),
   ("rift_spiral", "Rift Spiral", "A spinning tear in the air.", "spiraler")],
   [("the_umbral_regent", "The Umbral Regent", "Crown of the Dark"), ("maw_of_nothing", "Maw of Nothing", "The Hungry Absence")], ("vault_warden", "Vault Warden")),
 ("cinder_throne", "Cinder Throne", "throne", ["burn", "scorch"], "fire", [
   ("ash_knight", "Ash Knight", "A burnt knight that still guards the throne.", "brute"),
   ("ember_herald", "Ember Herald", "Sprays fans of fire.", "spreader"),
   ("pyre_bat", "Pyre Bat", "A burning bat that swoops.", "flier"),
   ("brand_turret", "Brand Turret", "A brazier that throws fire in a cross.", "crosser"),
   ("throne_zealot", "Throne Zealot", "Runs at you and explodes in devotion.", "bomber"),
   ("cinder_monk", "Cinder Monk", "Burns the floor wherever it walks.", "hazarder", "fire"),
   ("flame_dancer", "Flame Dancer", "Spins a spiral of embers.", "spiraler"),
   ("phoenix_chick", "Phoenix Chick", "A small burning bird that bursts into embers.", "hopper")],
   [("the_ash_king", "The Ash King", "Who Burns on the Throne"), ("pyroclasm", "Pyroclasm", "The Erupting Court")], ("throne_guard", "Throne Guard")),
 ("hollow_heart", "Hollow Heart", "heart", [], "acid", [
   ("vein_crawler", "Vein Crawler", "A pulsing worm that chases.", "chaser"),
   ("clot_hopper", "Clot Hopper", "Hops and bursts into clots.", "hopper"),
   ("artery_turret", "Artery Turret", "A throbbing vessel spurting rings.", "turret"),
   ("flesh_brute", "Flesh Brute", "A lump of muscle that charges.", "brute"),
   ("bile_spitter", "Bile Spitter", "Spits from far off.", "shooter"),
   ("heart_burrower", "Heart Burrower", "Tunnels through flesh and surfaces.", "burrower"),
   ("pulse_mother", "Pulse Mother", "Births vein crawlers.", "summoner", "vein_crawler"),
   ("throb_slammer", "Throb Slammer", "Slams with every heartbeat.", "slammer")],
   [("the_beating_dark", "The Beating Dark", "Heart of the Hollow"), ("twin_ventricle", "Twin Ventricle", "The Two That Pump")], ("organ_warden", "Organ Warden")),
 ("the_last_hollow", "The Last Hollow", "last", ["dread"], "fire", [
   ("final_husk", "Final Husk", "The last of the walking shells.", "chaser"),
   ("echo_runner", "Echo Runner", "A memory of every runner before.", "runner"),
   ("end_eye", "End Eye", "An eye at the end of all things, beaming.", "beamer"),
   ("last_choir", "Last Choir", "Sings rings of sorrow.", "turret"),
   ("hollow_herald", "Hollow Herald", "Blinks and sprays shards.", "blinker"),
   ("terminus_brute", "Terminus Brute", "Charges like the end of the world.", "brute"),
   ("fading_spiral", "Fading Spiral", "A spiral of the last light.", "spiraler"),
   ("ender_wall", "Ender Wall", "Patrols the last halls, firing walls of shot.", "waller")],
   [("the_hollow_one", "The Hollow One", "First and Last"), ("the_unmade", "The Unmade", "What Waits Beyond")], ("last_warden", "Last Warden")),
]
assert len(FLOORS) == 14

ARCH = dict(chaser=A_chaser, runner=A_runner, brute=A_brute, shooter=A_shooter, spreader=A_spreader, turret=A_turret, crosser=A_crosser,
            flier=A_flier, drifter=A_drifter, hopper=A_hopper, burrower=A_burrower, strafer=A_strafer, bomber=A_bomber, splitter=A_splitter,
            summoner=A_summoner, slammer=A_slammer, hazarder=A_hazarder, blinker=A_blinker, beamer=A_beamer, spiraler=A_spiraler, waller=A_waller)
COST = dict(chaser=1, runner=1, flier=1, hopper=1, drifter=1, shooter=2, spreader=2, strafer=2, bomber=2, burrower=2, hazarder=2,
            turret=2, crosser=2, brute=3, slammer=3, splitter=3, summoner=3, blinker=2, beamer=3, spiraler=3, waller=3)

ENEMIES, BOSSES, FLOORDEFS = [], [], []

# one more crypt enemy so the crypt has eight
ENEMIES.append(dict(id="crypt_lantern", name="Crypt Lantern", description="A hanging lantern that sheds a ring of embers.",
                    spriteKey="enemy_crypt_lantern", contactDamage=1, statusImmunities=["burn"], **{**A_turret(1.0), "tags": ["ranged", "stationary", "fire"]}))

def scale_attacks(e, depth):
    # deeper floors: slightly faster projectiles, shorter cooldowns (never below 70%)
    f = 1 + 0.025 * (depth - 1)
    for a in e.get("attacks", []):
        if "projectileSpeed" in a: a["projectileSpeed"] = round(a["projectileSpeed"] * f, 2)
        a["cooldown"] = round(max(a["cooldown"] * 0.7, a["cooldown"] / f), 2)
        if depth >= 8 and a["kind"] == "Projectile" and a.get("count", 1) >= 3: a["count"] += 1

for depth_i, (fid, fname, theme, immun, hazard, enemies, bosses, mini) in enumerate(FLOORS):
    depth = depth_i + 2
    s = 1 + 0.06 * (depth - 1)
    pool = []
    for entry in enemies:
        eid, name, desc, arch = entry[:4]
        extra = entry[4:]
        body = ARCH[arch](s, *extra)
        body["health"] = round(body["health"], 1)
        e = dict(id=eid, name=name, description=desc, spriteKey="enemy_" + eid, contactDamage=1 if depth < 9 else 2 if arch in ("brute", "slammer") else 1)
        e.update(body)
        e["tags"] = list(body.get("tags", [])) + [theme]
        if immun: e["statusImmunities"] = list(immun)
        scale_attacks(e, depth)
        ENEMIES.append(e)
        pool.append({"enemyId": eid, "cost": COST[arch], "weight": 1.5 if COST[arch] == 1 else 1.0 if COST[arch] == 2 else 0.7,
                     "tags": e["tags"][:1], **({"maxPerRoom": 2} if COST[arch] >= 3 else {})})
    minion = enemies[0][0]
    ranged = next(x[0] for x in enemies if x[3] in ("shooter", "spreader", "drifter", "strafer", "blinker"))
    tier = "Early" if depth <= 5 else "Mid" if depth <= 10 else "Late" if depth <= 14 else "Final"
    bh = 200 + 20 * depth
    for bi, (bid, bname, title) in enumerate(bosses):
        speed = 1.3 + 0.03 * depth
        p1 = [proj("fan", "Spread", 5, 5.2, 2.4, 0.6, spread=15, weight=2), atk("charge", "Dash", cooldown=4.5, telegraph=0.8, duration=0.6, recovery=0.8, minRange=2, dashSpeed=10)]
        p2 = [proj("ring", "Ring", 10 + depth // 3, 4.2, 3.2, 0.7, requiresLineOfSight=False, weight=2),
              atk("call", "Summon", cooldown=7, telegraph=0.9, recovery=0.6, requiresLineOfSight=False, summonId=minion, summonCount=2, maxAlive=3)]
        p3 = [proj("spiral", "Spiral", 3, 4.4, 4.0, 0.7, duration=2.0, waves=10, spiralStep=17, requiresLineOfSight=False, weight=2),
              proj("burst", "RandomBurst", 6, 5.4, 2.2, 0.5, spread=35),
              atk("slam", "Slam", cooldown=5, telegraph=0.9, recovery=0.7, maxRange=3, radius=2.6, damage=2, minDifficulty=2)]
        if bi == 1:  # second boss of the floor: ranged / flying flavour
            p1 = [proj("cross", "Cross", 1, 5.0, 2.4, 0.55, duration=0.9, waves=3, requiresLineOfSight=False), proj("xcross", "X", 1, 5.0, 2.4, 0.55, duration=0.9, waves=3, requiresLineOfSight=False)]
            p2 = [atk("beam", "Beam", cooldown=4.5, telegraph=1.0, duration=1.4, recovery=0.6, maxRange=14, damage=1, weight=2), atk("blink", "Teleport", cooldown=5, telegraph=0.5, recovery=0.2, requiresLineOfSight=False),
                  proj("wall", "Wall", 7, 4.0, 3.5, 0.7, spread=10)]
            p3 = [proj("double_spiral", "Spiral", 4, 4.2, 4.5, 0.8, duration=2.4, waves=12, spiralStep=13, requiresLineOfSight=False, weight=2),
                  atk("hazard", "Hazard", cooldown=4, telegraph=0.6, recovery=0.4, requiresLineOfSight=False, radius=1.6, hazardId=hazard, duration=5, damage=1),
                  atk("call", "Summon", cooldown=8, telegraph=0.9, recovery=0.6, requiresLineOfSight=False, summonId=ranged, summonCount=1, maxAlive=2)]
        phases = [{"healthAtOrBelow": 1.0, "invulnerableOnEnter": 0, "attacks": p1},
                  {"healthAtOrBelow": 0.6, "invulnerableOnEnter": 1.0, "transitionKey": bid + "_phase2",
                   "movement": mv("Orbit" if bi else "KeepDistance", speed + 0.4, preferredDistance=4.5, **({"flying": True} if bi else {})), "attacks": p2}]
        if depth >= 4 or tier == "Final":
            phases.append({"healthAtOrBelow": 0.3, "invulnerableOnEnter": 1.2, "transitionKey": bid + "_phase3",
                           "movement": mv("Chase", speed + 0.5, **({"flying": True} if bi else {})), "attacks": p3})
        for ph in phases:
            for a in ph["attacks"]: a["id"] = bid + "_" + a["id"]
        body = {"id": bid + "_body", "spriteKey": "boss_" + bid, "health": bh + 15 * bi, "healthPerFloor": 0.1, "contactDamage": 1, "radius": 1.0 + 0.1 * bi,
                "knockbackResistance": 0.95, "isBoss": True, "tags": ["boss", theme] + (["flying"] if bi else []),
                "movement": mv("Wander" if bi else "Chase", speed, **({"flying": True} if bi else {}))}
        if immun: body["statusImmunities"] = list(immun)
        b = {"id": bid, "name": bname, "title": title, "tier": tier, "description": f"Ruler of the {fname}.", "musicKey": "music_boss_" + tier.lower(),
             "introKey": "intro_" + bid, "rewardPool": "boss", "body": body, "phases": phases}
        BOSSES.append(b)
    mid, mname = mini
    mbody = {"id": mid + "_body", "spriteKey": "miniboss_" + mid, "health": 80 + 8 * depth, "healthPerFloor": 0.1, "contactDamage": 1, "radius": 0.8,
             "knockbackResistance": 0.85, "isBoss": True, "tags": ["miniboss", theme], "movement": mv("Hop", 2.0, pauseTime=0.8, hopDistance=2.5),
             "attacks": [proj(mid + "_ring", "Ring", 8, 4.5, 3.0, 0.6, requiresLineOfSight=False), atk(mid + "_charge", "Dash", cooldown=4, telegraph=0.75, duration=0.5, recovery=0.7, minRange=2.5, dashSpeed=11)],
             "onDeath": [{"kind": "SpawnEnemies", "enemyId": minion, "count": 2}]}
    if immun: mbody["statusImmunities"] = list(immun)
    BOSSES.append({"id": mid, "name": mname, "title": "Lair of the " + fname, "tier": "Miniboss", "description": f"A lair-dweller of the {fname}.", "rewardPool": "treasure", "body": mbody, "phases": []})
    FLOORDEFS.append({"id": fid, "name": fname, "theme": theme, "depth": depth, "minRooms": min(9 + depth // 2, 16), "maxRooms": min(12 + depth // 2, 20),
                      "minTemplateDifficulty": min(1 + (depth - 1) // 3, 6), "maxTemplateDifficulty": min(3 + (depth - 1) // 2, 10),
                      "baseEnemyBudget": 5 + depth // 2, "largeRoomChance": 0.15 + 0.01 * depth, "loopChance": 0.08,
                      "enemies": pool, "bosses": [{"id": b[0], "weight": 1} for b in bosses], "minibosses": [{"id": mid, "weight": 1}],
                      "musicKey": "music_" + fid, "ambienceKey": "amb_" + theme, "hazards": sorted({"spikes", hazard})})

ids = [e["id"] for e in ENEMIES] + [b["id"] for b in BOSSES]
assert len(ids) == len(set(ids)), [x for x in ids if ids.count(x) > 1]
json.dump({"enemies": ENEMIES, "bosses": BOSSES}, open("Assets/StreamingAssets/Content/Enemies/expansion_enemies.json", "w"), indent=1)
json.dump({"floors": FLOORDEFS}, open("Assets/StreamingAssets/Content/Floors/expansion_floors.json", "w"), indent=1)
print(len(ENEMIES), "enemies;", sum(b["tier"] != "Miniboss" for b in BOSSES), "bosses;", sum(b["tier"] == "Miniboss" for b in BOSSES), "minibosses;", len(FLOORDEFS), "floors")
