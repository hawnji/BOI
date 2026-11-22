#!/usr/bin/env python3
"""Generates Assets/StreamingAssets/Content/Items/expansion_items.json.

Hand-written item list in a compact form. Run from the repo root:
    python3 Tools/ContentGen/gen_items.py
The starter items (starter_items.json) are not touched. All names are original to Hollowcrawl.
"""
import json, os

ITEMS = []
RARITY = {0: "Common", 1: "Common", 2: "Uncommon", 3: "Rare", 4: "Legendary"}
PRICE = {0: 10, 1: 15, 2: 15, 3: 20, 4: 30}


def item(id, name, desc, kind, q, effects=None, tags=(), pools=None, active=None, rarity=None, stack=None):
    if pools is None:
        pools = {"Passive": ["treasure"], "Active": ["treasure", "shop"], "Trinket": ["trinket"]}[kind]
    d = {"id": id, "name": name, "description": desc, "kind": kind, "rarity": rarity or RARITY[q], "quality": q,
         "pools": list(pools), "tags": list(tags)}
    if kind == "Trinket" and not rarity:
        d["rarity"] = "Common" if q <= 1 else "Uncommon"
    if effects: d["effects"] = effects
    if active: d["active"] = active
    if stack: d["stacking"], d["maxStacks"] = "StackCapped", stack
    d["shopPrice"] = PRICE[q]
    ITEMS.append(d)


def stat(s, v, op="Flat"): return {"effect": "stat", "stat": s, "op": op, "value": v}
def elem(t, st, ch, dur=3, pot=None):
    e = {"effect": "proj_element", "type": t}
    if st: e.update(status=st, chance=ch, duration=dur)
    if pot: e["potency"] = pot
    return e
def act(charge, n, *on, starts=True):
    a = {"chargeType": charge, "maxCharge": n, "onUse": list(on)}
    if not starts: a["startsCharged"] = False
    return a
def P(id, name, desc, q, eff, tags=(), pools=None, **kw): item(id, name, desc, "Passive", q, eff, tags, pools, **kw)
def A(id, name, desc, q, active, tags=(), pools=None, eff=None): item(id, name, desc, "Active", q, eff, tags, pools, active=active)
def T(id, name, desc, q, eff, tags=()): item(id, name, desc, "Trinket", q, eff, tags)

# ---------------------------------------------------------------- passive: plain stat items (40)
P("ashen_bread", "Ashen Bread", "Dense, grey, filling. Vigor up.", 1, [{"effect": "grant_health", "containers": 1, "heal": 2}], ["health", "food"], ["treasure", "boss"])
P("salt_pork", "Salt Pork", "Tough meat for a long walk. Vigor up.", 1, [{"effect": "grant_health", "containers": 1, "heal": 2}], ["health", "food"], ["boss", "shop"])
P("marrow_stew", "Marrow Stew", "Two vigor up, a little slower.", 2, [{"effect": "grant_health", "containers": 2, "heal": 4}, stat("MoveSpeed", -0.1)], ["health", "food"], ["boss"])
P("black_honey", "Black Honey", "Sweet and strange. Vigor up, luck up.", 2, [{"effect": "grant_health", "containers": 1, "heal": 2}, stat("Luck", 1)], ["health", "food", "insect"])
P("cracked_whetstone", "Cracked Whetstone", "Damage up.", 1, [stat("Damage", 0.5)], ["offense"], ["treasure", "boss"])
P("iron_knuckles", "Iron Knuckles", "Damage up, knockback up.", 1, [stat("Damage", 0.6), stat("Knockback", 0.5)], ["offense"])
P("grudge_stone", "Grudge Stone", "Heavy with old anger. Big damage up, tears slower.", 2, [stat("Damage", 1.2), stat("FireRate", -0.3)], ["offense"], ["treasure", "boss"])
P("ember_heart", "Ember Heart", "Damage up and every shot runs hotter.", 3, [stat("Damage", 0.8), stat("Damage", 0.15, "PercentAdd")], ["offense", "fire"])
P("widow_pin", "Widow's Pin", "A long black pin. Critical chance up.", 2, [stat("CritChance", 0.08)], ["offense", "precision"])
P("hangmans_coin", "Hangman's Coin", "Critical hits cut deeper.", 2, [stat("CritDamage", 0.6)], ["offense", "precision"])
P("quill_of_haste", "Quill of Haste", "Rate of fire up.", 1, [stat("FireRate", 0.35)], ["offense"], ["treasure", "boss"])
P("rattle_drum", "Rattle Drum", "A tiny drum keeps the rhythm. Rate of fire up.", 2, [stat("FireRate", 0.5)], ["offense"])
P("frenzy_mask", "Frenzy Mask", "Shoot much faster, hit a little softer.", 2, [stat("FireRate", 0.25, "PercentAdd"), stat("Damage", -0.1, "PercentAdd")], ["offense", "risk"])
P("long_sight", "Long Sight", "Range up, shot speed up.", 1, [stat("Range", 2.0), stat("ShotSpeed", 0.15)], ["precision"])
P("tin_spyglass", "Tin Spyglass", "Range up.", 0, [stat("Range", 2.5)], ["precision"], ["treasure", "shop"])
P("slingers_band", "Slinger's Band", "Shot speed up, damage up.", 1, [stat("ShotSpeed", 0.25), stat("Damage", 0.3)], ["precision"])
P("hollow_boots", "Hollow Boots", "Light as dust. Speed up.", 1, [stat("MoveSpeed", 0.2)], ["movement"], ["treasure", "shop"])
P("runners_wrap", "Runner's Wrap", "Speed up, range up.", 1, [stat("MoveSpeed", 0.15), stat("Range", 1.0)], ["movement"])
P("weighted_cloak", "Weighted Cloak", "Slower, but you take less damage.", 2, [stat("MoveSpeed", -0.15), stat("Defense", 1)], ["defense"])
P("rabbit_bone", "Rabbit Bone", "Luck up.", 0, [stat("Luck", 1)], ["luck"], ["treasure", "shop"])
P("four_leaf_ash", "Four-Leaf Ash", "A clover burned to ash and still lucky. Luck up twice.", 2, [stat("Luck", 2)], ["luck", "plant"])
P("bloated_tick", "Bloated Tick", "Shots grow fat. Shot size and damage up.", 1, [stat("ShotSize", 0.25), stat("Damage", 0.3)], ["insect", "offense"])
P("pebble_pouch", "Pebble Pouch", "Smaller, faster, more shots.", 1, [stat("ShotSize", -0.2), stat("FireRate", 0.4)], ["offense"])
P("anvil_charm", "Anvil Charm", "Heavier shots. Knockback way up.", 0, [stat("Knockback", 1.5), stat("ShotSize", 0.1)], ["offense"])
P("pilgrim_staff", "Pilgrim's Staff", "All stats up a little.", 3, [stat("Damage", 0.3), stat("FireRate", 0.2), stat("MoveSpeed", 0.1), stat("Range", 1.0), stat("Luck", 1)], ["holy"], ["treasure", "boss"])
P("ossuary_key", "Ossuary Key", "Damage and range up. Smells of crypt.", 2, [stat("Damage", 0.5), stat("Range", 1.5)], ["bone"], ["boss"])
P("crow_feather", "Crow Feather", "Speed and shot speed up.", 1, [stat("MoveSpeed", 0.12), stat("ShotSpeed", 0.2)], ["bird", "movement"])
P("owl_eye", "Owl Eye", "Range and critical chance up.", 2, [stat("Range", 1.5), stat("CritChance", 0.05)], ["bird", "precision"])
P("raven_quill", "Raven Quill", "Damage up, shots fly straighter and faster.", 2, [stat("Damage", 0.4), stat("ShotSpeed", 0.3)], ["bird", "offense"])
P("candle_stub", "Candle Stub", "A little light, a little courage. Damage and luck up.", 1, [stat("Damage", 0.3), stat("Luck", 1)], ["light"])
P("burnt_hymnal", "Burnt Hymnal", "Holy pages, half-burned. Damage up, holy shots.", 2, [stat("Damage", 0.4), elem("Holy", None, 0)], ["holy", "fire"])
P("broken_halo", "Broken Halo", "Damage way up while you are unhurt.", 3, [dict(stat("Damage", 1.5), condition="health_full")], ["holy", "risk"])
P("last_breath", "Last Breath", "Damage way up when you are almost dead.", 2, [dict(stat("Damage", 2.0), condition="total_health_at_most:2")], ["risk"])
P("cornered_rat", "Cornered Rat", "Speed and fire rate up at low vigor.", 2, [dict(stat("FireRate", 0.6), condition="health_at_most:2"), dict(stat("MoveSpeed", 0.2), condition="health_at_most:2")], ["risk", "movement"])
P("misers_ledger", "Miser's Ledger", "Damage grows with your coins.", 2, [{"effect": "stat_scaling", "stat": "Damage", "perUnit": 0.04, "source": "coins", "max": 2.0}], ["economy"])
P("powder_heart", "Powder Heart", "Damage grows with your bombs.", 2, [{"effect": "stat_scaling", "stat": "Damage", "perUnit": 0.1, "source": "bombs", "max": 2.0}], ["bomb"])
P("wounded_pride", "Wounded Pride", "The more vigor you're missing, the harder you hit.", 2, [{"effect": "stat_scaling", "stat": "Damage", "perUnit": 0.25, "source": "missing_health", "max": 2.5}], ["risk"])
P("collectors_eye", "Collector's Eye", "Every item you own sharpens your aim.", 3, [{"effect": "stat_scaling", "stat": "CritChance", "perUnit": 0.01, "source": "items", "max": 0.25}], ["precision"])
P("hive_mind", "Hive Mind", "Faster fire for every companion at your side.", 2, [{"effect": "stat_scaling", "stat": "FireRate", "perUnit": 0.12, "source": "companions", "max": 1.2}], ["companion", "insect"])
P("deep_pockets", "Deep Pockets", "Luck grows with your keys.", 1, [{"effect": "stat_scaling", "stat": "Luck", "perUnit": 0.3, "source": "keys", "max": 3}], ["economy", "luck"])

# ---------------------------------------------------------------- passive: projectile mods (40)
P("bramble_tip", "Bramble Tip", "Shots pierce two foes.", 2, [{"effect": "proj_pierce", "count": 2}], ["piercing", "plant"])
P("ghost_lance", "Ghost Lance", "Shots pierce everything.", 3, [{"effect": "proj_pierce", "count": -1}, stat("Damage", -0.1, "PercentAdd")], ["piercing"])
P("lodestone_eye", "Lodestone Eye", "Strongly homing shots.", 3, [{"effect": "proj_homing", "strength": 1.0}], ["homing"])
P("hound_nose", "Hound's Nose", "Shots sniff out prey a little.", 1, [{"effect": "proj_homing", "strength": 0.35}, stat("Range", 0.5)], ["homing"])
P("rubber_marble", "Rubber Marble", "Shots bounce off walls twice.", 1, [{"effect": "proj_bounce", "count": 2}], ["bouncing"])
P("ricochet_ring", "Ricochet Ring", "Shots bounce four times.", 2, [{"effect": "proj_bounce", "count": 4}, stat("Range", 1.0)], ["bouncing"])
P("seed_pod", "Seed Pod", "Shots burst into three on impact.", 2, [{"effect": "proj_split", "count": 3, "damageScale": 0.4}], ["splitting", "plant"])
P("shatter_glass", "Shatter Glass", "Shots burst into five shards.", 3, [{"effect": "proj_split", "count": 5, "damageScale": 0.35}], ["splitting"])
P("powder_shot", "Powder Shot", "Shots explode on impact.", 3, [{"effect": "proj_explode", "radius": 1.0, "damageScale": 0.6}, stat("FireRate", -0.2)], ["explosive"])
P("spark_jar", "Spark Jar", "Shots arc to one nearby foe.", 2, [{"effect": "proj_chain", "jumps": 1, "range": 3.0, "damageScale": 0.6}], ["shock"])
P("storm_heart", "Storm Heart", "Lightning leaps between three foes.", 3, [{"effect": "proj_chain", "jumps": 3, "range": 3.5, "damageScale": 0.55}, elem("Shock", "shock", 0.15)], ["shock"])
P("rime_shard", "Rime Shard", "Shots may chill.", 1, [elem("Frost", "chill", 0.3)], ["frost"])
P("glacier_core", "Glacier Core", "Shots often chill, sometimes freeze solid.", 3, [elem("Frost", "chill", 0.5), elem("Frost", "freeze", 0.1, 1.5)], ["frost"])
P("nettle_sap", "Nettle Sap", "Shots poison often.", 2, [elem("Poison", "poison", 0.45, 4)], ["poison", "plant"])
P("rot_tooth", "Rot Tooth", "Shots spread blight.", 2, [elem("Blight", "blight", 0.3, 4)], ["blight"])
P("night_ink", "Night Ink", "Shots may fill foes with dread.", 2, [elem("Void", "dread", 0.2, 2)], ["void"])
P("void_pearl", "Void Pearl", "Shots ignore armour.", 3, [elem("Void", None, 0), stat("Damage", 0.3)], ["void"])
P("kindling_bundle", "Kindling Bundle", "Shots may burn, and rate of fire up.", 1, [elem("Fire", "burn", 0.2), stat("FireRate", 0.15)], ["fire"])
P("magnifying_lens", "Magnifying Lens", "Huge shots.", 2, [{"effect": "proj_size", "mult": 1.6}, stat("Damage", 0.4)], ["offense"])
P("needle_cluster", "Needle Cluster", "Fire a spread of three.", 3, [{"effect": "proj_volley", "shots": 3, "spread": 14, "damageScale": 0.7}], ["multishot"])
P("twin_barrel", "Twin Barrel", "Fire two shots side by side.", 2, [{"effect": "proj_volley", "shots": 2, "spread": 6, "damageScale": 0.8}], ["multishot"])
P("fan_of_knives", "Fan of Knives", "A wide fan of five weak shots.", 3, [{"effect": "proj_volley", "shots": 5, "spread": 40, "damageScale": 0.5}], ["multishot"])
P("hurling_arm", "Hurling Arm", "Shots arc over obstacles.", 2, [{"effect": "proj_delivery", "delivery": "Lobbed", "priority": 5}, stat("Damage", 0.5)], ["delivery"])
P("crescent_blade", "Crescent Blade", "Shots return to you.", 2, [{"effect": "proj_delivery", "delivery": "Boomerang", "priority": 6}, {"effect": "proj_pierce", "count": -1}], ["delivery", "piercing"])
P("dawn_prism", "Dawn Prism", "Fire a beam of light.", 4, [{"effect": "proj_delivery", "delivery": "Beam", "priority": 10}], ["delivery", "light", "holy"], ["treasure", "boss"])
P("orbit_stone", "Orbit Stone", "Shots circle you before flying off.", 2, [{"effect": "proj_delivery", "delivery": "Orbiting", "priority": 4}, stat("Range", 2.0)], ["delivery"])
P("tide_shell", "Tide Shell", "Shots roll out as a wave.", 3, [{"effect": "proj_delivery", "delivery": "Wave", "priority": 7}, {"effect": "proj_pierce", "count": -1}], ["delivery", "piercing"])
P("wraith_veil", "Wraith Veil", "Shots pass through rocks and walls.", 2, [{"effect": "proj_spectral"}], ["spectral"])
P("drawn_bow", "Drawn Bow", "Hold to charge a mighty shot.", 3, [{"effect": "proj_charge", "time": 1.0, "damageMult": 3.5}], ["charge"])
P("coiled_spring", "Coiled Spring", "Short charge, solid hit.", 2, [{"effect": "proj_charge", "time": 0.5, "damageMult": 2.0}, stat("ShotSpeed", 0.2)], ["charge"])
P("comet_tail", "Comet Tail", "Shots speed up as they fly.", 1, [{"effect": "proj_accelerate", "perSecond": 6}, stat("Range", 1.5)], ["precision"])
P("serpent_coil", "Serpent Coil", "Shots wriggle through the air.", 1, [{"effect": "proj_wavy", "amplitude": 0.4}, stat("Damage", 0.3)], ["wavy"])
P("executioner_mark", "Executioner's Mark", "Shot damage x1.5, rate of fire down.", 3, [{"effect": "proj_damage_mult", "mult": 1.5}, stat("FireRate", -0.3)], ["offense"])
P("blood_quill", "Blood Quill", "Shot damage up by a quarter.", 3, [{"effect": "proj_damage_mult", "mult": 1.25}], ["offense", "health"])
P("pestle", "Pestle", "Poison turns to blight.", 2, [{"effect": "proj_convert_status", "fromStatus": "poison", "toStatus": "blight", "potencyMult": 1.2}, elem("Poison", "poison", 0.2)], ["poison", "blight"])
P("hoarfrost_vial", "Hoarfrost Vial", "Chills deepen into freezes.", 3, [{"effect": "proj_convert_status", "fromStatus": "chill", "toStatus": "freeze", "durationMult": 0.6}, elem("Frost", "chill", 0.25)], ["frost"])
P("lightning_rod", "Lightning Rod", "Shots shock and fly fast.", 2, [elem("Shock", "shock", 0.25), stat("ShotSpeed", 0.3)], ["shock"])
P("scattershot", "Scattershot", "Your shots wobble and split into two.", 2, [{"effect": "proj_split", "count": 2, "damageScale": 0.5}, {"effect": "proj_wavy", "amplitude": 0.2}], ["splitting", "wavy"])
P("hunters_hood", "Hunter's Hood", "Homing shots and more critical hits.", 3, [{"effect": "proj_homing", "strength": 0.5}, stat("CritChance", 0.06)], ["homing", "precision"])
P("cinder_crown", "Cinder Crown", "Burning foes take heavier hits.", 3, [elem("Fire", "burn", 0.3), {"effect": "outgoing_mult", "mult": 1.25, "types": "Fire"}], ["fire"])

# ---------------------------------------------------------------- passive: on-hit / on-kill (24)
P("blasting_cap", "Blasting Cap", "Hits sometimes explode.", 2, [{"effect": "onhit_explode", "chance": 0.12, "radius": 1.2, "damageScale": 1.0}], ["explosive"])
P("thunder_seed", "Thunder Seed", "Hits sometimes burst into sparks.", 2, [{"effect": "onhit_status_aoe", "chance": 0.15, "radius": 1.5, "status": "shock", "duration": 2}], ["shock", "plant"])
P("spore_sac", "Spore Sac", "Hits sometimes puff poison spores.", 2, [{"effect": "onhit_status_aoe", "chance": 0.2, "radius": 1.4, "status": "poison", "duration": 4}], ["poison", "plant"])
P("frost_bloom", "Frost Bloom", "Hits sometimes chill everything nearby.", 2, [{"effect": "onhit_status_aoe", "chance": 0.18, "radius": 1.6, "status": "chill", "duration": 3}], ["frost", "plant"])
P("splinter_bark", "Splinter Bark", "Hits sometimes throw out splinters.", 2, [{"effect": "onhit_burst", "chance": 0.15, "count": 4, "damageScale": 0.4}], ["splitting", "plant"])
P("bone_rattle", "Bone Rattle", "Hits often scatter bone chips.", 3, [{"effect": "onhit_burst", "chance": 0.25, "count": 6, "damageScale": 0.35}], ["bone", "splitting"])
P("greedy_tooth", "Greedy Tooth", "Foes sometimes drop coins.", 1, [{"effect": "onkill_drop", "chance": 0.08, "pickup": "Coin"}], ["economy"])
P("keymakers_finger", "Keymaker's Finger", "Foes sometimes drop keys.", 2, [{"effect": "onkill_drop", "chance": 0.05, "pickup": "Key"}], ["economy"])
P("fuse_beetle_shell", "Beetle Shell", "Foes sometimes drop bombs.", 2, [{"effect": "onkill_drop", "chance": 0.05, "pickup": "Bomb"}], ["bomb", "insect"])
P("vampire_moth", "Vampire Moth", "Kills sometimes heal half a heart.", 3, [{"effect": "onkill_heal", "chance": 0.06, "halves": 1}], ["health", "insect"])
P("leech_jar", "Leech Jar", "Kills now and then heal you.", 2, [{"effect": "onkill_heal", "chance": 0.03, "halves": 1}, stat("Damage", 0.2)], ["health"])
P("powder_corpse", "Powder Corpse", "Foes explode when they die.", 3, [{"effect": "onkill_explode", "chance": 0.35, "radius": 1.5, "damage": 15}], ["explosive"])
P("ember_corpse", "Pyre Ash", "Burning foes spread fire when they die.", 2, [{"effect": "onkill_spread_status", "status": "burn", "radius": 2.0, "duration": 3, "requireType": "Fire"}, elem("Fire", "burn", 0.15)], ["fire"])
P("frozen_tear", "Frozen Tear", "Frozen foes spread chill on death.", 2, [{"effect": "onkill_spread_status", "status": "chill", "radius": 2.2, "duration": 3, "requireType": "Frost"}, elem("Frost", "chill", 0.15)], ["frost"])
P("hex_doll", "Hex Doll", "Bosses take much more damage.", 3, [{"effect": "outgoing_mult", "mult": 1.3, "vsBoss": True}], ["offense", "curse"])
P("assassins_glove", "Assassin's Glove", "Critical hits hit harder still.", 3, [{"effect": "outgoing_mult", "mult": 1.4, "crit": True}, stat("CritChance", 0.04)], ["precision"])
P("sacred_salt", "Sacred Salt", "Holy damage is stronger.", 2, [{"effect": "outgoing_mult", "mult": 1.3, "types": "Holy"}, elem("Holy", None, 0)], ["holy"])
P("venom_fang", "Venom Fang", "Poison does more damage.", 2, [{"effect": "outgoing_mult", "mult": 1.35, "types": "Poison"}, elem("Poison", "poison", 0.2)], ["poison"])
P("static_wire", "Static Wire", "Shock damage is stronger.", 2, [{"effect": "outgoing_mult", "mult": 1.35, "types": "Shock"}, elem("Shock", "shock", 0.15)], ["shock"])
P("frost_rune", "Frost Rune", "Frost damage is stronger.", 2, [{"effect": "outgoing_mult", "mult": 1.35, "types": "Frost"}, elem("Frost", "chill", 0.15)], ["frost"])
P("demolition_glove", "Demolition Glove", "Explosions hit much harder.", 2, [{"effect": "outgoing_mult", "mult": 1.5, "types": "Explosive"}], ["explosive", "bomb"])
P("dread_lantern", "Dread Lantern", "Foes nearby are filled with dread.", 3, [{"effect": "aura_status", "radius": 2.0, "status": "dread", "interval": 2, "duration": 1.5}], ["aura", "void", "light"])
P("frost_aura", "Winter Shroud", "A cold mist chills foes near you.", 2, [{"effect": "aura_status", "radius": 1.8, "status": "chill", "interval": 1, "duration": 2}], ["aura", "frost"])
P("plague_censer", "Plague Censer", "A swinging censer poisons nearby foes.", 2, [{"effect": "aura_status", "radius": 1.7, "status": "poison", "interval": 1.2, "duration": 3}], ["aura", "poison"])

# ---------------------------------------------------------------- passive: defensive (20)
P("bark_skin", "Bark Skin", "Take less damage.", 2, [stat("Defense", 1)], ["defense", "plant"])
P("scale_mail", "Scale Mail", "Take less damage, move slower.", 2, [stat("Defense", 1), stat("MoveSpeed", -0.05)], ["defense"])
P("tortoise_shell", "Tortoise Shell", "A chance to shrug off a hit.", 3, [{"effect": "incoming_block_chance", "chance": 0.15}], ["defense"])
P("lucky_scar", "Lucky Scar", "Small chance to ignore a hit. Luck up.", 2, [{"effect": "incoming_block_chance", "chance": 0.07}, stat("Luck", 1)], ["defense", "luck"])
P("asbestos_cloak", "Ashcloth Cloak", "Fire can't hurt you.", 2, [{"effect": "incoming_immunity", "types": "Fire"}], ["defense", "fire"])
P("blast_visor", "Blast Visor", "Explosions can't hurt you.", 3, [{"effect": "incoming_immunity", "types": "Explosive"}], ["defense", "bomb"])
P("gum_boots", "Gum Boots", "Immune to shock and poison.", 2, [{"effect": "incoming_immunity", "types": "Shock, Poison"}], ["defense"])
P("long_flinch", "Long Flinch", "You stay safe longer after a hit.", 1, [{"effect": "invuln_on_hit", "seconds": 0.6}], ["defense"])
P("spite_thorns", "Spite Thorns", "Getting hit bursts thorns around you.", 2, [{"effect": "ondamaged_burst", "chance": 1.0, "radius": 2.5, "damage": 20, "type": "Physical"}], ["defense", "plant"])
P("frost_heart", "Frost Heart", "Getting hit freezes nearby foes.", 3, [{"effect": "ondamaged_burst", "chance": 1.0, "radius": 2.5, "damage": 8, "type": "Frost", "status": "freeze", "duration": 1.5}], ["defense", "frost"])
P("powder_vest", "Powder Vest", "Getting hit sets off an explosion that spares you.", 2, [{"effect": "ondamaged_burst", "chance": 0.5, "radius": 2.0, "damage": 30, "type": "Explosive"}], ["defense", "explosive"])
P("guardian_seal", "Guardian Seal", "Getting hit sometimes grants a ward.", 2, [{"effect": "ondamaged_ward", "chance": 0.25, "halves": 2}], ["defense", "holy"])
P("battery_heart", "Battery Heart", "Getting hit charges your active item.", 2, [{"effect": "ondamaged_charge", "amount": 1}], ["defense", "charge"])
P("moss_bandage", "Moss Bandage", "Slowly heal while fighting.", 3, [{"effect": "regen", "interval": 40, "halves": 1, "inCombatOnly": True}], ["health", "plant"])
P("heartroot", "Heartroot", "Vigor up and slow healing.", 3, [{"effect": "grant_health", "containers": 1, "heal": 2}, {"effect": "regen", "interval": 60, "halves": 1}], ["health", "plant"])
P("holy_water", "Holy Water", "Two wards.", 1, [{"effect": "grant_health", "ward": 4}], ["health", "holy"], ["treasure", "shop"])
P("shadow_wine", "Shadow Wine", "Two umbral hearts.", 2, [{"effect": "grant_health", "umbral": 4}], ["health", "void"])
P("blessed_rag", "Blessed Rag", "Clearing a room unhurt may grant a ward.", 2, [{"effect": "roomclear_drop", "chance": 0.25, "pickup": "Ward", "requireNoDamage": True}], ["holy", "health"])
P("field_kit", "Field Kit", "Clearing rooms sometimes heals you.", 2, [{"effect": "roomclear_heal", "chance": 0.2, "halves": 1}], ["health"])
P("sentinel_eye", "Sentinel's Eye", "Take less damage while at full vigor.", 2, [dict(stat("Defense", 2), condition="health_full")], ["defense", "holy"])

# ---------------------------------------------------------------- passive: economy and utility (16)
P("tithe_box", "Tithe Box", "Coin pickups sometimes give extra.", 1, [{"effect": "pickup_bonus", "pickup": "Coin", "resource": "Coins", "amount": 1, "chance": 0.3}], ["economy"], ["shop", "treasure"])
P("bomb_bag", "Bomb Bag", "Bomb pickups sometimes give extra.", 1, [{"effect": "pickup_bonus", "pickup": "Bomb", "resource": "Bombs", "amount": 1, "chance": 0.3}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb"], ["shop", "treasure"])
P("key_ring", "Key Ring", "Key pickups sometimes give extra.", 1, [{"effect": "pickup_bonus", "pickup": "Key", "resource": "Keys", "amount": 1, "chance": 0.3}, {"effect": "grant_resource", "resource": "Keys", "amount": 2}], ["economy"], ["shop", "treasure"])
P("merchants_seal", "Merchant's Seal", "Shop prices drop.", 2, [{"effect": "shop_discount", "percent": 0.25}], ["economy"], ["shop"])
P("haggler_tongue", "Haggler's Tongue", "Everything costs 3 coins less.", 2, [{"effect": "shop_discount", "percent": 0, "flat": 3}], ["economy"], ["shop"])
P("fat_purse", "Fat Purse", "Coins drop more often.", 1, [{"effect": "drop_chance", "pickup": "Coin", "mult": 1.5}], ["economy"], ["shop"])
P("healers_herb", "Healer's Herb", "Hearts drop more often.", 1, [{"effect": "drop_chance", "pickup": "VigorHalf", "mult": 1.5}, {"effect": "drop_chance", "pickup": "VigorFull", "mult": 1.5}], ["health", "plant"])
P("dowsing_rod", "Dowsing Rod", "Keys drop more often.", 1, [{"effect": "drop_chance", "pickup": "Key", "mult": 1.6}], ["economy"])
P("allowance", "Allowance", "Gain 5 coins at the start of each floor.", 1, [{"effect": "floorstart_resource", "resource": "Coins", "amount": 5}], ["economy"], ["shop", "treasure"])
P("quartermaster", "Quartermaster", "Gain a bomb and a key every floor.", 2, [{"effect": "floorstart_resource", "resource": "Bombs", "amount": 1}, {"effect": "floorstart_resource", "resource": "Keys", "amount": 1}], ["economy", "bomb"])
P("ember_tithe", "Ember Tithe", "Gain an ember every floor.", 3, [{"effect": "floorstart_resource", "resource": "Embers", "amount": 1}], ["economy", "fire"], ["secret", "treasure"])
P("scavengers_sack", "Scavenger's Sack", "Clearing rooms sometimes drops a pickup.", 2, [{"effect": "roomclear_drop", "chance": 0.15, "pickup": "Coin"}, {"effect": "roomclear_drop", "chance": 0.08, "pickup": "Bomb"}, {"effect": "roomclear_drop", "chance": 0.08, "pickup": "Key"}], ["economy"])
P("perfect_ledger", "Perfect Ledger", "Clear rooms unhurt to earn coins.", 2, [{"effect": "roomclear_drop", "chance": 0.5, "pickup": "Coin", "requireNoDamage": True}], ["economy", "precision"])
P("second_pocket", "Second Pocket", "Carry an extra trinket.", 2, [{"effect": "trinket_slots", "add": 1}], ["economy"], ["treasure", "shop"])
P("hunters_trophy", "Hunter's Trophy", "Clearing a room charges your active item twice.", 2, [{"effect": "roomclear_charge", "amount": 1}], ["charge"])
P("wardstone", "Wardstone", "Enemies near the door are slowed as you enter.", 1, [{"effect": "roomenter_status", "status": "chill", "duration": 2, "radius": 4, "chance": 1.0}], ["frost"])

# ---------------------------------------------------------------- passive: bombs (8)
P("big_powder", "Big Powder", "Bombs are bigger and stronger. +5 bombs.", 2, [{"effect": "bomb_mod", "radiusMult": 1.3, "damageAdd": 20}, {"effect": "grant_resource", "resource": "Bombs", "amount": 5}], ["bomb"])
P("seeking_fuse", "Seeking Fuse", "Bombs creep toward enemies.", 2, [{"effect": "bomb_mod", "homing": True}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb", "homing"])
P("tar_bombs", "Tar Bombs", "Bombs stick to what they touch.", 1, [{"effect": "bomb_mod", "sticky": True, "damageAdd": 10}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb"])
P("cluster_charge", "Cluster Charge", "Bombs scatter little bombs.", 3, [{"effect": "bomb_mod", "cluster": 4}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb", "splitting"])
P("short_fuse", "Short Fuse", "Bombs go off sooner.", 1, [{"effect": "bomb_mod", "fuseMult": 0.6}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb"])
P("napalm_jar", "Napalm Jar", "Bombs burn foes.", 2, [{"effect": "bomb_mod", "type": "Fire", "status": "burn", "statusDuration": 4}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb", "fire"])
P("nail_bomb", "Nail Bomb", "Bombs throw out shots when they explode.", 2, [{"effect": "bomb_explode_shots", "count": 8, "damageScale": 0.8}, {"effect": "grant_resource", "resource": "Bombs", "amount": 3}], ["bomb", "splitting"])
P("tamed_powder", "Tamed Powder", "Your bombs never hurt you.", 3, [{"effect": "bomb_mod", "safe": True}, {"effect": "grant_resource", "resource": "Bombs", "amount": 5}], ["bomb", "defense"])

# ---------------------------------------------------------------- passive: companions (31)
COMP = [
    ("drone_mite", "Mite Hive", "A wax hive that releases a drone mite.", 2, ["insect"]),
    ("cinder_kit", "Coal Den", "A cinder kit follows you home.", 2, ["fire"]),
    ("frost_moth", "Rime Cocoon", "A frost moth hatches.", 2, ["frost", "insect"]),
    ("blight_toad", "Toad Stool", "A blight toad hops alongside.", 2, ["poison"]),
    ("lantern_eye", "Lantern Socket", "A floating eye watches for you.", 2, ["light"]),
    ("static_wren", "Wren's Nest", "A static wren joins you.", 2, ["shock", "bird"]),
    ("echo_shade", "Echo Shade", "Your shadow fights back.", 3, ["void"]),
    ("twin_husk", "Twin Husk", "A hollow double copies your shots.", 3, ["bone"]),
    ("mirror_shard", "Mirror Shard", "A glass sliver throws back your shots.", 2, []),
    ("dread_puppet", "Dread Puppet", "A stitched doll that mimics you.", 2, ["void"]),
    ("ash_halo", "Ash Halo", "A ring of burning ash orbits you.", 3, ["fire", "defense"]),
    ("grave_stone", "Grave Stone", "A heavy stone orbits and blocks shots.", 2, ["defense", "bone"]),
    ("thorn_ring", "Thorn Ring", "Spinning thorns shred nearby foes.", 2, ["plant"]),
    ("ember_moon", "Ember Moon", "A slow wide orbit of flame.", 2, ["fire"]),
    ("cold_shield", "Cold Shield", "A spinning disc of ice blocks shots.", 2, ["defense", "frost"]),
    ("gnaw_rat", "Gnaw Rat", "A hungry rat hunts for you.", 1, []),
    ("bramble_hound", "Bramble Hound", "A thorn-vine dog that bites hard.", 3, ["plant"]),
    ("spite_imp", "Spite Imp", "A tiny imp throws itself at foes.", 1, []),
    ("rot_leech", "Rot Leech", "A leech that drinks from your foes.", 2, ["poison"]),
    ("magpie", "Magpie", "Fetches loose pickups.", 2, ["bird", "economy"]),
    ("sack_goblin", "Sack Goblin", "Hoards dropped things for you.", 1, ["economy"]),
    ("fuse_beetle", "Fuse Beetle", "Lays bombs that spare you.", 2, ["bomb", "insect"]),
    ("powder_crow", "Powder Crow", "Drops a big charge now and then.", 3, ["bomb", "bird"]),
    ("coin_mole", "Coin Mole", "Digs up coins after cleared rooms.", 1, ["economy"]),
    ("key_wisp", "Key Wisp", "Finds keys after cleared rooms.", 2, ["economy"]),
    ("heart_moth", "Heart Moth", "Leaves hearts after cleared rooms.", 2, ["health", "insect"]),
    ("powder_sprite", "Powder Sprite", "Leaves bombs after cleared rooms.", 1, ["bomb"]),
    ("ward_knight", "Ward Knight", "A tiny knight blocks shots for you.", 3, ["defense", "holy"]),
]
for cid, name, desc, q, tags in COMP:
    iid = cid if cid not in ("drone_mite", "cinder_kit", "frost_moth", "blight_toad", "lantern_eye", "static_wren") else name.lower().replace("'", "").replace(" ", "_")
    P(iid, name, desc, q, [{"effect": "companion", "companion": cid}], ["companion"] + tags)
P("pack_leader", "Pack Leader", "Companions hit harder and grow.", 3, [{"effect": "companion_boost", "damageMult": 1.5, "sizeMult": 1.2}], ["companion"])
P("feeding_bowl", "Feeding Bowl", "Companions grow stronger. Vigor up.", 2, [{"effect": "companion_boost", "damageMult": 1.25}, {"effect": "grant_health", "containers": 1}], ["companion", "health"])
P("sparrow_flock", "Sparrow Flock", "Two bone sparrows join you.", 3, [{"effect": "companion", "companion": "bone_sparrow", "count": 2}], ["companion", "bird"])

# ---------------------------------------------------------------- active items (43)
A("rusty_trowel", "Rusty Trowel", "Dig up a few coins.", 1, act("Rooms", 2, {"effect": "active_spawn_pickups", "pickup": "Coin", "count": 3}), ["economy"])
A("powder_keg", "Powder Keg", "Drop a pile of bombs.", 2, act("Rooms", 4, {"effect": "active_spawn_pickups", "pickup": "Bomb", "count": 2}), ["bomb"])
A("skeleton_key", "Skeleton Key", "Conjure a key.", 2, act("Rooms", 4, {"effect": "active_spawn_pickups", "pickup": "Key", "count": 1}), ["economy"])
A("blood_jar", "Blood Jar", "Pour out a heart.", 2, act("Rooms", 4, {"effect": "active_spawn_pickups", "pickup": "VigorFull", "count": 1}), ["health"])
A("war_horn", "War Horn", "Damage way up for this room.", 2, act("Rooms", 3, {"effect": "active_temp_stat", "stat": "Damage", "op": "Flat", "value": 2.0, "duration": 0}), ["offense"])
A("hasty_tonic", "Hasty Tonic", "Speed and rate of fire up for this room.", 1, act("Rooms", 2, {"effect": "active_temp_stat", "stat": "FireRate", "op": "Flat", "value": 1.0, "duration": 0}, {"effect": "active_temp_stat", "stat": "MoveSpeed", "op": "Flat", "value": 0.3, "duration": 0}), ["offense", "movement"])
A("iron_draught", "Iron Draught", "Take less damage for 10 seconds.", 1, act("Rooms", 2, {"effect": "active_temp_stat", "stat": "Defense", "op": "Flat", "value": 2, "duration": 10}), ["defense"])
A("burning_book", "Burning Book", "Set the whole room ablaze.", 2, act("Rooms", 3, {"effect": "active_room_damage", "damage": 15, "type": "Fire", "status": "burn", "duration": 4}), ["fire"])
A("winter_bell", "Winter Bell", "Freeze every foe in the room.", 3, act("Rooms", 4, {"effect": "active_room_damage", "damage": 10, "type": "Frost", "status": "freeze", "duration": 2.5}), ["frost"])
A("plague_rattle", "Plague Rattle", "Poison the whole room.", 2, act("Rooms", 3, {"effect": "active_room_damage", "damage": 8, "type": "Poison", "status": "poison", "duration": 6}), ["poison"])
A("doom_drum", "Doom Drum", "Heavy damage to the whole room, scaling with your damage.", 3, act("Rooms", 5, {"effect": "active_room_damage", "damage": 30, "damageStatScale": 4}), ["offense"])
A("nightmare_mask", "Nightmare Mask", "Fill the room with dread.", 2, act("Rooms", 3, {"effect": "active_room_damage", "damage": 5, "type": "Void", "status": "dread", "duration": 4}), ["void"])
A("wheel_of_knives", "Wheel of Knives", "Burst a ring of shots around you.", 1, act("Seconds", 8, {"effect": "active_projectile_ring", "count": 10, "damageScale": 1.0}), ["multishot"])
A("star_burst", "Star Burst", "A huge ring of shots.", 3, act("Rooms", 3, {"effect": "active_projectile_ring", "count": 24, "damageScale": 1.5}), ["multishot", "light"])
A("wanderers_map", "Wanderer's Map", "Teleport to a random room.", 1, act("Rooms", 2, {"effect": "active_teleport", "target": "RandomRoom"}), ["movement"])
A("homeward_stone", "Homeward Stone", "Return to the first room.", 0, act("Rooms", 1, {"effect": "active_teleport", "target": "StartRoom"}), ["movement"])
A("pilgrims_compass", "Pilgrim's Compass", "Teleport to an unvisited special room.", 3, act("Rooms", 6, {"effect": "active_teleport", "target": "UnvisitedSpecialRoom"}), ["movement"])
A("bait_bucket", "Bait Bucket", "Summon three gnaw rats for this room.", 2, act("Rooms", 3, {"effect": "active_companion_room", "companion": "gnaw_rat", "count": 3}), ["companion"])
A("swarm_jar", "Swarm Jar", "Release five drone mites for this room.", 2, act("Rooms", 3, {"effect": "active_companion_room", "companion": "drone_mite", "count": 5}), ["companion", "insect"])
A("ward_banner", "Ward Banner", "Call two ward knights for this room.", 2, act("Rooms", 4, {"effect": "active_companion_room", "companion": "ward_knight", "count": 2}), ["companion", "defense"])
A("mortar_shell", "Mortar Shell", "Rain explosions on the room.", 2, act("Rooms", 3, {"effect": "active_explosions", "count": 6, "radius": 1.4, "damage": 30}), ["explosive"])
A("thunderhead", "Thunderhead", "Call down a storm of blasts.", 3, act("Rooms", 5, {"effect": "active_explosions", "count": 12, "radius": 1.2, "damage": 35}), ["explosive", "shock"])
A("whetting_ritual", "Whetting Ritual", "Lose half a heart, gain damage forever.", 2, act("Rooms", 3, {"effect": "active_hurt_self", "halves": 1}, {"effect": "active_perm_stat", "stat": "Damage", "value": 0.3}), ["risk", "offense"])
A("blood_offering", "Blood Offering", "Lose a heart, gain 8 coins.", 1, act("Rooms", 2, {"effect": "active_hurt_self", "halves": 2}, {"effect": "active_resource", "resource": "Coins", "amount": 8}), ["risk", "economy"])
A("coin_press", "Coin Press", "Turn 5 coins into a key and a bomb.", 1, act("Rooms", 1, {"effect": "active_resource", "resource": "Coins", "amount": -5}, {"effect": "active_resource", "resource": "Keys", "amount": 1}, {"effect": "active_resource", "resource": "Bombs", "amount": 1}), ["economy"])
A("scrying_bowl", "Scrying Bowl", "Reveal the floor, secrets too.", 2, act("Rooms", 4, {"effect": "active_reveal_map", "secrets": True}), ["utility"])
A("traveler_lamp", "Traveler's Lamp", "Reveal the floor map.", 1, act("Rooms", 3, {"effect": "active_reveal_map"}), ["light", "utility"])
A("healing_salve", "Healing Salve", "Heal a heart and a half.", 1, act("Rooms", 3, {"effect": "active_heal", "halves": 3}), ["health"])
A("holy_bell", "Holy Bell", "Gain a ward.", 2, act("Rooms", 3, {"effect": "active_heal", "halves": 0, "ward": 2}), ["health", "holy"])
A("growth_tonic", "Growth Tonic", "Range up for good.", 2, act("Rooms", 4, {"effect": "active_perm_stat", "stat": "Range", "value": 0.5}), ["precision"])
A("lucky_coin_flip", "Lucky Flip", "Luck up for good.", 2, act("Rooms", 6, {"effect": "active_perm_stat", "stat": "Luck", "value": 1}), ["luck"])
A("bone_flute", "Bone Flute", "Slows every foe in the room.", 1, act("Rooms", 2, {"effect": "active_room_damage", "damage": 0, "type": "Frost", "status": "chill", "duration": 5}), ["frost", "bone"])
A("thunder_drum", "Thunder Drum", "Shock every foe in the room.", 2, act("Rooms", 3, {"effect": "active_room_damage", "damage": 20, "type": "Shock", "status": "shock", "duration": 2}), ["shock"])
A("battle_tonic", "Battle Tonic", "Big burst of rate of fire for 6 seconds.", 2, act("Seconds", 20, {"effect": "active_temp_stat", "stat": "FireRate", "op": "PercentAdd", "value": 0.8, "duration": 6}), ["offense"])
A("frenzy_drum", "Frenzy Drum", "Kills charge it. Damage up for this room.", 2, act("Kills", 12, {"effect": "active_temp_stat", "stat": "Damage", "op": "PercentAdd", "value": 0.6, "duration": 0}), ["offense"])
A("reaper_sickle", "Reaper's Sickle", "Kills charge it. A ring of heavy blades.", 3, act("Kills", 15, {"effect": "active_projectile_ring", "count": 12, "damageScale": 2.5}), ["offense"])
A("soul_jar", "Soul Jar", "Kills charge it. Gain an umbral heart.", 3, act("Kills", 20, {"effect": "active_heal", "halves": 0, "umbral": 2}), ["void", "health"])
A("lucky_pouch", "Lucky Pouch", "Spill a random handful of goods.", 1, act("Rooms", 3, {"effect": "active_spawn_pickups", "pickup": "Coin", "count": 1}, {"effect": "active_spawn_pickups", "pickup": "Bomb", "count": 1}, {"effect": "active_spawn_pickups", "pickup": "Key", "count": 1}), ["economy"])
A("trinket_box", "Trinket Box", "Pop out a trinket. Single use.", 2, act("OneShot", 1, {"effect": "active_spawn_pickups", "pickup": "Trinket", "count": 1}), ["economy"])
A("wishing_bone", "Wishing Bone", "Pop out a random item. Single use.", 4, act("OneShot", 1, {"effect": "active_spawn_pickups", "pickup": "Item", "count": 1}), ["luck", "bone"], ["secret", "treasure"])
A("smoke_pot", "Smoke Pot", "You can't be hit for 5 seconds.", 2, act("Rooms", 3, {"effect": "active_temp_stat", "stat": "Defense", "op": "Flat", "value": 10, "duration": 5}), ["defense"])
A("bonfire_horn", "Bonfire Horn", "Burn and blast everything nearby.", 3, act("Rooms", 4, {"effect": "active_room_damage", "damage": 40, "type": "Fire", "status": "scorch", "duration": 3, "radius": 4}), ["fire", "explosive"])
A("sunlight_vial", "Sunlight Vial", "Holy light sears the room.", 3, act("Rooms", 4, {"effect": "active_room_damage", "damage": 35, "type": "Holy"}), ["holy", "light"])

# ---------------------------------------------------------------- trinkets (58)
TR = [
    ("bent_nail", "Bent Nail", "Damage up a little.", [stat("Damage", 0.25)], ["offense"]),
    ("chipped_tooth", "Chipped Tooth", "Rate of fire up a little.", [stat("FireRate", 0.2)], ["offense"]),
    ("lucky_button", "Lucky Button", "Luck up.", [stat("Luck", 1)], ["luck"]),
    ("sand_glass", "Sand Glass", "Speed up a little.", [stat("MoveSpeed", 0.1)], ["movement"]),
    ("lens_chip", "Lens Chip", "Range up.", [stat("Range", 1.5)], ["precision"]),
    ("pewter_ring", "Pewter Ring", "Shot speed up.", [stat("ShotSpeed", 0.2)], ["precision"]),
    ("sharp_flint", "Sharp Flint", "Critical chance up.", [stat("CritChance", 0.05)], ["precision"]),
    ("stone_bead", "Stone Bead", "Knockback up.", [stat("Knockback", 1.0)], ["offense"]),
    ("worn_buckle", "Worn Buckle", "Take less damage at low vigor.", [dict(stat("Defense", 1), condition="health_at_most:2")], ["defense"]),
    ("snail_shell", "Snail Shell", "Slower, but shots hit harder.", [stat("MoveSpeed", -0.1), stat("Damage", 0.5)], ["risk"]),
    ("ember_chip", "Ember Chip", "Shots sometimes burn.", [elem("Fire", "burn", 0.1)], ["fire"]),
    ("frost_chip", "Frost Chip", "Shots sometimes chill.", [elem("Frost", "chill", 0.12)], ["frost"]),
    ("toxic_chip", "Toxic Chip", "Shots sometimes poison.", [elem("Poison", "poison", 0.12, 4)], ["poison"]),
    ("spark_chip", "Spark Chip", "Shots sometimes shock.", [elem("Shock", "shock", 0.1)], ["shock"]),
    ("rot_chip", "Rot Chip", "Shots sometimes blight.", [elem("Blight", "blight", 0.1, 4)], ["blight"]),
    ("crow_bone", "Crow Bone", "Shots home a little.", [{"effect": "proj_homing", "strength": 0.2}], ["homing", "bird"]),
    ("loose_spring", "Loose Spring", "Shots bounce once.", [{"effect": "proj_bounce", "count": 1}], ["bouncing"]),
    ("splinter", "Splinter", "Shots pierce one foe.", [{"effect": "proj_pierce", "count": 1}], ["piercing"]),
    ("puffball", "Puffball", "Hits sometimes puff spores.", [{"effect": "onhit_status_aoe", "chance": 0.08, "radius": 1.2, "status": "poison", "duration": 3}], ["poison", "plant"]),
    ("firecracker", "Firecracker", "Hits rarely explode.", [{"effect": "onhit_explode", "chance": 0.05, "radius": 1.0, "damageScale": 0.8}], ["explosive"]),
    ("penny_charm", "Penny Charm", "Foes sometimes drop coins.", [{"effect": "onkill_drop", "chance": 0.04, "pickup": "Coin"}], ["economy"]),
    ("small_key_charm", "Key Charm", "Foes rarely drop keys.", [{"effect": "onkill_drop", "chance": 0.025, "pickup": "Key"}], ["economy"]),
    ("fuse_charm", "Fuse Charm", "Foes rarely drop bombs.", [{"effect": "onkill_drop", "chance": 0.025, "pickup": "Bomb"}], ["bomb"]),
    ("tick_charm", "Tick Charm", "Kills rarely heal you.", [{"effect": "onkill_heal", "chance": 0.02, "halves": 1}], ["health", "insect"]),
    ("dented_cup", "Dented Cup", "Clearing rooms sometimes heals.", [{"effect": "roomclear_heal", "chance": 0.1, "halves": 1}], ["health"]),
    ("copper_bell", "Copper Bell", "Clearing rooms sometimes drops a coin.", [{"effect": "roomclear_drop", "chance": 0.2, "pickup": "Coin"}], ["economy"]),
    ("lost_key", "Lost Key", "Clearing rooms sometimes drops a key.", [{"effect": "roomclear_drop", "chance": 0.08, "pickup": "Key"}], ["economy"]),
    ("spare_fuse", "Spare Fuse", "Clearing rooms sometimes drops a bomb.", [{"effect": "roomclear_drop", "chance": 0.08, "pickup": "Bomb"}], ["bomb"]),
    ("prayer_bead", "Prayer Bead", "Clean rooms sometimes grant a ward.", [{"effect": "roomclear_drop", "chance": 0.12, "pickup": "Ward", "requireNoDamage": True}], ["holy"]),
    ("battery_shard", "Battery Shard", "Clearing a room gives one extra active charge.", [{"effect": "roomclear_charge", "amount": 1}], ["charge"]),
    ("static_plug", "Static Plug", "Taking a hit charges your active item.", [{"effect": "ondamaged_charge", "amount": 1}], ["charge"]),
    ("thorn_twig", "Thorn Twig", "Getting hit sometimes bursts thorns.", [{"effect": "ondamaged_burst", "chance": 0.4, "radius": 2.0, "damage": 12, "type": "Physical"}], ["defense", "plant"]),
    ("guardian_feather", "Guardian Feather", "Getting hit rarely grants a ward.", [{"effect": "ondamaged_ward", "chance": 0.12, "halves": 1}], ["defense", "bird"]),
    ("rubber_heel", "Rubber Heel", "A short extra moment of safety after hits.", [{"effect": "invuln_on_hit", "seconds": 0.3}], ["defense"]),
    ("clay_amulet", "Clay Amulet", "Rare chance to ignore a hit.", [{"effect": "incoming_block_chance", "chance": 0.05}], ["defense"]),
    ("soot_mask", "Soot Mask", "Immune to fire.", [{"effect": "incoming_immunity", "types": "Fire"}], ["defense", "fire"]),
    ("lead_lining", "Lead Lining", "Immune to explosions.", [{"effect": "incoming_immunity", "types": "Explosive"}], ["defense", "bomb"]),
    ("bargain_slip", "Bargain Slip", "Shops are a little cheaper.", [{"effect": "shop_discount", "percent": 0.1}], ["economy"]),
    ("loaded_die", "Loaded Die", "Item drops lean your way. Luck up.", [stat("Luck", 2), stat("Damage", -0.2)], ["luck", "risk"]),
    ("heart_locket", "Heart Locket", "Hearts drop more often.", [{"effect": "drop_chance", "pickup": "VigorHalf", "mult": 1.3}], ["health"]),
    ("treasure_map_scrap", "Map Scrap", "Keys drop more often.", [{"effect": "drop_chance", "pickup": "Key", "mult": 1.3}], ["economy"]),
    ("powder_pinch", "Powder Pinch", "Bombs drop more often.", [{"effect": "drop_chance", "pickup": "Bomb", "mult": 1.3}], ["bomb"]),
    ("old_wallet", "Old Wallet", "Gain 3 coins each floor.", [{"effect": "floorstart_resource", "resource": "Coins", "amount": 3}], ["economy"]),
    ("miners_lamp", "Miner's Lamp", "Gain a bomb each floor.", [{"effect": "floorstart_resource", "resource": "Bombs", "amount": 1}], ["bomb", "light"]),
    ("jailer_ring", "Jailer's Ring", "Gain a key each floor.", [{"effect": "floorstart_resource", "resource": "Keys", "amount": 1}], ["economy"]),
    ("short_wick", "Short Wick", "Bombs go off faster.", [{"effect": "bomb_mod", "fuseMult": 0.75}], ["bomb"]),
    ("fat_wick", "Fat Wick", "Bombs are a bit bigger.", [{"effect": "bomb_mod", "radiusMult": 1.15, "damageAdd": 8}], ["bomb"]),
    ("glue_drop", "Glue Drop", "Bombs stick.", [{"effect": "bomb_mod", "sticky": True}], ["bomb"]),
    ("chill_aura_charm", "Cold Charm", "A faint chill around you.", [{"effect": "aura_status", "radius": 1.3, "status": "chill", "interval": 1.5, "duration": 1.5}], ["aura", "frost"]),
    ("doorstop", "Doorstop", "Chill foes when you enter a room.", [{"effect": "roomenter_status", "status": "chill", "duration": 1.5, "radius": 5, "chance": 1.0}], ["frost"]),
    ("fear_tooth", "Fear Tooth", "Foes sometimes cower when you enter.", [{"effect": "roomenter_status", "status": "dread", "duration": 2, "radius": 6, "chance": 0.5}], ["void"]),
    ("pet_collar", "Pet Collar", "Companions hit a bit harder.", [{"effect": "companion_boost", "damageMult": 1.2}], ["companion"]),
    ("hunters_whistle", "Hunter's Whistle", "Companion shots copy your effects.", [{"effect": "companion_inherit", "scale": 0.5}], ["companion"]),
    ("boss_tooth", "Boss Tooth", "Deal more damage to bosses.", [{"effect": "outgoing_mult", "mult": 1.15, "vsBoss": True}], ["offense"]),
    ("whetted_edge", "Whetted Edge", "Critical hits hit a bit harder.", [stat("CritDamage", 0.3)], ["precision"]),
    ("glass_marble", "Glass Marble", "Low vigor makes you fire faster.", [dict(stat("FireRate", 0.5), condition="health_at_most:1")], ["risk"]),
    ("miser_coin", "Miser's Coin", "Damage up while you hold 20 coins.", [dict(stat("Damage", 0.8), condition="coins_at_least:20")], ["economy"]),
    ("pocket_lint", "Pocket Lint", "Utterly useless. Probably. Luck up a little.", [stat("Luck", 0.5)], ["luck"]),
]
for i, (tid, name, desc, eff, tags) in enumerate(TR):
    T(tid, name, desc, 1 if i < 40 else 2, eff, tags)

out = os.path.join("Assets", "StreamingAssets", "Content", "Items", "expansion_items.json")
ids = [d["id"] for d in ITEMS]
assert len(ids) == len(set(ids)), [x for x in ids if ids.count(x) > 1]
with open(out, "w") as f:
    json.dump({"items": ITEMS}, f, indent=1)
from collections import Counter
print(len(ITEMS), Counter(d["kind"] for d in ITEMS))
