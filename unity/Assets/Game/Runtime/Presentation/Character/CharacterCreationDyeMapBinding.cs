using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterCreationDyeMapBinding : MonoBehaviour
    {
        [SerializeField] private int skinType;
        [SerializeField] private Texture2D dyeMask;

        public int SkinType => skinType;
        public Texture2D DyeMask => dyeMask;

        public void Bind(int type, Texture2D mask)
        {
            skinType = type;
            dyeMask = mask;
        }
    }
}
