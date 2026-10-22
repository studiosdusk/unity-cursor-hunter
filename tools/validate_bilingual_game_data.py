"""Validate English runtime data and the non-runtime Korean annotated reference."""
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets/CursorHunter/Data/Resources/GameData"

def load_runtime():
    return json.loads((DATA / "game-data.en.json").read_text())

def check_schema(value, schema, root, path="$"):
    """Checks the JSON Schema subset used by the two checked-in schemas."""
    if "$ref" in schema:
        reference = root
        assert schema["$ref"].startswith("#/")
        for part in schema["$ref"][2:].split("/"):
            reference = reference[part.replace("~1", "/").replace("~0", "~")]
        check_schema(value, reference, root, path)
        return
    kind = schema.get("type")
    if kind == "object":
        assert isinstance(value, dict), path
        assert set(schema.get("required", [])) <= set(value), f"Missing fields: {path}"
        if not schema.get("additionalProperties", True):
            assert set(value) <= set(schema.get("properties", {})), f"Unknown fields: {path}"
        for key, child in value.items():
            if key in schema.get("properties", {}):
                check_schema(child, schema["properties"][key], root, path + "." + key)
    elif kind == "array":
        assert isinstance(value, list), path
        assert len(value) >= schema.get("minItems", 0), path
        assert len(value) <= schema.get("maxItems", float("inf")), path
        for index, item in enumerate(value):
            check_schema(item, schema["items"], root, f"{path}[{index}]")
    elif kind in ("number", "integer"):
        assert type(value) in (int, float) and math.isfinite(value), path
        if kind == "integer": assert value == int(value), path
        assert value >= schema.get("minimum", float("-inf")), path
        assert value <= schema.get("maximum", float("inf")), path
        if "exclusiveMinimum" in schema: assert value > schema["exclusiveMinimum"], path
    elif kind == "string":
        assert isinstance(value, str) and len(value) >= schema.get("minLength", 0), path
        if "pattern" in schema: assert re.search(schema["pattern"], value), path
    elif kind == "boolean":
        assert type(value) is bool, path
    if "const" in schema: assert value == schema["const"], path
    if "enum" in schema: assert value in schema["enum"], path

def validate_documents():
    from build_korean_game_data_reference import validate_reference
    info = load_runtime()
    configuration = json.loads((DATA / "progression-config.en.json").read_text())
    validate_reference(info)
    schema = json.loads((ROOT / "docs/collaboration/game-data-document.schema.json").read_text())
    config_schema = json.loads((ROOT / "docs/collaboration/progression-config.schema.json").read_text())
    check_schema(info, schema, schema)
    check_schema(configuration, config_schema, config_schema)
    documents = [dict(configuration, information=info)]
    assert configuration["locale"] == "en"
    assert set(info) == {"schemaVersion", "balanceVersion", "stats", "rules", "gemstones", "monsters", "skills", "relics", "relicDropRules"}
    def no_retired_data(value):
        if isinstance(value, dict):
            for key, child in value.items():
                assert not any(word in key.lower() for word in ("fragment", "loot")), key
                no_retired_data(child)
        elif isinstance(value, list):
            for child in value: no_retired_data(child)
        elif isinstance(value, str):
            assert not value.startswith(("fragment.", "loot.")), value
    no_retired_data(info)
    no_retired_data(configuration)
    assert not (DATA / "game-data.ko.json").exists(), "Korean reference must not be a runtime resource"
    assert not (DATA / "active-data.json").exists(), "Runtime source must not be locale-selectable"
    loader = (ROOT / "Assets/CursorHunter/Data/Runtime/GameDataDocument.cs").read_text()
    assert 'RuntimeResourcePath = "GameData/game-data.en"' in loader
    assert 'Resources.Load<TextAsset>(RuntimeResourcePath)' in loader
    assert 'Resources.Load<TextAsset>(ProgressionResourcePath)' in loader
    assert 'ProgressionResourcePath = "GameData/progression-config.en"' in loader
    for script in (ROOT / "Assets/CursorHunter").rglob("*.cs"):
        text = script.read_text()
        assert "game-data.ko" not in text and "GameData/active-data" not in text, script
    schema = json.loads((ROOT / "docs/collaboration/game-data-document.schema.json").read_text())
    for document in documents:
        info, progression = document["information"], document["progression"]
        assert document["documentVersion"] == 2
        assert progression["savedProgressPolicy"] in ("overlay", "fileOnly")
        assert info["schemaVersion"] == 4 and info["balanceVersion"] >= 1
        assert len(info["gemstones"]) == 6 and len(info["monsters"]) == 10
        assert [skill["id"] for skill in info["skills"]] == [
            "skill.tornado", "skill.headshot", "skill.thunder", "skill.flame_exhaust",
            "skill.magic_ball", "skill.laser", "skill.pet_baby_dragon"]
        relic_ids = [
            "relic.red_potion", "relic.blue_potion", "relic.monster_meat",
            "relic.thief_shoes", "relic.archers_bow", "relic.warrior_armor",
            "relic.wizards_hat", "relic.valor_ring", "relic.lucky_dice",
            "relic.kraken_eye", "relic.four_leaf_clover", "relic.ancient_book",
            "relic.adventurers_bag", "relic.compass", "relic.megaphone",
            "relic.telescope", "relic.stone", "relic.rainbow_feather",
            "relic.scales_of_judgment", "relic.treasure_chest"]
        assert [relic["id"] for relic in info["relics"]] == relic_ids
        assert len(info["relics"]) == 20
        assert info["relicDropRules"] == {
            "maxPerKill": 1,
            "candidatePolicy": "unownedOnly",
            "resolution": "sumFixedChanceThenWeightedPick",
            "preserveBaseChanceAfterAcquisition": True,
        }
        drop_totals = {}
        for relic_index, relic in enumerate(info["relics"]):
            assert type(relic["owned"]) is bool
            assert relic["sourceMonsterId"] == f"monster.{relic_index // 2 + 1:02}"
            assert 0 < relic["dropChancePercent"] <= 100
            assert relic["effectId"] and math.isfinite(relic["effectValue"])
            assert 0 <= relic["effectValue"] <= 1000
            assert 0 <= relic["effectValue2"] <= 100
            drop_totals[relic["sourceMonsterId"]] = (
                drop_totals.get(relic["sourceMonsterId"], 0) + relic["dropChancePercent"])
        assert all(total <= 100 for total in drop_totals.values())
        stats, rules = info["stats"], info["rules"]
        assert 1 <= stats["attackPower"] <= 9223372036854775807
        assert stats["attackRadiusWorldUnits"] > 0 and stats["attackCooldownSeconds"] >= .05
        assert 0 <= stats["criticalChancePercent"] <= 100 and stats["bossDamageMultiplier"] > 0
        assert 15 <= stats["normalFieldDurationSeconds"] <= 30
        assert 1 <= rules["perMonsterAliveLimit"] <= rules["globalAliveLimit"] <= 80
        assert rules["criticalDamageMultiplier"] >= 1 and rules["bossFieldDurationSeconds"] > 0
        entities = {entry["id"]: entry for entry in document["entities"]}
        assert len(entities) == len(document["entities"])
        for entry in document["entities"]:
            assert entry["displayName"] and entry["description"]
        categories = {entry["id"]: entry for entry in progression["categories"]}
        assert len(categories) == len(progression["categories"]) == 23
        nodes = {n["id"]: n for c in progression["categories"] for n in c["nodes"]}
        assert len(nodes) == sum(len(c["nodes"]) for c in categories.values()) == 90
        skill_ids = [
            "skill.tornado", "skill.headshot", "skill.thunder",
            "skill.flame_exhaust", "skill.magic_ball", "skill.laser",
            "skill.pet_baby_dragon"]
        for skill_id in skill_ids:
            assert [node["id"] for node in categories[skill_id]["nodes"]] == [skill_id]
        currency = {g["id"] for g in info["gemstones"]}
        ordered_nodes = set()
        for c in categories.values():
            assert c["id"] in entities and c["tab"] in ("stats", "skills", "monsters")
            for n in c["nodes"]:
                assert not n["prerequisiteNodeId"] or n["prerequisiteNodeId"] in ordered_nodes, "Nodes must follow prerequisite order"
                ordered_nodes.add(n["id"])
                assert n["cost"] >= 0 and n["currencyId"] in currency
                assert n["operation"] in ("baseline", "set", "add", "enable", "tier")
                assert isinstance(n["startsUnlocked"], bool) and 0 <= n["requiredBossTier"] <= 5
                assert n["displayName"] and n["description"] and math.isfinite(n["value"])
                seen, current = {n["id"]}, n["prerequisiteNodeId"]
                while current:
                    assert current in nodes and current not in seen, f"Bad prerequisite: {n['id']}"
                    seen.add(current)
                    current = nodes[current]["prerequisiteNodeId"]
        for section in ("gemstones", "monsters", "skills"):
            for item in info[section]:
                assert item["id"] in entities and isinstance(item["enabled"], bool)
        for relic in info["relics"]:
            assert isinstance(relic["owned"], bool)
        for monster in info["monsters"]:
            assert monster["id"] in categories
            assert monster["hitPoints"] > 0 and monster["spawnIntervalSeconds"] >= .1
            assert 1 <= monster["productionCount"] <= 16
            assert monster["gemstoneId"] in currency and monster["gemstoneAmount"] >= 0
            assert monster["garnetReward"] >= 0
            assert 0 <= monster["gemstoneChancePercent"] <= 100
            assert type(monster["behaviorType"]) is int and monster["behaviorType"] == 0
            definition = ROOT / f"Assets/CursorHunter/Data/Resources/MonsterDefinitions/Monster{monster['id'][-2:]}.asset"
            assert "prefabKey: " + entities[monster["id"]]["prefabKey"] in definition.read_text()
        for skill in info["skills"]:
            assert skill["id"] in entities
            assert skill.get("upgrades", []) == []
        for gem in info["gemstones"]:
            assert 0 <= gem["amount"] <= 9223372036854775807
        assert len(progression["bossRewards"]) == 5
        for i, boss in enumerate(progression["bossRewards"], 1):
            assert boss["id"] == f"boss.v{i}" and boss["tier"] == i
            assert boss["gemstoneId"] in currency
    return documents[0]

def main():
    validate_documents()
    print("PASS: English-only runtime input, Korean detailed/key-only references, separate growth configuration, 23 categories / 90 nodes, seven purchasable skills, 20 relic definitions and fixed-drop rules, behaviorType=0, schema, references and rewards")
    print("No Unity or game execution.")

if __name__ == "__main__":
    main()
