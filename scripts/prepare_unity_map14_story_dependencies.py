"""Recover current Dragon Country public MapTalk graphs and follower closure."""
from prepare_unity_map13_story_dependencies import build as build_current_dependencies


def build():
    return build_current_dependencies(14)


if __name__ == "__main__":
    build()
