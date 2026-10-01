"""Offline tests; no Unity, game, player save or runtime data writes."""
import json
import unittest

from build_korean_game_data_reference import (
    build_key_reference, decode_key_reference, validate_reference, ROOT, SOURCE,
)


class KoreanKeyReferenceTests(unittest.TestCase):
    def test_nested_keys_and_array_order(self):
        source = {"information": {"stats": {"attackPower": 10},
                  "skills": [{"id": "skill.fireball", "enabled": True}]}}
        result = build_key_reference(source)
        self.assertEqual(result["information(전투 기본 정보)"]["stats(스탯)"],
                         {"attackPower(공격력)": 10})
        self.assertEqual(decode_key_reference(result), source)
        self.assertEqual(source["information"]["stats"], {"attackPower": 10})

    def test_types_int64_empty_containers_and_null(self):
        source = {"amount": 9223372036854775807, "enabled": False,
                  "value": 0.125, "description": None, "id": "",
                  "skills": [], "information": {}}
        recovered = decode_key_reference(build_key_reference(source))
        self.assertEqual(json.dumps(recovered), json.dumps(source))
        self.assertIs(type(recovered["amount"]), int)
        self.assertIs(type(recovered["enabled"]), bool)

    def test_unknown_key_must_not_be_silently_omitted(self):
        with self.assertRaisesRegex(ValueError, "newField"):
            build_key_reference({"newField": 1})

    def test_complete_checked_in_references(self):
        validate_reference(json.loads((ROOT / SOURCE).read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
