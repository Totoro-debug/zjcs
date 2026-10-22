"""Read the pinned property-row binding fields from the original UI prefabs.

The character property dialog stores its catalog groups and row templates as
prefab references that are not part of the serialized scene tree.  This module
reads those fields straight from the pinned APK bundles with the managed
schemas so Unity binds the same catalogs, slots and row prefabs.
"""
import json

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr


def transform_path(transform):
    names = []
    while True:
        names.insert(0, transform.m_GameObject.read().m_Name)
        if not transform.m_Father.path_id:
            break
        transform = transform.m_Father.read()
    return '/' + '/'.join(names)


def node_path(game_object):
    transform = next(c.component.read() for c in game_object.m_Component
                     if c.component.type.name in ('RectTransform', 'Transform'))
    return transform_path(transform)


class BindingReader:
    def __init__(self):
        self.apk = Apk()
        self.schemas = ScriptSchemas()
        self.inputs = {}

    def object(self, bundle, path_id):
        identity = int(path_id)
        return next(obj for obj in self.apk.objects(bundle) if obj.path_id == identity)

    def convert(self, value, owner):
        if isinstance(value, list):
            return [self.convert(item, owner) for item in value]
        if not isinstance(value, dict):
            return value
        if 'm_PathID' in value:
            if not value['m_PathID']:
                return None
            ptr = PPtr(**value, assetsfile=owner.assets_file)
            obj = ptr.deref()
            if obj.type.name == 'GameObject':
                return node_path(obj.read())
            read = obj.read(check_read=False)
            game_object = getattr(read, 'm_GameObject', None)
            if game_object is not None and game_object.path_id:
                return node_path(game_object.read())
            return transform_path(read)
        return {key: self.convert(item, owner) for key, item in value.items()}

    def component_fields(self, bundle, path_id, class_names):
        game_object = self.object(bundle, path_id)
        result = {}
        for node in self.walk(game_object.read()):
            for entry in node.m_Component:
                component = entry.component.deref()
                if component.type.name != 'MonoBehaviour':
                    continue
                script = component.read(check_read=False).m_Script.read()
                name = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
                if name not in class_names:
                    continue
                raw = self.apk.provenance(component)
                self.inputs[component.assets_file.name + ':' + str(component.path_id)] = dict(
                    raw, type=component.type.name, script=name)
                fields = self.schemas.read(component)
                result[name] = {key: self.convert(value, component) for key, value in fields.items()
                                if key not in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name')}
        missing = set(class_names) - result.keys()
        if missing:
            raise ValueError('Original property binding components are missing: ' + ', '.join(sorted(missing)))
        return result

    def walk(self, game_object):
        yield game_object
        transform = next((c.component.read() for c in game_object.m_Component
                          if c.component.type.name in ('RectTransform', 'Transform')), None)
        if transform is None:
            return
        for child in transform.m_Children:
            yield from self.walk(child.read().m_GameObject.read())

    def read(self, dialog_source, dialog_classes, group_source, group_classes, rank_source=None,
             rank_classes=()):
        dialog = self.component_fields(dialog_source['bundle'], dialog_source['path_id'], dialog_classes)
        group = self.component_fields(group_source['bundle'], group_source['path_id'], group_classes)
        result = {
            'manifest': '854_156383',
            'inputs': dict(sorted(self.inputs.items())),
            'dialog': dialog,
            'group': group,
        }
        if rank_source is not None and rank_classes:
            result['rank'] = self.component_fields(rank_source['bundle'], rank_source['path_id'],
                                                   tuple(rank_classes))
            result['inputs'] = dict(sorted(self.inputs.items()))
        return result
