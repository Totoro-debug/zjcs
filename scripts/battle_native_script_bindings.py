"""Reuse the canonical native graph converter's static script registrations."""
import ast
from pathlib import Path

import prepare_battle_skill_graph_assets as canonical
from battle_skill_entity_prefabs import skill_entity_scripts


def shared_bindings():
    # Keep historical conversion receipts intact. Read the existing declarative
    # registrations without executing its complete historical asset conversion.
    tree = ast.parse(Path(canonical.__file__).read_text(encoding='utf-8-sig'))
    main = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'main')
    factories = dict(hit_entity_scripts=canonical.hit_entity_scripts, skill_entity_scripts=skill_entity_scripts,
                     action_entity_scripts=canonical.action_entity_scripts, damage_entity_scripts=canonical.damage_entity_scripts)

    def value(node):
        if isinstance(node, ast.Constant):
            return node.value
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.USub, ast.UAdd)):
            return ast.literal_eval(node)
        if isinstance(node, (ast.List, ast.Tuple)):
            result = [value(child) for child in node.elts]
            return tuple(result) if isinstance(node, ast.Tuple) else result
        if isinstance(node, ast.Name) and node.id == 'EVENT_ASSET':
            return canonical.EVENT_ASSET
        if isinstance(node, ast.Dict):
            result = {}
            for key, child in zip(node.keys, node.values):
                if key is None:
                    result.update(value(child))
                else:
                    result[value(key)] = value(child)
            return result
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in factories:
            assert not node.args and not node.keywords, 'Native script factory arguments changed'
            return factories[node.func.id]()
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == 'dict':
            assert not node.args and all(keyword.arg is not None for keyword in node.keywords)
            return {keyword.arg: value(keyword.value) for keyword in node.keywords}
        raise ValueError('Review a changed canonical native script registration: ' + ast.dump(node))

    mapped = dict(canonical.SCRIPTS)
    for statement in main.body:
        call = statement.value if isinstance(statement, ast.Expr) else None
        if isinstance(call, ast.Call) and isinstance(call.func, ast.Attribute) and call.func.attr == 'update' \
                and isinstance(call.func.value, ast.Name) and call.func.value.id == 'mapped':
            assert len(call.args) == 1 and not call.keywords
            mapped.update(value(call.args[0]))
    calls = [node for node in ast.walk(main) if isinstance(node, ast.Call)
             and isinstance(node.func, ast.Name) and node.func.id == 'build']
    assert len(calls) == 1, 'Canonical native graph conversion entry changed'
    selected = {keyword.arg: value(keyword.value) for keyword in calls[0].keywords
                if keyword.arg in ('reference_scripts', 'external_scripts', 'source_scripts')}
    assert len(selected) == 3, 'Canonical native script registries are incomplete'
    return dict(MAPPED=mapped, **selected)
