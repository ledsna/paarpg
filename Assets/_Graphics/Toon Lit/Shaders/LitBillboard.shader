Shader "Ledsna/LitInstancedBillboard"
{
    Properties
    {
        _Scale("Scale", Float) = 0.3

        [Header(Texture Array)]
        [Space(10)]
        _TextureArray("Texture Array", 2DArray) = "" {}
        _FallbackTexture("Fallback Texture (if no array)", 2D) = "white" {}

        [Toggle] _DEBUG_CULL_MASK ("Debug Cull Mask", Float) = 0
        
        [Header(Wind Settings)]
        [Space(10)]
        _WindSpeed("Wind Speed", Float) = 1.0
        _WindStrength("Wind Strength", Float) = 0.1
        _WindDirection("Wind Direction", Vector) = (1, 0, 1, 0)
        _WindFrequency("Wind Frequency", Float) = 1.0
        _WindGustStrength("Wind Gust Strength", Float) = 0.05
        
        [Header(Wild Grass Settings)]
        [Space(10)]
        _WildGrassChance("Wild Grass Chance (lower = rarer)", Range(0.0, 1.0)) = 0.01
        _WildNormalStrength("Wild Normal Strength", Range(0.0, 1.0)) = 0.3
        
        [Header(Flower Settings)]
        [Space(10)]
        _FlowerSizeMultiplier("Flower Size Multiplier", Range(0.5, 3.0)) = 1.25
        _FlowerSizeVariation("Flower Size Variation", Range(0.0, 0.5)) = 0.15
        _FlowerCameraNudge("Flower Camera Nudge", Range(0.0, 0.5)) = 0.05

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

        _DiffuseSteps("Diffuse Steps", Float) = 5.0
        _FresnelSteps("Fresnel Steps", Float) = 3.0
        _SpecularStep("Specular Step", Range(0, 1)) = 0.1

        [Space(20)]

        _DistanceSteps("Light Distance Steps", Float) = -1
        _ShadowSteps("Shadow Steps", Float) = -1
        _ReflectionSteps("Reflection Steps", Float) = -1

        // Lit Shader properties

        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        // Specular vs Metallic workflow

        // [HideInInspector]
        // [Toggle(_SPECULAR_SETUP)] _MetallicSpecToggle ("Workflow, Specular (if on), Metallic (if off)", Float) = 0
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
        [HideInInspector] _ZWrite("__zw", Float) = 0.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 0.0
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
        Tags
        {
            "Queue" = "AlphaTest"
            "PreviewType" = "Plane"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "TerrainCompatible" = "True"
        }
        LOD 300

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _USE_TEXTURE_COLOR

            // Removed: _ENVIRONMENTREFLECTIONS_OFF (always enabled)
            // Removed: _SPECULARHIGHLIGHTS_OFF (always enabled)
            // Removed: _RECEIVE_SHADOWS_OFF (always enabled)

            // -------------------------------------
            // Universal Pipeline keywords (DRASTICALLY REDUCED)
            // Shadows are always enabled and soft
            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #define _SHADOWS_SOFT
            #define ALPHATEST_ON

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

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION


            //--------------------------------------
            // GPU Instancing
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma multi_compile_instancing

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            #pragma instancing_options procedural:Setup
            // For no freezing while async shader compilation
            #pragma editor_sync_compilation
            // #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/BillboardForwardPass.hlsl"
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
            ZTest LEqual
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_TEXTURE_COLOR

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:Setup
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            
            #include "../ShaderLibrary/BillboardDepthOnlyPass.hlsl"
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_TEXTURE_COLOR

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:Setup
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/BillboardDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

//    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"

    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.CustomBillboardShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}