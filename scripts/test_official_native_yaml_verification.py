"""Regression checks for Unity names and original packed scalar bytes."""
import unittest
import yaml
from official_native_yaml_verification import documents, unpack_array, OriginalYamlInteger


class NativeYamlVerificationTests(unittest.TestCase):
    def test_original_unquoted_names_and_numeric_booleans(self):
        for name in ("Off", "On", "OFF", "on", "true", "False", "yes", "No"):
            value = documents("--- !u!1 &7\nGameObject:\n  m_Name: " + name + "\n  m_IsActive: 1\n")[7][1]
            self.assertEqual(value["m_Name"], name)
            self.assertIsInstance(value["m_Name"], str)
            self.assertEqual(value["m_IsActive"], 1)
        # The adapter is local to native assets; unrelated YAML parsing is intact.
        self.assertIs(yaml.safe_load("name: Off")["name"], False)

    def test_packed_array_uses_original_digits(self):
        value = documents("--- !u!114 &8\nMonoBehaviour:\n  states: 0100000002000000\n")[8][1]["states"]
        self.assertIsInstance(value, OriginalYamlInteger)
        self.assertEqual(value.scalar, "0100000002000000")
        shape = {"type": "vector", "children": {"Array": {"children": {"data": {"type": "int"}}}}}
        self.assertEqual(unpack_array(value, shape, 2), [1, 2])


if __name__ == "__main__":
    unittest.main()
