using System;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI
{
    // Explicit local loading bindings; contains the unchanged selected native config.
    public sealed class BattleRoundPortraitAssets : ScriptableObject
    {
        [Serializable] public sealed class TextureEntry { public string Path; public Texture2D Texture; }
        [Serializable] public sealed class SpriteEntry { public string Path; public Sprite Sprite; }
        public GamePlayManagerConfig Configuration;
        public TextureEntry[] Textures;
        public SpriteEntry[] Sprites;
        public Texture2D LoadTexture(string path)
        {
            foreach (var entry in Textures) if (entry.Path == path) return entry.Texture;
            throw new InvalidOperationException("Unbound original battle portrait texture: " + path);
        }
        public Sprite LoadSprite(string path)
        {
            foreach (var entry in Sprites) if (entry.Path == path) return entry.Sprite;
            throw new InvalidOperationException("Unbound original battle portrait sprite: " + path);
        }
    }
}
