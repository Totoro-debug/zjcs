"""Prevent diagnostic adaptation from removing original recovery code."""
import re
import unittest
from prepare_unity_story_playable_runtime import CODE, OUTPUT, logging_adapter

class PlayableSourceAdaptation(unittest.TestCase):
    def test_numbered_exception_does_not_consume_following_methods(self):
        source = (CODE / "gameframework-csharp9-source/GameFramework/MyPlayable.cs").read_text(encoding="utf8")
        adapted, count = logging_adapter(source)
        self.assertGreater(count, 0)
        self.assertIn("Debug.LogException(exception2);", adapted)
        self.assertIn("protected virtual void StopImpl()", adapted)
        self.assertIn("public void ForceStopOnError()", adapted)
        self.assertIn("myStatus = PlayableStatus.Stop;", adapted)
        self.assertIn("OnStop?.SafeInvoke(this);", adapted)
        # Every original non-diagnostic line remains in the same order.
        declaration = r"(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|sealed)\s+)+(?:[\w<>?,.\[\]]+\s+)(\w+)\s*\("
        self.assertEqual(re.findall(declaration, source), re.findall(declaration, adapted))

    def test_numbered_exception_stop_transition_is_present_in_generated_source(self):
        source = (OUTPUT / "MyPlayable.cs").read_text(encoding="utf8")
        self.assertIn("catch (Exception exception2)", source)
        self.assertIn("Debug.LogException(exception2);", source)
        self.assertIn("myStatus = PlayableStatus.Stop;", source)
        self.assertIn("ForceStopOnError()", source)

    def test_unrecognized_handler_is_rejected(self):
        source = "GLoggerErrorInterpolatedStringHandler message = MakeHandler();\n"
        with self.assertRaisesRegex(ValueError, "Unresolved original logging"):
            logging_adapter(source)

if __name__ == "__main__":
    unittest.main()
