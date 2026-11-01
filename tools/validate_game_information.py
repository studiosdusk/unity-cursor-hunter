"""Read-only checks for the v4 JSON handoff. Never opens Unity or runs the game."""
import argparse
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Assets/CursorHunter"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--syntax", action="store_true", help="Also parse C# with optional tree-sitter packages.")
    args = parser.parse_args()
    from validate_bilingual_game_data import validate_documents
    document = validate_documents()
    example = document["information"]
    categories = {c["id"]: c for c in document["progression"]["categories"]}
    entities = {e["id"]: e for e in document["entities"]}
    def values(category, baseline):
        result = []
        current = baseline
        for node in categories[category]["nodes"]:
            operation = node["operation"]
            if operation == "baseline":
                current = baseline
            elif operation == "set":
                current = node["value"]
            elif operation == "add":
                current += node["value"]
            result.append(current)
        return result
    data = {
        "schemaVersion": example["schemaVersion"],
        "normalFieldDurationSeconds": values("stat.fieldDuration", example["stats"]["normalFieldDurationSeconds"]),
        "attackCooldownSeconds": values("stat.cooldown", example["stats"]["attackCooldownSeconds"]),
        "monsters": [dict(m, prefabKey=entities[m["id"]]["prefabKey"],
                          unlockCurrencyId=categories[m["id"]]["nodes"][0]["currencyId"]) for m in example["monsters"]],
        "skills": [dict(s, currencyId=categories[s["id"]]["nodes"][0]["currencyId"])
                   for s in example["skills"] if s["id"] in categories]
    }
    errors = []
    def check(ok, message):
        if not ok: errors.append(message)
    def finite_positive(value):
        return isinstance(value, (int, float)) and math.isfinite(value) and value > 0

    check(data["schemaVersion"] == example["schemaVersion"] == 4, "Schema version must be 4")
    check(set(example) == {"schemaVersion", "balanceVersion", "stats", "rules", "gemstones", "monsters", "skills", "relics", "relicDropRules"}, "Unexpected JSON sections")
    check(len(example["relics"]) == 20, "Expected 20 combat relic definitions")
    check(example["relicDropRules"].get("maxPerKill") == 1, "Relic drop cap must be one per kill")
    gems = ["gem." + name for name in ["garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon"]]
    behavior_profiles = [
        "Basic", "Small", "Fast", "Vanish", "Shield", "Teleport",
        "HighHealth", "Giant", "Healer", "Resurrector",
    ]
    check([g["id"] for g in example["gemstones"]] == gems, "Gemstone IDs/order changed")
    check(len(data["monsters"]) == len(example["monsters"]) == 10, "Expected 10 monsters")
    progression_nodes = [node for category in document["progression"]["categories"]
                         for node in category["nodes"]]
    check("initialBossTier" not in document["progression"],
          "Progression configuration must not gate research by boss tier")
    check(all("requiredBossTier" not in node for node in progression_nodes),
          "Progression nodes must not contain boss-stage unlock restrictions")
    check(all(f"monster.{index:02}" in categories for index in range(1, 11)),
          "Expected all 10 monster research categories")
    check([s["id"] for s in example["skills"]] ==
          ["skill.tornado", "skill.headshot", "skill.thunder", "skill.flame_exhaust",
           "skill.magic_ball", "skill.laser", "skill.pet_baby_dragon"],
          "Expected seven skill IDs")
    check(data["normalFieldDurationSeconds"] == [15, 20, 25, 30], "Normal duration must stop at 30")
    check(all(finite_positive(v) and v >= .05 for v in data["attackCooldownSeconds"]), "Invalid cooldown")
    check(all(data["attackCooldownSeconds"][i] > data["attackCooldownSeconds"][i+1] for i in range(4)), "Cooldown upgrades must decrease")
    progression_nodes = {
        node["id"]: node
        for category in document["progression"]["categories"]
        for node in category["nodes"]
    }
    paid_costs = [node["cost"] for node in progression_nodes.values() if node.get("cost", 0) > 0]
    free_costs = [node["cost"] for node in progression_nodes.values() if node.get("cost", 0) == 0]
    check(len(paid_costs) == 107 and len(free_costs) == 6,
          "Expected 113 researches: 107 paid and six free starting researches")
    check(all(cost % 20 == 0 for cost in paid_costs),
          "Paid research costs must remain exactly doubled from the 10-gem baseline increments")
    check(all(progression_nodes.get(node_id, {}).get("cost") == expected for node_id, expected in {
        "stat.attack.02": 160,
        "stat.radius.02": 160,
        "skill.pet_baby_dragon": 160,
        "skill.magic_ball": 240,
        "skill.tornado": 320,
        "monster.02": 180,
        "monster.02.production.01": 100,
    }.items()), "Representative research costs do not match the 2x balance")
    expected_skill_roots = {
        "skill.pet_baby_dragon": ("gem.garnet", "stat.attack.01"),
        "skill.magic_ball": ("gem.topaz", "skill.pet_baby_dragon.damage.01"),
        "skill.tornado": ("gem.amethyst", "skill.magic_ball"),
        "skill.headshot": ("gem.amethyst", "skill.magic_ball"),
        "skill.thunder": ("gem.sapphire", "skill.headshot.damage.01"),
        "skill.flame_exhaust": ("gem.diamond", "skill.thunder.damage.01"),
        "skill.laser": ("gem.diamond", "skill.thunder.damage.01"),
    }
    skill_category_order = [category["id"] for category in document["progression"]["categories"]
                            if category["id"].startswith("skill.")]
    check(skill_category_order == list(expected_skill_roots),
          "Skill categories must follow the five-phase pet-first progression")
    for skill_id, (currency_id, prerequisite_id) in expected_skill_roots.items():
        root = categories.get(skill_id, {}).get("nodes", [{}])[0]
        check(root.get("currencyId") == currency_id and
              root.get("prerequisiteNodeId") == prerequisite_id,
              f"Wrong phase prerequisite or currency: {skill_id}")
    check(next((monster["hitPoints"] for monster in example["monsters"]
                if monster["id"] == "monster.02"), None) == 15,
          "Queen Bee baseline HP must be 15")
    skill_damage_baselines = (
        ("skill.pet_baby_dragon", "PetSkillController.cs", "PetSkillController.prefab",
         "DamageMultiplier", 3),
        ("skill.magic_ball", "MagicBallSkillController.cs", "MagicBallSkillController.prefab",
         "DamageMultiplier", 5),
        ("skill.tornado", "TornadoSkillController.cs", "TornadoSkillController.prefab",
         "DamageMultiplier", 7),
        ("skill.headshot", "HeadshotSkillController.cs", "HeadshotSkillController.prefab",
         "SplashDamageMultiplier", 7),
        ("skill.thunder", "ThunderSkillController.cs", "ThunderSkillController.prefab",
         "DamageMultiplier", 9),
        ("skill.flame_exhaust", "FlameExhaustSkillController.cs",
         "FlameExhaustSkillController.prefab", "DamageMultiplier", 11),
        ("skill.laser", "LaserSkillController.cs", "LaserSkillController.prefab",
         "DamageMultiplier", 11),
    )
    for skill_id, runtime_name, prefab_name, field_name, damage in skill_damage_baselines:
        runtime_path = BASE / "Combat/Runtime" / runtime_name
        prefab_path = BASE / "Combat/Prefabs/Skills" / prefab_name
        runtime_text = runtime_path.read_text() if runtime_path.exists() else ""
        prefab_text = prefab_path.read_text() if prefab_path.exists() else ""
        number = rf"{damage}(?:\.0)?"
        check(bool(re.search(rf"{field_name}\s*=\s*{number}f?\b", runtime_text)),
              f"{skill_id} runtime baseline damage must be {damage}")
        check(bool(re.search(rf"(?m)^\s*{field_name}: {number}\s*$", prefab_text)),
              f"{skill_id} prefab baseline damage must be {damage}")
    tornado_runtime = (BASE / "Combat/Runtime/TornadoSkillController.cs").read_text()
    tornado_prefab = (BASE / "Combat/Prefabs/Skills/TornadoSkillController.prefab").read_text()
    check(bool(re.search(r"HitIntervalSeconds\s*=\s*1(?:\.0)?f?\b", tornado_runtime)),
          "Tornado must hit at most once per second while overlapping a target")
    check(bool(re.search(r"(?m)^\s*HitIntervalSeconds: 1(?:\.0)?\s*$", tornado_prefab)),
          "Tornado prefab hit interval must match its one-second runtime baseline")
    headshot_runtime = (BASE / "Combat/Runtime/HeadshotSkillController.cs").read_text()
    headshot_prefab = (BASE / "Combat/Prefabs/Skills/HeadshotSkillController.prefab").read_text()
    check(bool(re.search(r"BossDamageMultiplier\s*=\s*3(?:\.0)?f?\b", headshot_runtime)),
          "Headshot boss bonus must preserve its specialization without dominating the phase")
    check(bool(re.search(r"(?m)^\s*BossDamageMultiplier: 3(?:\.0)?\s*$", headshot_prefab)),
          "Headshot prefab boss bonus must match the runtime baseline")
    check(bool(re.search(
        r"m_EditorClassIdentifier: CursorHunter\.Combat::CursorHunter\.Combat\.PlayerCombatStatsDefaults\s+attackPower: 10",
        (BASE / "App/Scenes/Main.unity").read_text())),
        "Main authored attack power must stay 10 for the Queen Bee two-hit baseline")
    known_currencies = {"gem.garnet"}
    override_controllers = {}
    animation_clips = {}
    for path in ROOT.rglob("*.overrideController"):
        meta = Path(str(path) + ".meta")
        if meta.exists():
            match = re.search(r"^guid: ([0-9a-f]+)$", meta.read_text(), re.MULTILINE)
            if match:
                override_controllers[match[1]] = path
    for path in ROOT.rglob("*.anim"):
        meta = Path(str(path) + ".meta")
        if meta.exists():
            match = re.search(r"^guid: ([0-9a-f]+)$", meta.read_text(), re.MULTILINE)
            if match:
                animation_clips[match[1]] = path
    ids = set()
    main_scene_path = BASE / "App/Scenes/Main.unity"
    main_scene_text = main_scene_path.read_text()
    starting_garnet = re.search(r"(?m)^  startingGarnet: (\d+)$", main_scene_text)
    starting_balances = re.search(r"(?m)^  startingGemstoneBalances: ([0-9a-f]+)$", main_scene_text)
    starting_garnet_balance = (
        int.from_bytes(bytes.fromhex(starting_balances.group(1))[:8], "little")
        if starting_balances else -1
    )
    starting_garnet_value = int(starting_garnet.group(1)) if starting_garnet else -1
    starter_tutorial_node_ids = [
        "stat.attack.01",
        "stat.radius.01",
        "stat.cooldown.01",
        "stat.critical.01",
        "stat.fieldDuration.01",
        "monster.01",
    ]
    starter_tutorial_nodes = [
        progression_nodes[node_id]
        for node_id in starter_tutorial_node_ids
        if node_id in progression_nodes
    ]
    tutorial_controller = (
        BASE / "Progression/Runtime/TraitScreenController.cs").read_text()
    tutorial_array = re.search(
        r"StarterTutorialNodeIds\s*=\s*\{([\s\S]*?)\};",
        tutorial_controller)
    controller_tutorial_order = (
        re.findall(r'"([^"]+)"', tutorial_array.group(1))
        if tutorial_array else [])
    right_click_step = re.search(
        r"IsStarterTutorialRightClickStep\(TraitNodeDefinition node\)\s*\{([\s\S]*?)\n        \}",
        tutorial_controller)
    right_click_step_id = (
        re.search(r'node\.Id,\s*"([^"]+)"', right_click_step.group(1))[1]
        if right_click_step and re.search(r'node\.Id,\s*"([^"]+)"', right_click_step.group(1))
        else None
    )
    information_garnet = next(
        (gem.get("amount", 0) for gem in example["gemstones"]
         if gem.get("id") == "gem.garnet"),
        -1,
    )
    check(all(node_id in progression_nodes for node_id in starter_tutorial_node_ids),
          "All starter tutorial research steps must exist")
    check(controller_tutorial_order == starter_tutorial_node_ids,
          "Starter tutorial order must include only basic stats and Stump")
    check(all(node.get("cost", 0) == 0 for node in starter_tutorial_nodes),
          "Every mandatory starter tutorial research must be free")
    check(progression_nodes.get("monster.01", {}).get("operation") == "enable",
          "Starter onboarding must explicitly unlock the basic Stump monster")
    check(right_click_step_id in starter_tutorial_node_ids and
          progression_nodes.get(right_click_step_id, {}).get("cost") == 0,
          "The mandatory right-click practice must use a free tutorial node")
    check(starting_garnet_value == information_garnet,
          "Main scene startingGarnet must match the runtime game-data starting wallet")
    check(starting_garnet_balance == information_garnet,
          "Main scene gemstone balances must match the runtime game-data starting wallet")
    icon_block = re.search(r"(?m)^  monsterIcons:\n((?:  - .*\n)+)", main_scene_text)
    scene_monster_icon_guids = (
        re.findall(r"guid: ([0-9a-f]{32})", icon_block[1]) if icon_block else []
    )
    check(len(scene_monster_icon_guids) == 10, "Main scene must map all ten monster icons")

    def get_body_sprite_guid(prefab_path):
        prefab_text = prefab_path.read_text()
        documents = re.split(r"(?=^--- !u!)", prefab_text, flags=re.MULTILINE)
        game_object_names = {}
        for document in documents:
            game_object = re.match(r"^--- !u!1 &(\d+)", document)
            name = re.search(r"(?m)^  m_Name: (.+)$", document)
            if game_object and name:
                game_object_names[game_object[1]] = name[1]
        body_sprites = []
        for document in documents:
            renderer = re.match(r"^--- !u!212 &", document)
            game_object = re.search(r"(?m)^  m_GameObject: \{fileID: (\d+)\}", document)
            sprite = re.search(r"(?m)^  m_Sprite: \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}", document)
            if (renderer and game_object and sprite and
                    game_object_names.get(game_object[1], "").startswith("Body")):
                body_sprites.append((game_object_names[game_object[1]], sprite[1]))
        exact_body = next((guid for name, guid in body_sprites if name == "Body"), None)
        return exact_body or (body_sprites[0][1] if body_sprites else None)

    for index, monster in enumerate(data["monsters"], 1):
        expected_id = f"monster.{index:02}"
        check(monster["id"] == expected_id and expected_id not in ids, "Monster ID/order mismatch")
        ids.add(expected_id)
        check(monster["unlockCurrencyId"] in known_currencies, f"Circular currency gate: {expected_id}")
        check(monster["gemstoneId"] in gems, "Unknown drop currency")
        check(finite_positive(monster["hitPoints"]) and finite_positive(monster["spawnIntervalSeconds"]), "Invalid monster balance")
        if monster["gemstoneAmount"] > 0: known_currencies.add(monster["gemstoneId"])
        definition = BASE / f"Data/Resources/MonsterDefinitions/Monster{index:02}.asset"
        check(definition.exists(), f"Missing visual definition: {expected_id}")
        if definition.exists():
            text = definition.read_text()
            check(f"monsterId: {expected_id}" in text, "Wrong monster asset ID")
            # Growth/progression reads the JSON, while live spawn and drops
            # read MonsterDefinition. Keep the duplicated authoring values in
            # sync so the tooltip, summary and combat reward cannot disagree.
            def asset_scalar(field):
                match = re.search(r"(?m)^    " + re.escape(field) + r": (.*)$", text)
                return match.group(1).strip() if match else None

            for asset_field, info_field in (
                    ("maxHealth", "hitPoints"),
                    ("spawnIntervalSeconds", "spawnIntervalSeconds"),
                    ("garnetReward", "garnetReward"),
                    ("bonusDropAmount", "gemstoneAmount"),
                    ("bonusDropChancePercent", "gemstoneChancePercent")):
                raw = asset_scalar(asset_field)
                check(raw is not None, f"Missing {asset_field} in combat definition: {expected_id}")
                if raw is not None:
                    try:
                        check(math.isclose(float(raw), float(monster[info_field]),
                                           rel_tol=0.0, abs_tol=0.0001),
                              f"Growth/combat {info_field} mismatch: {expected_id}")
                    except (TypeError, ValueError):
                        check(False, f"Invalid {asset_field} in combat definition: {expected_id}")

            if monster["gemstoneAmount"] > 0:
                raw_currency = asset_scalar("bonusDropCurrencyId")
                check(raw_currency == monster["gemstoneId"],
                      f"Growth/combat gemstone ID mismatch: {expected_id}")

            display_name = re.search(r"(?m)^  displayName: (.+)$", text)
            check(bool(display_name), f"Missing trait-screen display name: {expected_id}")
            check(bool(display_name) and bool(display_name.group(1).strip()),
                  f"Empty trait-screen display name: {expected_id}")
            profile_name = behavior_profiles[index - 1]
            profile_path = BASE / f"Data/Resources/MonsterBehaviorProfiles/{profile_name}.asset"
            profile_meta = Path(str(profile_path) + ".meta")
            check(profile_path.exists() and profile_meta.exists(),
                  f"Missing combat behavior profile for {expected_id}: {profile_name}")
            if profile_meta.exists():
                profile_guid = re.search(r"(?m)^guid: ([0-9a-f]+)$", profile_meta.read_text())
                check(bool(profile_guid) and profile_guid.group(1) in text,
                      f"Wrong combat behavior profile for {expected_id}: {profile_name}")
            match = re.search(r"visualPrefab: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}", text)
            check(bool(match), f"Missing prefab: {expected_id}")
            if match:
                source = ROOT / "Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/Prefabs" / (monster["prefabKey"] + ".prefab")
                check(source.exists(), f"Missing prefab path: {source}")
                if source.exists():
                    source_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(source) + ".meta").read_text(), re.MULTILINE)[1]
                    if index <= len(scene_monster_icon_guids):
                        sprite_dir = ROOT / "Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites" / monster["prefabKey"]
                        sprite = next((candidate for candidate in (sprite_dir / "Body.png", sprite_dir / "Body_Fire.png") if candidate.exists()), None)
                        check(sprite is not None, f"Missing body icon source: {expected_id}")
                        if sprite is not None:
                            sprite_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(sprite) + ".meta").read_text(), re.MULTILINE)[1]
                            check(scene_monster_icon_guids[index - 1] == sprite_guid,
                                  f"Trait icon does not match the combat monster body: {expected_id}")
                            check(get_body_sprite_guid(source) == sprite_guid,
                                  f"Combat prefab body does not match its selected icon source: {expected_id}")
                    variant = BASE / "Combat/Prefabs/Monsters/Visuals" / (source.stem + "_Combat.prefab")
                    if variant.exists():
                        variant_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(variant) + ".meta").read_text(), re.MULTILINE)[1]
                        check(match[2] == variant_guid, f"Combat visual variant GUID mismatch: {expected_id}")
                        variant_text = variant.read_text()
                        check(f"m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}" in variant_text,
                              f"Combat visual source mismatch: {expected_id}")
                        controller_match = re.search(
                            r"propertyPath: m_Controller[\s\S]*?objectReference: \{fileID: 22100000, guid: ([0-9a-f]+), type: 2\}",
                            variant_text)
                        if controller_match:
                            controller = override_controllers.get(controller_match[1])
                            check(controller is not None, f"Missing death override controller asset: {expected_id}")
                            if controller is not None:
                                clip_match = re.search(
                                    r"m_OverrideClip: \{fileID: 7400000, guid: ([0-9a-f]+), type: 2\}",
                                    controller.read_text())
                                clip = animation_clips.get(clip_match[1]) if clip_match else None
                                check(clip is not None, f"Death override clip is missing: {expected_id}")
                                if clip is not None:
                                    check("m_LoopTime: 0" in clip.read_text(),
                                          f"Death override clip still loops: {expected_id}")
                    else:
                        check(match[2] == source_guid, f"Prefab GUID mismatch: {expected_id}")
                        check(f"--- !u!1 &{match[1]}\n" in source.read_text(), f"Prefab root fileID missing: {expected_id}")
    check(known_currencies == set(gems), "Some currencies have no attainable monster source")
    expected_progression_skills = {
        "skill.pet_baby_dragon", "skill.tornado", "skill.thunder", "skill.headshot",
        "skill.flame_exhaust", "skill.magic_ball", "skill.laser",
    }
    check({skill["id"] for skill in data["skills"]} == expected_progression_skills,
          "Progression skill categories must contain the starter pet and six skills")
    for skill in data["skills"]:
        check(skill["currencyId"] in known_currencies, "Unattainable skill currency")
    check(all(skill_id in categories and categories[skill_id].get("nodes")
              for skill_id in expected_progression_skills),
          "Each skill must have a purchase node")
    for skill_id in expected_progression_skills - {"skill.pet_baby_dragon"}:
        skill_nodes = categories.get(skill_id, {}).get("nodes", [])
        check(any(node.get("id", "").startswith(skill_id + ".damage.")
                  for node in skill_nodes),
              f"Missing independent damage research: {skill_id}")
        check(any(node.get("id") == skill_id + ".damage.02"
                  for node in skill_nodes),
              f"Missing second damage research: {skill_id}")
        check(any(node.get("id", "").startswith(skill_id + ".radius.")
                  for node in skill_nodes),
              f"Missing independent radius research: {skill_id}")
    pet_nodes = categories.get("skill.pet_baby_dragon", {}).get("nodes", [])
    check(any(node.get("id", "").startswith("skill.pet_baby_dragon.damage.")
              for node in pet_nodes), "Pet needs independent damage research")
    check(any(node.get("id") == "skill.pet_baby_dragon.damage.03"
              for node in pet_nodes), "Pet needs its third damage research")
    check(any(node.get("id", "").startswith("skill.pet_baby_dragon.simultaneousHits.")
              for node in pet_nodes), "Pet needs simultaneous-hit research")

    # Keep the early-field supply path explicit: GameData stores the resolved
    # per-wave count, while the authored MonsterDefinition keeps one base
    # member and App adds the progression bonus when preparing a live run.
    stump = next((monster for monster in example["monsters"]
                  if monster["id"] == "monster.01"), None)
    stump_category = categories.get("monster.01", {})
    stump_production_nodes = [
        node for node in stump_category.get("nodes", [])
        if node.get("id", "").startswith("monster.01.production.")
    ]
    stump_definition = BASE / "Data/Resources/MonsterDefinitions/Monster01.asset"
    check(stump is not None and stump.get("productionCount") == 2 and
          math.isclose(stump.get("spawnIntervalSeconds", 0), 0.75,
                       rel_tol=0.0, abs_tol=0.0001),
          "Stump baseline must produce two per opportunity every 0.75 seconds")
    check([node.get("value") for node in stump_production_nodes] == [3, 4, 5] and
          all(node.get("operation") == "set" for node in stump_production_nodes),
          "Stump production research must set resolved counts to 3, 4, then 5")
    if stump_definition.exists():
        stump_asset_text = stump_definition.read_text()
        stump_pack_size = re.search(r"(?m)^    packSize: (\d+)$", stump_asset_text)
        stump_interval = re.search(r"(?m)^    spawnIntervalSeconds: ([^\s]+)$", stump_asset_text)
        check(bool(stump_pack_size) and int(stump_pack_size.group(1)) == 1,
              "Stump combat definition must use one base member before progression bonus")
        check(bool(stump_interval) and
              math.isclose(float(stump_interval.group(1)), 0.75,
                           rel_tol=0.0, abs_tol=0.0001),
              "Stump combat spawn interval must match its 0.75-second progression baseline")
    else:
        check(False, "Missing Stump combat definition")

    tornado_controller = BASE / "Combat/Prefabs/Skills/TornadoSkillController.prefab"
    tornado_object = BASE / "Combat/Prefabs/Skills/TornadoObject.prefab"
    check(tornado_controller.exists(), "Missing tornado controller prefab")
    check(tornado_object.exists(), "Missing tornado object prefab")
    if tornado_controller.exists() and tornado_object.exists():
        controller_guid = (tornado_controller.with_suffix(".prefab.meta")).read_text().split("guid: ")[1].splitlines()[0]
        object_guid = (tornado_object.with_suffix(".prefab.meta")).read_text().split("guid: ")[1].splitlines()[0]
        check(object_guid in tornado_controller.read_text(), "Tornado controller does not reference its pooled object")
        check("2c00aae28c77fcd47ba0036f2a5a4865" in tornado_object.read_text(), "Tornado object does not reference the selected snow effect")
        for relative, parent in (("App/Scenes/Main.unity", "2188000105"),
                                 ("Combat/Scenes/CombatSandbox.unity", "201")):
            scene_text = (BASE / relative).read_text()
            check(f"m_SourcePrefab: {{fileID: 100100000, guid: {controller_guid}, type: 3}}" in scene_text,
                  f"Tornado controller is not a prefab instance in {relative}")
            check(f"m_TransformParent: {{fileID: {parent}}}" in scene_text,
                  f"Tornado controller is not under the combat manager in {relative}")
            check("tornadoControllerPrefab:" not in scene_text,
                  f"Old runtime-instantiated tornado controller remains in {relative}")

    magic_controller = BASE / "Combat/Prefabs/Skills/MagicBallSkillController.prefab"
    magic_object = BASE / "Combat/Prefabs/Skills/MagicBallObject.prefab"
    magic_visual = ROOT / "Assets/DownLoadAssets/118 sprite effects bundle/25 sprite effects/_prefabs/Magic_Sphere_blue_Visual.prefab"
    for path in (magic_controller, magic_object, magic_visual):
        check(path.exists(), f"Missing Magic Ball prefab: {path}")
    if all(path.exists() for path in (magic_controller, magic_object, magic_visual)):
        magic_controller_guid = magic_controller.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        magic_object_guid = magic_object.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        magic_visual_guid = magic_visual.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        check(magic_object_guid in magic_controller.read_text(), "Magic Ball controller does not reference its ball")
        check(magic_visual_guid in magic_object.read_text(), "Magic Ball object does not contain the selected visual")
        magic_object_text = magic_object.read_text()
        check("boundaryCollider: {fileID: 100002}" in magic_object_text and
              "hitbox: {fileID: 5411213688557413086}" in magic_object_text and
              magic_object_text.count("CircleCollider2D:") >= 2,
              "Magic Ball needs separate root boundary and child attack circles")
        for relative, parent in (("App/Scenes/Main.unity", "2188000105"),
                                 ("Combat/Scenes/CombatSandbox.unity", "201")):
            scene_text = (BASE / relative).read_text()
            check(re.search(r"m_TransformParent: \{fileID: " + parent +
                            r"\}[\s\S]*?m_SourcePrefab: \{fileID: 100100000, guid: " +
                            magic_controller_guid + r", type: 3\}", scene_text),
                  f"Magic Ball controller is not under the combat manager in {relative}")

    laser_controller = BASE / "Combat/Prefabs/Skills/LaserSkillController.prefab"
    laser_effect = BASE / "Combat/Prefabs/Skills/LaserEffect.prefab"
    laser_visual = ROOT / "Assets/DownLoadAssets/118 sprite effects bundle/15 effects/_prefabs/Lazer_normal_Visual.prefab"
    for path in (laser_controller, laser_effect, laser_visual):
        check(path.exists(), f"Missing Laser prefab: {path}")
    if all(path.exists() for path in (laser_controller, laser_effect, laser_visual)):
        laser_controller_guid = laser_controller.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        laser_effect_guid = laser_effect.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        laser_visual_guid = laser_visual.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        check(laser_effect_guid in laser_controller.read_text(), "Laser controller does not reference its effect")
        laser_effect_text = laser_effect.read_text()
        check(laser_visual_guid in laser_effect_text, "Laser effect does not contain the selected visual")
        check("hitbox: {fileID: 300102}" in laser_effect_text and
              "BoxCollider2D:" in laser_effect_text and "m_IsTrigger: 1" in laser_effect_text,
              "Laser effect needs a child rectangular trigger HitBox")
        for relative, parent in (("App/Scenes/Main.unity", "2188000105"),
                                 ("Combat/Scenes/CombatSandbox.unity", "201")):
            scene_text = (BASE / relative).read_text()
            check(re.search(r"m_TransformParent: \{fileID: " + parent +
                            r"\}[\s\S]*?m_SourcePrefab: \{fileID: 100100000, guid: " +
                            laser_controller_guid + r", type: 3\}", scene_text),
                  f"Laser controller is not under the combat manager in {relative}")

    pet_controller = BASE / "Combat/Prefabs/Skills/PetSkillController.prefab"
    pet_effect = ROOT / "Assets/DownLoadAssets/Casual_VFX/Cartoon Casual VFX Pack/Prefabs/Explosion/Explosion_Effect_Blue.prefab"
    pet_scene = (BASE / "App/Scenes/Main.unity").read_text()
    for path in (pet_controller, pet_effect):
        check(path.exists(), f"Missing BabyDragon skill asset: {path}")
    if pet_controller.exists() and pet_effect.exists():
        pet_guid = pet_controller.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        effect_guid = pet_effect.with_suffix(".prefab.meta").read_text().split("guid: ")[1].splitlines()[0]
        check(effect_guid in pet_controller.read_text(), "BabyDragon skill does not reference the blue impact effect")
        check(re.search(r"m_TransformParent: \{fileID: 2188000105\}[\s\S]*?"
                        r"m_SourcePrefab: \{fileID: 100100000, guid: " + pet_guid +
                        r", type: 3\}", pet_scene),
              "BabyDragon skill controller is not under Main CombatManager")
        check("propertyPath: petRoot" in pet_scene and
              "objectReference: {fileID: 2127624400}" in pet_scene and
              "propertyPath: groundRing" in pet_scene and
              "objectReference: {fileID: 1999272709}" in pet_scene and
              "propertyPath: worldCamera" in pet_scene and
              "objectReference: {fileID: 1273511514}" in pet_scene,
              "BabyDragon skill scene references are missing")
        check("m_SourcePrefab: {fileID: 100100000, guid: 5110becfdecf74563833b775b9ce1677, type: 3}" in pet_scene,
              "BabyDragon's child ally ring is missing from Main")

    controller = (BASE / "Progression/Runtime/TraitScreenController.cs").read_text()
    check("CreateGameInformationJson" in controller and "SourceJson" in controller, "Summary must use captured JSON")
    check("GetOwnedRelicIds" in controller, "Relic test ownership is not included in resolved JSON")
    combat = (BASE / "Combat/Runtime/CombatRunController.cs").read_text()
    check("ConfigureRelics" in combat and "RelicSnapshots" in combat,
          "Relic combat snapshot handoff is missing")
    check("GetGemstoneDropWeight" not in controller, "Old weighted gemstone unlock path remains")
    catalog = (BASE / "Progression/Runtime/TraitCatalog.cs").read_text()
    check('AddGemstoneChain' not in catalog and '"stat.multiClick"' not in catalog, "Old unlock/multi-click catalog remains")
    check('CreateTabButton(tabs, "PETTab"' not in controller, "Pet tab remains")
    check('CreateTabButton(tabs, "LOOTTab"' not in controller, "Retired relic tab remains")
    check(all(m["behaviorType"] == 0 for m in example["monsters"]), "Only reserved None behavior is defined")
    for path in BASE.rglob("*.cs"):
        source = path.read_text()
        check(not re.search(r"\b(?:AddComponent|GetComponent)(?:InChildren)?<Text>", source), f"Legacy Text component: {path}")
        check(not re.search(r"\b(?:HorizontalWrapMode|VerticalWrapMode)\b", source), f"Legacy Text property: {path}")
        check(not re.search(r"(\[[^\n]+\])\s*\1", source), f"Repeated C# attribute: {path}")

    scene = (BASE / "App/Scenes/Main.unity").read_text()
    check("testModeUnlockAll: 0" in scene and "testModeFreeUpgrades: 0" in scene, "Scene still bypasses progression")
    check("  m_FontData:" not in scene, "Legacy Text remains in main scene")
    check("m_ReferenceResolution: {x: 1920, y: 1080}" in scene, "Wrong canvas design resolution")
    check('m_RenderScale: 1' in (ROOT / "Assets/Settings/UniversalRP.asset").read_text(), "Render scale below native")
    if args.syntax:
        import tree_sitter
        import tree_sitter_c_sharp
        parser = tree_sitter.Parser(tree_sitter.Language(tree_sitter_c_sharp.language()))
        for path in BASE.rglob("*.cs"):
            tree = parser.parse(path.read_bytes())
            if tree.root_node.has_error:
                stack = [tree.root_node]
                while stack:
                    node = stack.pop()
                    if node.type == "ERROR" or node.is_missing:
                        errors.append(f"C# syntax: {path.relative_to(ROOT)}:{node.start_point.row+1} {node.type}")
                    stack.extend(node.children)
    if errors: raise SystemExit("\n".join(errors))
    print("PASS: JSON sections, MonsterDefinition baselines, Stump production, starter onboarding order, currency/prerequisite-based skill and monster progression, 10 monsters, seven skill IDs, 20 combat relic definitions, currency reachability, prefab IDs, TMP, scene and render settings" + (", C# syntax" if args.syntax else ""))
    print("Unity import, semantic compilation and gameplay were NOT run.")

if __name__ == "__main__":
    main()
