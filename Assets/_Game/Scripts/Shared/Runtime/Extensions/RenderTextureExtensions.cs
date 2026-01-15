using UnityEngine;

namespace __Project.Shared.Extensions
{
    public static class RenderTextureExtensions
    {
        public static Texture2D ToTexture2D(this RenderTexture rt)
        {
            var output = new Texture2D(rt.width, rt.height);
            RenderTexture.active = rt;
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            output.Apply();
            return output;
        }
    }
}