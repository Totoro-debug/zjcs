"""Exercise real temporary cleanup and rejection of persistent Unity projects."""
import os
from pathlib import Path
import unittest
from unittest.mock import patch
from validation_workspace import (
    require_temporary_unity_project, temporary_validation_directory, validation_root,
)


class ValidationWorkspaceTests(unittest.TestCase):
    def test_cleanup_on_failure(self):
        with patch.dict(os.environ, {}, clear=False):
            os.environ.pop("ZJCS_VALIDATION_DIR", None)
            with self.assertRaisesRegex(RuntimeError, "probe failure"):
                with temporary_validation_directory("cleanup-test-") as temporary:
                    work = Path(temporary)
                    (work / "Player.exe").write_text("fixture")
                    self.assertTrue(work.is_relative_to(validation_root()))
                    raise RuntimeError("probe failure")
            self.assertFalse(work.exists())

    def test_session_and_isolated_project(self):
        with temporary_validation_directory("session-test-") as temporary:
            work = Path(temporary)
            with patch.dict(os.environ, {"ZJCS_VALIDATION_DIR": str(work)}):
                with temporary_validation_directory("compiler-") as compiler:
                    self.assertEqual(Path(compiler).parent, work)
                project = work / "unity"
                settings = project / "ProjectSettings"
                settings.mkdir(parents=True)
                (settings / "ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.57f1c2")
                self.assertEqual(require_temporary_unity_project(project), project.resolve())
                with self.assertRaises(ValueError):
                    require_temporary_unity_project(Path(__file__).resolve().parents[1] / "unity")

    def test_refuses_persistent_output_override(self):
        repository = Path(__file__).resolve().parents[1]
        with patch.dict(os.environ, {"ZJCS_VALIDATION_DIR": str(repository / ".tools")}):
            with self.assertRaises(ValueError):
                temporary_validation_directory("unsafe-")


if __name__ == "__main__":
    unittest.main()
