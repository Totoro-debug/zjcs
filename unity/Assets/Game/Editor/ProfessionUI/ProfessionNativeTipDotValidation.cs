using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionNativeTipDotValidation
    {
        private const string Probe = "Assets/ProfessionTipDotGraphProbe";
        [Serializable] private sealed class Source { public string manifest; public Graph[] graphs; public Node[] nodes; }
        [Serializable] private sealed class Graph { public string path, name; public int type, scope; public string[] nodes; }
        [Serializable] private sealed class Connection { public string field, node; }
        [Serializable] private sealed class Port
        {
            public string key, field, owner, type;
            public int direction, connectionType, typeConstraint, flowType;
            public bool dynamic;
            public Connection[] connections;
        }
        [Serializable] private sealed class Managed
        {
            public TipDotNodeKey[] Parents, Childs;
            public TipDotNodeKey NodeKey;
            public TipDotStateInfo TipDotInfo;
            public bool IsShowTip, IsAll;
            public int Profession;
            public int[] Professions;
            public AchievementTypeAndParamItem[] AchievementTypeAndParamList;
        }
        [Serializable] private sealed class Node
        {
            public string path, graph, name, mono, desc, nodeName, selectedType, managedType;
            public int nodeId, id;
            public bool monoShow, ban;
            public Vector2 position;
            public TipDotNodePresetData preset;
            public Port[] ports;
            public Managed managedData;
        }

        private static void Require(bool value, string description)
        {
            if (!value) throw new InvalidOperationException("Original profession graph differs: " + description);
        }
        private static void Text(string actual, string expected, string description) =>
            Require((actual ?? "") == (expected ?? ""), description);
        private static void Reference(SerializedProperty property, string expected, string description) =>
            Text(AssetDatabase.GetAssetPath(property.objectReferenceValue), expected, description);

        public static void Run()
        {
            string fixture = null;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-professionTipDotFixture") fixture = args[i + 1];
            if (fixture == null) throw new InvalidOperationException("Native profession graph fixture is missing.");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(fixture));
            Require(source.manifest == "871_156598" && source.graphs.Length == 2 && source.nodes.Length == 16, "fixture scope");
            try
            {
                ProfessionTipDotValidation.Run();
                foreach (var expected in source.graphs) VerifyGraph(expected);
                foreach (var expected in source.nodes) VerifyNode(expected);
                Debug.Log("Profession native tip-dot import passed: 2 original graphs, 16 original nodes, all 6 managed node types, original node fields/presets/condition data and every serialized port/connection. Runtime local player/controller hookup and UI rendering remain separate.");
            }
            finally { AssetDatabase.DeleteAsset(Probe); }
        }

        private static void VerifyGraph(Graph expected)
        {
            var graph = AssetDatabase.LoadAssetAtPath<TipDotNodeGraph>(expected.path);
            Require(graph != null, expected.path + " graph import");
            Text(graph.name, expected.name, "graph name");
            Require((int)graph.Type == expected.type && (int)graph.Scope == expected.scope, "graph type/scope");
            Require(graph.nodes.Count == expected.nodes.Length, "graph node count");
            for (var i = 0; i < expected.nodes.Length; i++)
                Text(AssetDatabase.GetAssetPath(graph.nodes[i]), expected.nodes[i], "original graph node order");
        }

        private static void VerifyNode(Node expected)
        {
            var node = AssetDatabase.LoadAssetAtPath<TipDotGraphNode>(expected.path);
            Require(node != null, expected.path + " node import");
            Text(node.name, expected.name, "node name");
            Text(AssetDatabase.GetAssetPath(node.graph), expected.graph, "node graph pointer");
            Text(node.Mono, expected.mono, "node Mono");
            Text(node.NodeDesc, expected.desc, "node description");
            Text(node.NodeName, expected.nodeName, "node name field");
            Text(node.SelectedType, expected.selectedType, "node selected type");
            Require(node.NodeId == expected.nodeId && node.TipDotNodeId == expected.id && node.IsBan == expected.ban && node.IsMonoShow == expected.monoShow && node.position == expected.position, "native node metadata");
            var preset = node.PresetData;
            Require(preset.Priority == expected.preset.Priority && preset.PresetTipLevel == expected.preset.PresetTipLevel &&
                preset.NeedAllShow == expected.preset.NeedAllShow && preset.IsRoot == expected.preset.IsRoot && preset.IsReverse == expected.preset.IsReverse, "node preset");
            var levels = preset.IgnoreLevels;
            var expectedLevels = expected.preset.IgnoreLevels;
            Require((levels == null ? 0 : levels.Count) == (expectedLevels == null ? 0 : expectedLevels.Count), "ignore-level count");
            if (levels != null) for (var i = 0; i < levels.Count; i++) Require(levels[i] == expectedLevels[i], "ignore-level order");
            Require(node.RuntimeTipDotNode == null && node.TipDotNode != null, "native managed-reference import");
            Text(node.TipDotNode.GetType().Name, expected.managedType, "managed-reference class");
            var clone = node.TipDotNode.Clone();
            Require(clone != null && !ReferenceEquals(clone, node.TipDotNode) && clone.GetType() == node.TipDotNode.GetType(), "managed-node clone");
            var data = expected.managedData;
            Text(node.TipDotNode.NodeKey.GraphName, data.NodeKey.GraphName, "managed graph name");
            Text(node.TipDotNode.NodeKey.NodeName, data.NodeKey.NodeName, "managed node key");
            Require(node.TipDotNode.IsShowTip == data.IsShowTip && node.TipDotNode.TipDotInfo.TipLevel == data.TipDotInfo.TipLevel, "managed initial state");
            Text(node.TipDotNode.TipDotInfo.Data, data.TipDotInfo.Data, "managed initial data");
            Require((node.TipDotNode.Parents?.Count ?? 0) == (data.Parents?.Length ?? 0) &&
                (node.TipDotNode.Childs?.Count ?? 0) == (data.Childs?.Length ?? 0), "managed initial relationships");
            if (node.TipDotNode is TipDotProfessionCanUpgradeConditionNode upgrade)
            {
                Require(upgrade.IsAll == data.IsAll && (upgrade.Professions?.Count ?? 0) == data.Professions.Length, "upgrade condition configuration: " + expected.path + " actual all=" + upgrade.IsAll + " list=" + string.Join(",", upgrade.Professions) + " expected all=" + data.IsAll + " list=" + string.Join(",", data.Professions));
                for (var i = 0; i < data.Professions.Length; i++) Require(upgrade.Professions[i] == data.Professions[i], "upgrade profession order");
                var copied = (TipDotProfessionCanUpgradeConditionNode)clone;
                Require(!ReferenceEquals(upgrade.Professions, copied.Professions), "upgrade clone list ownership");
            }
            if (node.TipDotNode is TipDotProfessionPromotionCondtionNode promotion)
                Require(promotion.Profession == data.Profession, "promotion profession");
            if (node.TipDotNode is TipDotAchievementConditionNode achievement)
            {
                Require(achievement.AchievementTypeAndParamList.Count == data.AchievementTypeAndParamList.Length, "achievement condition list");
                for (var i = 0; i < data.AchievementTypeAndParamList.Length; i++)
                {
                    Require(achievement.AchievementTypeAndParamList[i].AchievementType == data.AchievementTypeAndParamList[i].AchievementType, "achievement type");
                    Text(achievement.AchievementTypeAndParamList[i].JsonParam, data.AchievementTypeAndParamList[i].JsonParam, "achievement parameter");
                }
                var copied = (TipDotAchievementConditionNode)clone;
                Require(!ReferenceEquals(achievement.AchievementTypeAndParamList, copied.AchievementTypeAndParamList) &&
                    !ReferenceEquals(achievement.AchievementTypeAndParamList[0], copied.AchievementTypeAndParamList[0]), "achievement clone ownership");
            }
            var serialized = new SerializedObject(node);
            var ports = serialized.FindProperty("ports");
            var keys = ports.FindPropertyRelative("keys");
            var values = ports.FindPropertyRelative("values");
            Require(keys.arraySize == expected.ports.Length && values.arraySize == expected.ports.Length, "native port count");
            for (var i = 0; i < expected.ports.Length; i++)
            {
                var port = expected.ports[i];
                var actual = values.GetArrayElementAtIndex(i);
                Text(keys.GetArrayElementAtIndex(i).stringValue, port.key, "port key order");
                Text(actual.FindPropertyRelative("_fieldName").stringValue, port.field, "port field");
                Text(actual.FindPropertyRelative("_typeQualifiedName").stringValue, port.type, "original serialized port value type");
                Reference(actual.FindPropertyRelative("_node"), port.owner, "port owner");
                Require(actual.FindPropertyRelative("_direction").intValue == port.direction &&
                    actual.FindPropertyRelative("_connectionType").intValue == port.connectionType &&
                    actual.FindPropertyRelative("_typeConstraint").intValue == port.typeConstraint &&
                    actual.FindPropertyRelative("_flowConnectionType").intValue == port.flowType &&
                    actual.FindPropertyRelative("_dynamic").boolValue == port.dynamic, "native port metadata");
                var connections = actual.FindPropertyRelative("connections");
                Require(connections.arraySize == port.connections.Length, "port connection count");
                var resolved = node.GetPort(port.key).GetConnections();
                Require(resolved.Count == port.connections.Length, "runtime port resolution count");
                for (var j = 0; j < port.connections.Length; j++)
                {
                    var connection = connections.GetArrayElementAtIndex(j);
                    Text(connection.FindPropertyRelative("fieldName").stringValue, port.connections[j].field, "connection field");
                    Reference(connection.FindPropertyRelative("node"), port.connections[j].node, "connection peer");
                    Text(resolved[j].fieldName, port.connections[j].field, "resolved field order");
                    Text(AssetDatabase.GetAssetPath(resolved[j].node), port.connections[j].node, "resolved peer order");
                }
            }
        }
    }
}
