#!/usr/bin/env python3
"""Generates Content/Progression/expansion_progression.json: 22 challenges and 50 achievements.

The starter progression (12 characters, 8 challenges, 63 achievements) lives in progression.json.
Run from the repo root: python3 Tools/ContentGen/gen_progression.py
"""
import json

def stat(s, v, op="Flat"): return {"effect": "stat", "stat": s, "op": op, "value": v}

# (id, name, description, fields) ; "gate" = boss id whose first defeat unlocks the challenge
CH = [
 ("ch_iron_will", "Iron Will", "Start with Bark Skin and Scale Mail. Shots are weak.", "osseous_matron",
  dict(characterId="wanderer", startingItems=["bark_skin", "scale_mail"], effects=[stat("Damage", -1.0)])),
 ("ch_marsh_crossing", "Marsh Crossing", "Reach the Mire Hollows' end with poison in your blood and no shops.", "the_drowned_mother",
  dict(characterId="thornmaid", startingItems=["nettle_sap"], noShops=True, goal="ReachFloor", goalFloor=4)),
 ("ch_forge_born", "Forge-Born", "Fire immune, fire shot, half your vigor.", "the_ironmonger",
  dict(characterId="ashwright", startingItems=["asbestos_cloak", "kindling_bundle"], effects=[stat("MaxHealth", -1)])),
 ("ch_long_winter", "The Long Winter", "Everything you hit freezes, but you crawl.", "the_winter_widow",
  dict(characterId="wanderer", startingItems=["glacier_core"], effects=[stat("MoveSpeed", -0.3)])),
 ("ch_green_thumb", "Green Thumb", "Companions only: your own shots barely sting.", "the_gardener",
  dict(characterId="beastcaller", startingItems=["thorn_ring", "gnaw_rat", "bramble_hound"], effects=[stat("Damage", -2.0)])),
 ("ch_bell_toll", "Bell Toll", "Start with the Storm Bell and Battery Heart. No treasure rooms.", "the_bellringer",
  dict(characterId="bellwarden", startingItems=["storm_bell", "battery_heart"], noTreasureRooms=True)),
 ("ch_flooded_faith", "Flooded Faith", "Holy shots and two wards. Hearts never drop.", "the_abbess_below",
  dict(characterId="hollow_saint", startingItems=["burnt_hymnal", "holy_water"], noHealthPickups=True)),
 ("ch_harvest_moon", "Harvest Moon", "Seed Pod and Shatter Glass. Every floor is shrouded.", "the_harvester",
  dict(characterId="wanderer", startingItems=["seed_pod", "shatter_glass"], forcedHex="hex_shroud")),
 ("ch_queen_swarm", "Queen of the Swarm", "Mite hive and swarm jar. Four floors to prove it.", "the_hive_queen",
  dict(characterId="moth_seer", startingItems=["mite_hive", "swarm_jar", "hive_mind"], goal="ReachFloor", goalFloor=5)),
 ("ch_glass_cannon", "Glass Cannon", "Huge damage, one heart, no wards.", "the_glazier",
  dict(characterId="wanderer", startingItems=["executioner_mark", "glass_fang"], effects=[stat("MaxHealth", -2)])),
 ("ch_lights_out", "Lights Out", "Every floor is in gloom. Your lantern helps a little.", "the_umbral_regent",
  dict(characterId="lanternbearer", forcedHex="hex_gloom", startingItems=["dread_lantern"])),
 ("ch_ash_crown", "Crown of Ash", "Every hex, every floor. Nightmare difficulty.", "the_ash_king",
  dict(characterId="ashwright", hexChance=1.0, difficultyId="nightmare")),
 ("ch_heartless", "Heartless", "The Unmade with umbral hearts only, and no health drops.", "the_beating_dark",
  dict(characterId="the_unmade", startingItems=["shadow_wine"], noHealthPickups=True)),
 ("ch_last_light", "Last Light", "Reach the Last Hollow on hard with only the Dawn Prism.", "the_hollow_one",
  dict(characterId="lanternbearer", startingItems=["dawn_prism"], difficultyId="hard")),
 ("ch_pauper_prince", "Pauper Prince", "The Gambler with no coins and no shops.", None,
  dict(characterId="gambler", startingCoins=0, noShops=True)),
 ("ch_bomb_squad", "Bomb Squad", "Ninety-nine bombs. Shots barely work.", None,
  dict(characterId="tinker", startingBombs=99, startingItems=["tamed_powder", "cluster_charge"], effects=[stat("Damage", -2.0), stat("FireRate", -0.5)])),
 ("ch_speedrun", "Breakneck", "Speed is all you have. Reach the fifth floor.", None,
  dict(characterId="quickling", startingItems=["hollow_boots", "runners_wrap"], goal="ReachFloor", goalFloor=5, effects=[stat("MaxHealth", -1)])),
 ("ch_maze_runner", "Maze Runner", "Every floor is a labyrinth.", None,
  dict(characterId="wanderer", forcedHex="hex_labyrinth", goal="ReachFloor", goalFloor=6)),
 ("ch_blindfold", "Blindfold", "Every floor is veiled. Trust your instincts.", None,
  dict(characterId="moth_seer", forcedHex="hex_veil")),
 ("ch_frailty", "Brittle Bones", "Every floor carries frailty.", None,
  dict(characterId="wanderer", forcedHex="hex_frailty", goal="ReachFloor", goalFloor=8)),
 ("ch_one_shot", "One Shot", "Charge every shot; you can't fire quickly.", None,
  dict(characterId="wanderer", startingItems=["drawn_bow", "grudge_stone"], effects=[stat("FireRate", -0.8)])),
 ("ch_menagerie", "Menagerie", "Five companions to start. You cannot hurt anything yourself.", None,
  dict(characterId="beastcaller", startingItems=["magpie", "spite_imp", "echo_shade", "ash_halo", "ward_knight"], effects=[stat("Damage", -10.0)], goal="ReachFloor", goalFloor=5)),
]

ACH = []
def ach(id, name, desc, trigger, key=None, target=1, unlocks=None):
    a = {"id": id, "name": name, "description": desc, "trigger": trigger, "target": target}
    if key: a["key"] = key
    if unlocks: a["unlocks"] = unlocks
    ACH.append(a)

BOSSES = [("osseous_matron", "The Osseous Matron"), ("the_drowned_mother", "The Drowned Mother"), ("the_ironmonger", "The Ironmonger"),
          ("the_winter_widow", "The Winter Widow"), ("the_gardener", "The Gardener"), ("the_bellringer", "The Bellringer"),
          ("the_abbess_below", "The Abbess Below"), ("the_harvester", "The Harvester"), ("the_hive_queen", "The Hive Queen"),
          ("the_glazier", "The Glazier"), ("the_umbral_regent", "The Umbral Regent"), ("the_ash_king", "The Ash King"),
          ("the_beating_dark", "The Beating Dark"), ("the_hollow_one", "The Hollow One"), ("the_unformed", "The Unformed")]
challenges = []
for cid, name, desc, gate, f in CH:
    c = {"id": cid, "name": name, "description": desc}
    f = dict(f)
    if "goal" in f: c["goal"] = f.pop("goal")
    c.update(f)
    if gate: c["unlockId"] = "challenge_" + cid[3:]
    challenges.append(c)
gates = {gate: "challenge_" + cid[3:] for cid, _, _, gate, _ in CH if gate}
for bid, bname in BOSSES:
    ach("slay_" + bid, "Slayer of " + bname.replace("The ", ""), f"Defeat {bname}.", "BossDefeated", bid, unlocks=[gates[bid]] if bid in gates else None)
for n, name in ((5, "Halfway Down"), (10, "Deep Delver"), (15, "The Bottom")):
    ach(f"reach_floor_{n}", name, f"Reach floor {n}.", "ReachFloor", target=n)
for tid, name in (("plaguebearer", "Plaguebearer"), ("stormcaller", "Stormcaller"), ("rimeborn", "Rimeborn"), ("powderkin", "Powderkin"),
                  ("huntmaster", "Huntmaster"), ("bulwark", "Bulwark"), ("goldtongue", "Goldtongue"), ("swarmlord", "Swarmlord"),
                  ("ossuary_saint", "Ossuary Saint"), ("verdant", "Verdant")):
    ach("become_" + tid, "Become " + name, f"Transform into the {name}.", "Transformation", tid)
for cid, name, _, _, _ in CH:
    ach("done_" + cid, name + " Complete", f"Complete the {name} challenge.", "ChallengeComplete", cid)

json.dump({"challenges": challenges, "achievements": ACH}, open("Assets/StreamingAssets/Content/Progression/expansion_progression.json", "w"), indent=1)
print(len(challenges), "challenges;", len(ACH), "achievements")
