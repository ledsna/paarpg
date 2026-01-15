#ifndef TERRAIN_DYNAMIC_LIGHTMAP_CAPTURE_PASS_INCLUDED
#define TERRAIN_DYNAMIC_LIGHTMAP_CAPTURE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// This pass captures the dynamic lightmap (Enlighten Realtime GI) values
// by rendering the terrain with lightmap UVs as the vertex positions.
// The output is a texture that can be sampled by procedural geometry (grass).

struct CaptureAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CaptureVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float2 dynamicLightmapUV : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv);

CaptureVaryings DynamicLightmapCaptureVertex(CaptureAttributes input)
{
    CaptureVaryings output = (CaptureVaryings)0;
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Apply terrain instancing
    TerrainInstancing(input.positionOS, input.normalOS, input.texcoord);

    // Get world space normal for GI sampling
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
    output.normalWS = normalInputs.normalWS;

    // Calculate dynamic lightmap UV
    // The terrain's lightmap UVs typically match the terrain UVs (texcoord)
    // We need to transform them using unity_DynamicLightmapST
    output.dynamicLightmapUV = input.texcoord * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;

    // Output position: use lightmap UVs as clip space position
    // This fills the render target in lightmap UV space
    // UV (0,0) -> clip (-1, -1), UV (1,1) -> clip (1, 1)
    float2 clipPos = input.texcoord * 2.0 - 1.0;
    
    // Flip Y for proper orientation (render targets have inverted Y)
    #if UNITY_UV_STARTS_AT_TOP
    clipPos.y = -clipPos.y;
    #endif
    
    output.positionCS = float4(clipPos, 0.5, 1.0);

    return output;
}

half4 DynamicLightmapCaptureFragment(CaptureVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // Sample the dynamic lightmap
    // This is the key - during DrawRenderers, Unity has bound unity_DynamicLightmap
    // for this terrain object, so we can sample it!
    
    #if defined(DYNAMICLIGHTMAP_ON)
        // Sample dynamic lightmap and decode
        half4 encodedLightmap = SAMPLE_TEXTURE2D(unity_DynamicLightmap, samplerunity_DynamicLightmap, input.dynamicLightmapUV);
        
        // Decode the lightmap (Unity uses RGBM or other encoding)
        half3 dynamicGI = DecodeLightmap(encodedLightmap, half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0));
        
        #if defined(DIRLIGHTMAP_COMBINED)
            // Sample directional lightmap for better quality
            half4 direction = SAMPLE_TEXTURE2D(unity_DynamicDirectionality, samplerunity_DynamicLightmap, input.dynamicLightmapUV);
            half3 normalWS = normalize(input.normalWS);
            dynamicGI = DecodeDirectionalLightmap(dynamicGI, direction, normalWS);
        #endif
        
        return half4(dynamicGI, 1.0);
    #else
        // DEBUG: Output magenta when DYNAMICLIGHTMAP_ON is not defined
        // This helps identify if the keyword is being set correctly
        return half4(1, 0, 1, 1);
    #endif
}

#endif // TERRAIN_DYNAMIC_LIGHTMAP_CAPTURE_PASS_INCLUDED
