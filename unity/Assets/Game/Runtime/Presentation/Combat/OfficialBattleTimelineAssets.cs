using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleTimelineData
    {
        public string manifest;
        public float sceneSize, z, deadTime, minDistance, moveDuration;
        public string anchor;
        public BattleTimelineCardBindings cardBindings;
        public BattleTimelineSceneBindings sceneBindings;
        public BattleTimelinePrefab[] prefabs;
        public BattleTimelineTexture[] textures;
        public BattleTimelineSprite[] sprites;
        public BattleTimelineMaterial[] materials;
        public BattleTimelineShader[] shaders;
    }
    [Serializable] public sealed class BattleTimelineCardBindings
    { public string faction, mainPlayerEffect, monster, monsterHurt, npcRoot, npc, playerRoot, player; }
    [Serializable] public sealed class BattleTimelineSceneBindings { public string line, slot; }
    [Serializable] public sealed class BattleTimelinePrefab
    {
        public string key, particlePrefab;
        public bool hasController;
        public BattleTimelineNode[] nodes;
        public BattleTimelineController controller;
    }
    [Serializable] public sealed class BattleTimelineNode
    {
        public string name, path;
        public int parent, layer;
        public bool active, hasRenderer, hasCircle;
        public float[] position, rotation, scale;
        public BattleTimelineRenderer renderer;
        public BattleTimelineCircle circle;
    }
    [Serializable] public sealed class BattleTimelineRenderer
    {
        public string kind, sprite;
        public string[] materials;
        public int order, drawMode, maskInteraction, spriteSortPoint;
        public bool enabled, flipX, flipY;
        public float[] color, size;
    }
    [Serializable] public sealed class BattleTimelineCircle
    {
        public float radius;
        public int segments;
        public float[] offset, color;
        public string sprite;
    }
    [Serializable] public sealed class BattleTimelineController
    {
        public string name;
        public int defaultState;
        public BattleTimelineParameter[] parameters;
        public BattleFunctionEntryClip[] clips;
        public BattleFunctionEntryState[] states;
        public BattleFunctionEntryTransition[] anyTransitions;
    }
    [Serializable] public sealed class BattleTimelineParameter { public string name; public int type; }
    [Serializable] public sealed class BattleTimelineTexture
    {
        public string key, path, sha256;
        public int width, height, filter, wrapU, wrapV;
    }
    [Serializable] public sealed class BattleTimelineSprite
    {
        public string key, name, texture;
        public float[] rect, pivot, border;
        public float ppu;
    }
    [Serializable] public sealed class BattleTimelineMaterial
    {
        public string key, name, shader;
        public int queue;
        public string[] keywords;
        public BattleTimelineFloat[] floats;
        public BattleTimelineColor[] colors;
        public BattleTimelineMaterialTexture[] textures;
    }
    [Serializable] public sealed class BattleTimelineFloat { public string name; public float value; }
    [Serializable] public sealed class BattleTimelineColor { public string name; public float[] value; }
    [Serializable] public sealed class BattleTimelineMaterialTexture
    {
        public string name, texture;
        public float[] scale, offset;
    }
    [Serializable] public sealed class BattleTimelineShader { public string key, name; }

    public sealed class OfficialBattleTimelineAssets : ScriptableObject
    {
        public GameObject ScenePrefab, CardPrefab;
        public string SourceSha256;
        public float SceneSize, Z, DeadTime, MinDistance, MoveDuration;
        public string Anchor;
        public BattleTimelineCardBindings CardBindings;
        public BattleTimelineSceneBindings SceneBindings;
        public static OfficialBattleTimelineAssets Load()
        {
            var assets = Resources.Load<OfficialBattleTimelineAssets>("Combat/OfficialBattleTimelineAssets");
            if (assets == null || assets.ScenePrefab == null || assets.CardPrefab == null)
                throw new InvalidOperationException("Restore the official scene timeline assets before entering battle.");
            return assets;
        }
    }
}
