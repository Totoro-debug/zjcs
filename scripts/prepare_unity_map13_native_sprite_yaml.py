"""Serialize source-native Sprite fields without altering geometry or bindings."""
import yaml


class Dumper(yaml.SafeDumper):
    pass


def mapping(dumper, value):
    return dumper.represent_mapping("tag:yaml.org,2002:map", value.items(),
        flow_style=bool(value) and all(not isinstance(v, (dict, list)) for v in value.values()))


def source_header():
    return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject={"fileID": 0},
        m_PrefabInstance={"fileID": 0}, m_PrefabAsset={"fileID": 0})


def fields(value):
    if isinstance(value, dict):
        if set(value) == {"m_FileID", "m_PathID"}:
            if value["m_PathID"]:
                raise ValueError("Original reward pointer needs an explicit binding")
            return {"fileID": 0}
        return {key: fields(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [fields(item) for item in value]
    if isinstance(value, bool):
        return int(value)
    return value


Dumper.add_representer(dict, mapping)
