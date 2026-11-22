#!/usr/bin/env python3
"""Generates Content/Synergies/expansion_transformations.json (10 transformations + synergies). Run from repo root."""
import json
def stat(s, v, op="Flat"): return {"effect": "stat", "stat": s, "op": op, "value": v}
def tf(id, name, desc, tag, effects, count=3):
    return {"id": id, "name": name, "description": desc, "requires": [{"tag": tag, "count": count}], "effects": effects, "visualKey": "transform:" + id}
TF = [
    tf("plaguebearer", "Plaguebearer", "Poison clings to you; poisoned foes spread it when they die.", "poison",
       [{"effect": "aura_status", "radius": 1.5, "status": "poison", "interval": 1, "duration": 3},
        {"effect": "onkill_spread_status", "status": "poison", "radius": 2.0, "duration": 3, "requireType": "Poison"},
        {"effect": "incoming_immunity", "types": "Poison"}]),
    tf("stormcaller", "Stormcaller", "Lightning leaps from every shot; shock can't touch you.", "shock",
       [{"effect": "proj_chain", "jumps": 2, "range": 3.0, "damageScale": 0.5}, {"effect": "incoming_immunity", "types": "Shock"}, stat("ShotSpeed", 0.2)]),
    tf("rimeborn", "Rimeborn", "Cold follows you and your shots bite with frost.", "frost",
       [{"effect": "aura_status", "radius": 1.6, "status": "chill", "interval": 1, "duration": 2},
        {"effect": "proj_element", "type": "Frost", "status": "chill", "chance": 0.25, "duration": 3}, stat("Defense", 1)]),
    tf("powderkin", "Powderkin", "Bombs are your friends: bigger, safer, and always in stock.", "bomb",
       [{"effect": "bomb_mod", "safe": True, "radiusMult": 1.2, "damageAdd": 15}, {"effect": "grant_resource", "resource": "Bombs", "amount": 5},
        {"effect": "floorstart_resource", "resource": "Bombs", "amount": 1}]),
    tf("huntmaster", "Huntmaster", "Your shots hunt on their own and strike true.", "homing",
       [{"effect": "proj_homing", "strength": 0.5}, stat("CritChance", 0.08), stat("Range", 1.5)]),
    tf("bulwark", "Bulwark", "You become a wall: tougher and safer after hits.", "defense",
       [stat("Defense", 1), {"effect": "incoming_block_chance", "chance": 0.1}, {"effect": "grant_health", "containers": 1, "heal": 2}]),
    tf("goldtongue", "Goldtongue", "Merchants love you and coins find their way to you.", "economy",
       [{"effect": "shop_discount", "percent": 0.2}, {"effect": "drop_chance", "pickup": "Coin", "mult": 1.5},
        {"effect": "stat_scaling", "stat": "Luck", "perUnit": 0.05, "source": "coins", "max": 3}], count=4),
    tf("swarmlord", "Swarmlord", "Insects gather to you; drone mites follow wherever you go.", "insect",
       [{"effect": "companion", "companion": "drone_mite", "count": 2}, stat("FireRate", 0.3)]),
    tf("ossuary_saint", "Ossuary Saint", "Holy bone: wards come easily and holy shots burn the dark.", "holy",
       [{"effect": "proj_element", "type": "Holy"}, {"effect": "ondamaged_ward", "chance": 0.3, "halves": 2},
        {"effect": "outgoing_mult", "mult": 1.2, "types": "Holy"}]),
    tf("verdant", "Verdant", "Roots and thorns: slow healing and thorns when struck.", "plant",
       [{"effect": "regen", "interval": 45, "halves": 1},
        {"effect": "ondamaged_burst", "chance": 1.0, "radius": 2.2, "damage": 15, "type": "Physical", "status": "poison", "duration": 3}], count=4),
]
SY = [
    {"id": "frostfire", "name": "Frostfire", "description": "Fire and frost together scorch and shatter.", "priority": 10,
     "requires": [{"tag": "fire", "count": 1}, {"tag": "frost", "count": 1}],
     "effects": [{"effect": "outgoing_mult", "mult": 1.2, "types": "Fire, Frost"}]},
    {"id": "thunder_powder", "name": "Thunder Powder", "description": "Shock and explosions: hits sometimes detonate in sparks.", "priority": 10,
     "requires": [{"itemId": "storm_heart"}, {"itemId": "powder_shot"}],
     "effects": [{"effect": "onhit_status_aoe", "chance": 0.25, "radius": 1.8, "status": "shock", "duration": 2}]},
    {"id": "orbit_blades", "name": "Orbiting Blades", "description": "Returning blades circle you first.", "priority": 15, "exclusiveGroup": "delivery_orbit",
     "requires": [{"itemId": "crescent_blade"}, {"itemId": "orbit_stone"}],
     "effects": [{"effect": "proj_damage_mult", "mult": 1.3}]},
    {"id": "glass_storm", "name": "Glass Storm", "description": "Shards on shards: splits keep splitting.", "priority": 10,
     "requires": [{"itemId": "shatter_glass"}, {"itemId": "fan_of_knives"}],
     "effects": [{"effect": "onhit_burst", "chance": 0.2, "count": 4, "damageScale": 0.3}]},
    {"id": "kennel", "name": "Kennel", "description": "Two hunters make a pack; both bite harder.", "priority": 5,
     "requires": [{"itemId": "gnaw_rat"}, {"itemId": "bramble_hound"}],
     "effects": [{"effect": "companion_boost", "damageMult": 1.4}]},
    {"id": "dead_mans_purse", "name": "Dead Man's Purse", "description": "Greed and grudge: coins make you deadly.", "priority": 5,
     "requires": [{"itemId": "misers_ledger"}, {"itemId": "fat_purse"}],
     "effects": [{"effect": "onkill_drop", "chance": 0.06, "pickup": "Coin"}]},
]
json.dump({"synergies": SY, "transformations": TF}, open("Assets/StreamingAssets/Content/Synergies/expansion_transformations.json", "w"), indent=1)
print(len(TF), len(SY))
