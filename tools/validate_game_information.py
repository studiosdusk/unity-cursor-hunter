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
    check([g["id"] for g in example["gemstones"]] == gems, "Gemstone IDs/order changed")
    check(len(data["monsters"]) == len(example["monsters"]) == 10, "Expected 10 monsters")
    check([s["id"] for s in example["skills"]] ==
          ["skill.tornado", "skill.headshot", "skill.thunder", "skill.flame_exhaust",
           "skill.magic_ball", "skill.laser", "skill.pet_baby_dragon"],
          "Expected seven skill IDs")
    check(data["normalFieldDurationSeconds"] == [15, 20, 25, 30], "Normal duration must stop at 30")
    check(all(finite_positive(v) and v >= .05 for v in data["attackCooldownSeconds"]), "Invalid cooldown")
    check(all(data["attackCooldownSeconds"][i] > data["attackCooldownSeconds"][i+1] for i in range(4)), "Cooldown upgrades must decrease")
    known_currencies = {"gem.garnet"}
    death_variants = {
        2: "Bat", 5: "Scorpion", 6: "Fairy", 7: "Spider",
        8: "Golem", 9: "Bud", 10: "Skeleton",
    }
    ids = set()
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
            match = re.search(r"visualPrefab: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}", text)
            check(bool(match), f"Missing prefab: {expected_id}")
            if match:
                source = ROOT / "Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/Prefabs" / (monster["prefabKey"] + ".prefab")
                check(source.exists(), f"Missing prefab path: {source}")
                if source.exists():
                    source_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(source) + ".meta").read_text(), re.MULTILINE)[1]
                    if index in death_variants:
                        species = death_variants[index]
                        variant = BASE / "Combat/Prefabs/Monsters/Visuals" / (source.stem + "_Combat.prefab")
                        controller = variant.with_name(species + "_DeathOnce.overrideController")
                        clip = BASE / "Combat/Art/MonsterDeath" / species / "Dead.anim"
                        for asset in (variant, controller, clip):
                            check(asset.exists(), f"Missing death asset: {asset}")
                        if all(asset.exists() for asset in (variant, controller, clip)):
                            variant_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(variant) + ".meta").read_text(), re.MULTILINE)[1]
                            controller_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(controller) + ".meta").read_text(), re.MULTILINE)[1]
                            clip_guid = re.search(r"^guid: ([0-9a-f]+)$", Path(str(clip) + ".meta").read_text(), re.MULTILINE)[1]
                            check(match[2] == variant_guid, f"Death variant GUID mismatch: {expected_id}")
                            check(f"m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}" in variant.read_text(),
                                  f"Death variant source mismatch: {expected_id}")
                            check(f"objectReference: {{fileID: 22100000, guid: {controller_guid}, type: 2}}" in variant.read_text(),
                                  f"Death controller missing from variant: {expected_id}")
                            check(f"m_OverrideClip: {{fileID: 7400000, guid: {clip_guid}, type: 2}}" in controller.read_text(),
                                  f"Nonlooping death clip missing from controller: {expected_id}")
                            check("m_LoopTime: 0" in clip.read_text(), f"Death clip still loops: {expected_id}")
                    else:
                        check(match[2] == source_guid, f"Prefab GUID mismatch: {expected_id}")
                        check(f"--- !u!1 &{match[1]}\n" in source.read_text(), f"Prefab root fileID missing: {expected_id}")
    check(known_currencies == set(gems), "Some currencies have no attainable monster source")
    for skill in data["skills"]:
        check(skill["currencyId"] in known_currencies, "Unattainable skill currency")
        check(skill["id"] in (
            "skill.tornado", "skill.headshot", "skill.thunder",
            "skill.flame_exhaust", "skill.magic_ball", "skill.laser",
            "skill.pet_baby_dragon"),
              "Unexpected purchased skill category")
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
              "propertyPath: cursorAnchor" in pet_scene and
              "objectReference: {fileID: 1460028808}" in pet_scene,
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
    print("PASS: JSON sections, 10 monsters, seven skill IDs, 20 combat relic definitions, currency reachability, prefab IDs, TMP, scene and render settings" + (", C# syntax" if args.syntax else ""))
    print("Unity import, semantic compilation and gameplay were NOT run.")

if __name__ == "__main__":
    main()
