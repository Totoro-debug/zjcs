"""Normalize extracted Home UI animation curves for Unity JsonUtility."""

from __future__ import annotations


def normalize_home_ui_animations(animations):
    if animations is None:
        return None
    if not isinstance(animations, list):
        raise ValueError("Home UI animations must be a list.")

    normalized = []
    for animation in animations:
        if not isinstance(animation, dict):
            raise ValueError("Home UI animation entries must be objects.")
        result = dict(animation)
        tracks = []
        for track in animation.get("tracks") or []:
            if not isinstance(track, dict):
                raise ValueError("Home UI animation tracks must be objects.")
            normalized_track = dict(track)
            keys = []
            for key in track.get("keys") or []:
                if isinstance(key, (list, tuple)):
                    if len(key) != 4:
                        raise ValueError("Home UI animation keys must have four curve values.")
                    keys.append({
                        "time": key[0],
                        "value": key[1],
                        "in_slope": 0.0 if key[2] is None else key[2],
                        "out_slope": 0.0 if key[3] is None else key[3],
                        "in_step": key[2] is None,
                        "out_step": key[3] is None,
                    })
                elif isinstance(key, dict):
                    keys.append(dict(key))
                else:
                    raise ValueError("Home UI animation keys must be curve arrays or objects.")
            normalized_track["keys"] = keys
            tracks.append(normalized_track)
        result["tracks"] = tracks
        normalized.append(result)
    return normalized
