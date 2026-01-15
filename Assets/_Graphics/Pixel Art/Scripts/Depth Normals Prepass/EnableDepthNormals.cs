using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace PixelPerfectCamera.NormalTextureFeature
{
    /// <summary>
    /// A simple Renderer Feature that forces URP to generate the DepthNormals texture (_CameraNormalsTexture).
    /// This is done by injecting a pass that requests ScriptableRenderPassInput.Normal.
    /// </summary>
    public class EnableDepthNormals : ScriptableRendererFeature
    {
        class EnableDepthNormalsPass : ScriptableRenderPass
        {
            public EnableDepthNormalsPass()
            {
                // The event doesn't matter much since we don't draw anything, 
                // but we need to be in the pipeline to make the request.
                renderPassEvent = RenderPassEvent.BeforeRendering;
            }

            public void Setup()
            {
                // This is the key line: it tells URP "I need the normals texture".
                // URP will then automatically inject the internal DepthNormalOnlyPass.
                ConfigureInput(ScriptableRenderPassInput.Normal);
            }

            // Required for URP 17+ (Unity 6) compatibility to avoid warnings, even if empty.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext) { }
        }

        private EnableDepthNormalsPass m_Pass;

        public override void Create()
        {
            m_Pass = new EnableDepthNormalsPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_Pass.Setup();
            renderer.EnqueuePass(m_Pass);
        }
    }
}
