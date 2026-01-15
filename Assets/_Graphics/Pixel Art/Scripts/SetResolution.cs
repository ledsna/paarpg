using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class SetResolution : MonoBehaviour
{
    [SerializeField] private RenderTexture renderTexture;

    private int lastWidth = -1;
    private int lastHeight = -1;
    [SerializeField] private int scale = 40;
    [SerializeField] private Vector2Int aspect = new(16, 9);
    private int width = 640;
    private int height = 360;

    private static readonly int ResolutionID = Shader.PropertyToID("_PixelResolution");

    void OnEnable()
    {
        UpdateResolution();
    }

    void Update()
    {
        if (!renderTexture) return;

        width = aspect.x * scale;
        height = aspect.y * scale;
        
        if (width != lastWidth || height != lastHeight)
            UpdateResolution();
    }

    private void UpdateResolution()
    {
        if (!renderTexture) return;
        
        if (renderTexture.IsCreated())
            renderTexture.Release();

        renderTexture.width = width;
        renderTexture.height = height;
        
        renderTexture.Create();

        lastWidth = renderTexture.width;
        lastHeight = renderTexture.height;

        Shader.SetGlobalVector(ResolutionID, new Vector2(width, height));
    }
}