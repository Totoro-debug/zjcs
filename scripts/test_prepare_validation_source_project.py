import json
from pathlib import Path
import tempfile
import unittest
from uuid import uuid4
from unittest.mock import patch

from prepare_validation_source_project import prepare
from validation_workspace import validation_root


class SourceProjectTests(unittest.TestCase):
    def test_copy_includes_current_files_and_refuses_overwrite(self):
        with tempfile.TemporaryDirectory(prefix='source-project-' + uuid4().hex + '-', dir=validation_root()) as temp:
            root = Path(temp)
            repository = root / 'repository'
            source = repository / 'unity'
            for name in ('Assets', 'Packages', 'ProjectSettings'):
                (source / name).mkdir(parents=True)
            (source / 'ProjectSettings/ProjectVersion.txt').write_text('version')
            (source / 'Assets/current.cs').write_text('uncommitted source')
            (source / 'Packages/manifest.json').write_text('{}')
            (source / 'Library').mkdir()
            workspace = root / 'session'
            workspace.mkdir()
            (workspace / '.zjcs-validation.json').write_text(json.dumps(
                {'format': 1, 'repository': str(repository)}))
            with patch('prepare_validation_source_project.validation_root', return_value=root):
                project = prepare(workspace, source, repository)
                self.assertEqual((project / 'Assets/current.cs').read_text(), 'uncommitted source')
                self.assertFalse((project / 'Library').exists())
                (source / 'Assets/current.cs').write_text('changed')
                self.assertEqual((project / 'Assets/current.cs').read_text(), 'uncommitted source')
                with self.assertRaises(FileExistsError):
                    prepare(workspace, source, repository)

    def test_rejects_foreign_workspace(self):
        with tempfile.TemporaryDirectory(prefix='source-project-' + uuid4().hex + '-', dir=validation_root()) as temp:
            root = Path(temp)
            workspace = root / 'session'
            workspace.mkdir()
            (workspace / '.zjcs-validation.json').write_text(json.dumps(
                {'format': 1, 'repository': str(root / 'other')}))
            with patch('prepare_validation_source_project.validation_root', return_value=root):
                with self.assertRaisesRegex(ValueError, 'another repository'):
                    prepare(workspace, root / 'source', root / 'repository')


if __name__ == '__main__':
    unittest.main()
