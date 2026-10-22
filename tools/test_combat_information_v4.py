"""Offline data-contract tests. Does not launch Unity or touch PlayerPrefs."""
import copy
import json
import unittest
from validate_bilingual_game_data import check_schema, load_runtime, validate_documents, ROOT


class CombatInformationV4Tests(unittest.TestCase):
    def setUp(self):
        self.info = load_runtime()
        self.schema = json.loads((ROOT / "docs/collaboration/game-information-v4.schema.json").read_text())

    def test_complete_pipeline_static_contract(self):
        document = validate_documents()
        self.assertEqual(len(document["progression"]["categories"]), 23)
        self.assertEqual(sum(len(c["nodes"]) for c in document["progression"]["categories"]), 90)

    def test_only_resolved_combat_sections_remain(self):
        self.assertEqual(set(self.info), {
            "schemaVersion", "balanceVersion", "stats", "rules", "gemstones", "monsters",
            "skills", "relics", "relicDropRules"})
        self.assertEqual(self.info["schemaVersion"], 4)
        self.assertEqual(len(self.info["relics"]), 20)
        self.assertEqual(len(self.info["monsters"]), 10)
        self.assertEqual([s["id"] for s in self.info["skills"]], [
            "skill.tornado", "skill.headshot", "skill.thunder",
            "skill.flame_exhaust", "skill.magic_ball", "skill.laser",
            "skill.pet_baby_dragon"])
        for old in ("fragment", "progression", "entities", "fieldHelp", "implementation"):
            self.assertNotIn(old, json.dumps(self.info))

    def test_reserved_enum_codes_reject_invalid_values(self):
        for value in (-1, 1, 99, "None", None, True, 0.5):
            with self.subTest(value=value):
                changed = copy.deepcopy(self.info)
                changed["monsters"][0]["behaviorType"] = value
                with self.assertRaises(AssertionError):
                    check_schema(changed, self.schema, self.schema)

    def test_behavior_field_is_required_on_every_monster(self):
        for index in range(10):
            changed = copy.deepcopy(self.info)
            del changed["monsters"][index]["behaviorType"]
            with self.assertRaises(AssertionError):
                check_schema(changed, self.schema, self.schema)

    def test_relics_are_combat_input_but_growth_settings_are_not(self):
        self.assertEqual(self.info["relicDropRules"]["maxPerKill"], 1)
        changed = copy.deepcopy(self.info)
        del changed["relics"]
        with self.assertRaises(AssertionError):
            check_schema(changed, self.schema, self.schema)
        for key, value in (("progression", {}), ("entities", [])):
            changed = dict(self.info, **{key: value})
            with self.assertRaises(AssertionError):
                check_schema(changed, self.schema, self.schema)
        changed = copy.deepcopy(self.info)
        changed["monsters"][0]["relicFragmentId"] = "fragment.relic.monster.01"
        with self.assertRaises(AssertionError):
            check_schema(changed, self.schema, self.schema)

    def test_full_int64_and_unlocked_false_balances_are_preserved(self):
        changed = copy.deepcopy(self.info)
        changed["gemstones"][1].update(enabled=False, amount=9223372036854775807)
        check_schema(changed, self.schema, self.schema)
        self.assertEqual(json.loads(json.dumps(changed)), changed)

    def test_normal_field_cap_and_schema_version(self):
        for key, value in (("normalFieldDurationSeconds", 31), ("attackCooldownSeconds", 0)):
            changed = copy.deepcopy(self.info)
            changed["stats"][key] = value
            with self.assertRaises(AssertionError):
                check_schema(changed, self.schema, self.schema)
        changed = dict(self.info, schemaVersion=2)
        with self.assertRaises(AssertionError):
            check_schema(changed, self.schema, self.schema)

    def test_relic_contract_is_present_without_fragment_currency_path(self):
        base = ROOT / "Assets/CursorHunter"
        information = (base / "Contracts/Runtime/GameInformation.cs").read_text()
        snapshot = (base / "Contracts/Runtime/ProgressionCombatSnapshot.cs").read_text()
        self.assertIn("RelicInformation", information)
        self.assertIn("RelicCombatSnapshot", snapshot)
        self.assertIn("RelicDropRules", snapshot)
        for path in base.rglob("*.cs"):
            if "Tests" in path.parts: continue
            source = path.read_text()
            for retired in ("LootFragmentCurrencyId", "_fragmentBalances", "GrantLootFragments"):
                self.assertNotIn(retired, source, str(path))
        for relative in ("Contracts/Runtime/GameInformation.cs", "Contracts/Runtime/ProgressionCombatSnapshot.cs",
                         "Contracts/Runtime/SpawnSnapshot.cs", "Combat/Runtime/MonsterCombatTarget.cs"):
            self.assertIn("MonsterBehaviorType", (base / relative).read_text())
        self.assertIn("authored.BehaviorType", (base / "App/Runtime/HuntManager.cs").read_text())


if __name__ == "__main__":
    unittest.main()
