Shader "Ledsna/Lit"
{
    Properties
    {
        // Outline Thresholds
        _DepthThreshold("Depth Threshold", Float) = 52
        _NormalsThreshold("Normals Threshold", Float) = 0.17
        _ExternalScale("External Scale", Float) = 1
        _InternalScale("Internal Scale", Float) = 1

        // [Space(20)]

        // Outlines Settings
        [ToggleUI]_DebugOn("Debug", Float) = 0
        [ToggleUI]_External("External", Float) = 0
        [ToggleUI]_Convex("Convex", Float) = 0
        [ToggleUI]_Concave("Concave", Float) = 0
        // [ToggleUI]_Outside("Outside", Float) = 0
        _OutlineStrength("OutlineStrength", Range(0, 1)) = 0.5
        _OutlineColour("OutlineColour", Color) = (0,0,0,1)

        // Cel Shading (Diffuse-Specular)
        [Space(20)]
        [ToggleUI]_DiffuseSpecularCelShader("Diffuse-Specular", Float) = 0

        _DiffuseSteps("Diffuse Steps", Float) = -1
        _FresnelSteps("Fresnel Steps", Float) = -1
        _SpecularStep("Specular Step", Range(0, 1)) = 0.5

        [Space(20)]

        _DistanceSteps("Light Distance Steps", Float) = -1
        _ShadowSteps("Shadow Steps", Float) = -1
        _ReflectionSteps("Reflection Steps", Float) = -1

        // Lit Properties

        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)

        _WorkflowMode("WorkflowMode", Float) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        // Removed toggles - always enabled:
        // - Specular Highlights (always on)
        // - Environment Reflections (always on)
        // - Receive Shadows (always on)

        [Space(40)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}

        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecGlossMap("Specular", 2D) = "white" {}

        // [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        // [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0
        
        // Obsolete Properties (for material upgrader)
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "TerrainCompatible" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // -------------------------------------
            // Material Keywords (kept minimal)
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // Removed: _RECEIVE_SHADOWS_OFF (always enabled)
            // #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            // #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            // -------------------------------------
            // Universal Pipeline keywords (DRASTICALLY REDUCED)
            // Shadows are always enabled and soft
            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #define _SHADOWS_SOFT

            // Keep only what actually varies in your scenes:
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // Removed multi_compile for always-enabled features:
            // - Main light shadows (always cascade)
            // - Additional light shadows (removed - use shader_feature if needed)
            // - Forward+ (removed - not used)

            // -------------------------------------
            // GI / Lightmapping keywords
            // Added for realtime GI support (light probes, dynamic lightmaps, probe volumes)
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            // #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

        // Pass
        // {
        //     Name "Universal2D"
        //     Tags
        //     {
        //         "LightMode" = "Universal2D"
        //     }

        //     // -------------------------------------
        //     // Render State Commands
        //     Blend[_SrcBlend][_DstBlend]
        //     ZWrite[_ZWrite]
        //     Cull[_Cull]

        //     HLSLPROGRAM
        //     #pragma target 3.5

        //     // -------------------------------------
        //     // Shader Stages
        //     #pragma vertex vert
        //     #pragma fragment frag

        //     // -------------------------------------
        //     // Material Keywords
        //     #pragma shader_feature_local_fragment _ALPHATEST_ON
        //     #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

        //     #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

        //     // -------------------------------------
        //     // Includes
        //     #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        //     #include "Packages/com.com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
        //     ENDHLSL
        // }
    }

    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"

    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.CustomShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
