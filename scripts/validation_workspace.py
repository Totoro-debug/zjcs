"""Validation outputs under output/; original inputs and tools stay in .tools."""
import os
from pathlib import Path
import tempfile


def validation_root():
    root = Path(__file__).resolve().parents[1] / "output/validation"
    root.mkdir(parents=True, exist_ok=True)
    if root.is_symlink() or root.is_junction():
        raise ValueError("Validation root must not be a link")
    return root.resolve()


def validation_directory():
    """Use the calling session, or the repository validation root."""
    root = validation_root()
    configured = os.environ.get("ZJCS_VALIDATION_DIR")
    if not configured:
        return root
    directory = Path(configured).resolve()
    if directory == root or not directory.is_relative_to(root):
        raise ValueError("ZJCS_VALIDATION_DIR must be a session inside " + str(root))
    if not directory.is_dir():
        raise ValueError("The configured validation session does not exist")
    return directory


def temporary_validation_directory(prefix):
    return tempfile.TemporaryDirectory(prefix=prefix, dir=validation_directory())


def require_temporary_unity_project(project):
    project = project.resolve()
    root = validation_directory()
    if project == root or not project.is_relative_to(root):
        raise ValueError("Use an isolated Unity project inside " + str(root))
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError("The isolated Unity project is missing")
    return project
