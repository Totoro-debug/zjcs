using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Native Unity gradients use 16-bit key times, rather than JsonUtility's managed Gradient representation.
    [Serializable]
    public sealed class WorldNativeGradientData
    {
        public Color key0,key1,key2,key3,key4,key5,key6,key7;
        public int ctime0,ctime1,ctime2,ctime3,ctime4,ctime5,ctime6,ctime7;
        public int atime0,atime1,atime2,atime3,atime4,atime5,atime6,atime7;
        public int m_NumColorKeys,m_NumAlphaKeys,m_Mode,m_ColorSpace;
        public Gradient Value()
        {
            var keys=new[]{key0,key1,key2,key3,key4,key5,key6,key7};
            var colorTimes=new[]{ctime0,ctime1,ctime2,ctime3,ctime4,ctime5,ctime6,ctime7};
            var alphaTimes=new[]{atime0,atime1,atime2,atime3,atime4,atime5,atime6,atime7};
            var colors=new GradientColorKey[m_NumColorKeys];var alphas=new GradientAlphaKey[m_NumAlphaKeys];
            for(int i=0;i<colors.Length;i++)colors[i]=new GradientColorKey(keys[i],colorTimes[i]/65535f);
            for(int i=0;i<alphas.Length;i++)alphas[i]=new GradientAlphaKey(keys[i].a,alphaTimes[i]/65535f);
            var result=new Gradient();result.SetKeys(colors,alphas);result.mode=(GradientMode)m_Mode;return result;
        }
    }
}
