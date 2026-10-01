"""Read-only checks for the v3 JSON handoff. Never opens Unity or runs the game."""
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
        return [baseline if n["operation"] == "baseline" else n["value"] for n in categories[category]["nodes"]]
    data = {
        "schemaVersion": example["schemaVersion"],
        "normalFieldDurationSeconds": values("stat.fieldDuration", example["stats"]["normalFieldDurationSeconds"]),
        "attackCooldownSeconds": values("stat.cooldown", example["stats"]["attackCooldownSeconds"]),
        "monsters": [dict(m, prefabKey=entities[m["id"]]["prefabKey"],
                          unlockCurrencyId=categories[m["id"]]["nodes"][0]["currencyId"]) for m in example["monsters"]],
        "skills": [dict(s, currencyId=categories[s["id"]]["nodes"][0]["currencyId"],
                        damageCoefficient=s["damage"]) for s in example["skills"]]
    }
    errors = []
    def check(ok, message):
        if not ok: errors.append(message)
    def finite_positive(value):
        return isinstance(value, (int, float)) and math.isfinite(value) and value > 0

    check(data["schemaVersion"] == example["schemaVersion"] == 3, "Schema version must be 3")
    check(set(example) == {"schemaVersion", "balanceVersion", "stats", "rules", "gemstones", "monsters", "skills"}, "Unexpected JSON sections")
    gems = ["gem." + name for name in ["garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon"]]
    check([g["id"] for g in example["gemstones"]] == gems, "Gemstone IDs/order changed")
    check(len(data["monsters"]) == len(example["monsters"]) == 10, "Expected 10 monsters")
    check(len(data["skills"]) == len(example["skills"]) == 7, "Expected 7 skills including cursor aura")
    check(data["normalFieldDurationSeconds"] == [15, 20, 25, 30], "Normal duration must stop at 30")
    check(all(finite_positive(v) and v >= .05 for v in data["attackCooldownSeconds"]), "Invalid cooldown")
    check(all(data["attackCooldownSeconds"][i] > data["attackCooldownSeconds"][i+1] for i in range(4)), "Cooldown upgrades must decrease")
    known_currencies = {"gem.garnet"}
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
            match = re.search(r"prefab: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}", text)
            check(bool(match), f"Missing prefab: {expected_id}")
            if match:
                prefab = ROOT / "Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/Prefabs" / (monster["prefabKey"] + ".prefab")
                check(prefab.exists(), f"Missing prefab path: {prefab}")
                if prefab.exists():
                    check(match[2] in Path(str(prefab) + ".meta").read_text(), "Prefab GUID mismatch")
                    check(f"--- !u!1 &{match[1]}\n" in prefab.read_text(), "Prefab root fileID missing")
    check(known_currencies == set(gems), "Some currencies have no attainable monster source")
    for skill in data["skills"]:
        check(skill["currencyId"] in known_currencies, "Unattainable skill currency")
        check(all(finite_positive(skill[key]) for key in ["damageCoefficient", "radiusWorldUnits", "cooldownSeconds"]), "Invalid skill numeric data")

    controller = (BASE / "Progression/Runtime/TraitScreenController.cs").read_text()
    check("CreateGameInformationJson" in controller and "SourceJson" in controller, "Summary must use captured JSON")
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
    print("PASS: JSON sections, 10 monsters, 7 skills, currency reachability, prefab IDs, TMP, scene and render settings" + (", C# syntax" if args.syntax else ""))
    print("Unity import, semantic compilation and gameplay were NOT run.")

if __name__ == "__main__":
    main()
