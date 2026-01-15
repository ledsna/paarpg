#ifndef OUTLINES_INCLUDED
#define OUTLINES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

SamplerState point_clamp_sampler;
float2 _PixelResolution;

static const half3 NORMAL_EDGE_BIAS = half3(0.57735, 0.57735, 0.57735); // normalize(1,1,1)

half GetDepth(half2 uv)
{
    half rawDepth = SampleSceneDepth(uv);
    if (!unity_OrthoParams.w)
    {
        return LinearEyeDepth(rawDepth, _ZBufferParams);
    }
    half orthoLinearDepth = _ProjectionParams.x > 0 ? rawDepth : 1 - rawDepth;
    half orthoEyeDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, orthoLinearDepth);
    return orthoEyeDepth;
}

half3 GetNormal(half2 uv)
{
    return SampleSceneNormals(uv);
}

void GetNeighbourUVs(half2 uv, half distance, out half2 neighbours[4])
{
    half2 pixelSize = 1.0 / _PixelResolution;
    half2 offset = pixelSize * distance;
    
    neighbours[0] = uv + half2(0,  offset.y);  // Top
    neighbours[1] = uv + half2(0, -offset.y);  // Bottom
    neighbours[2] = uv + half2( offset.x, 0);  // Right
    neighbours[3] = uv + half2(-offset.x, 0);  // Left
}

void GetDepthDiffSum(half depth, half2 neighbours[4], out half depth_diff_sum)
{
    depth_diff_sum = 0;
    [unroll]
    for (int i = 0; i < 4; ++i)
        depth_diff_sum += GetDepth(neighbours[i]) - depth;
}

void GetNormalDiffSum(half3 normal, half2 neighbours[4], out half normal_diff_sum)
{
    normal_diff_sum = 0;

    [unroll]
    for (int j = 0; j < 4; ++j)
    {
        half3 neighbour_normal = GetNormal(neighbours[j]);
        half3 normal_diff = normal - neighbour_normal;
        half normal_diff_weight = smoothstep(-.01, .01, dot(normal_diff, NORMAL_EDGE_BIAS));

        normal_diff_sum += dot(normal_diff, normal_diff) * normal_diff_weight;
    }
}

half OutlineType(half2 uv)
{
    half2 neighbour_depths[4];
    half2 neighbour_normals[4];
    
    GetNeighbourUVs(uv, _ExternalScale, neighbour_depths);
    GetNeighbourUVs(uv, _InternalScale, neighbour_normals);

    half depth_diff_sum, normal_diff_sum;
    GetDepthDiffSum(GetDepth(uv), neighbour_depths, depth_diff_sum);
    GetNormalDiffSum(GetNormal(uv), neighbour_normals, normal_diff_sum);

    half depth_edge = step(_DepthThreshold / 10000.0h, depth_diff_sum);
    half normal_edge = step(_NormalsThreshold, sqrt(normal_diff_sum));

    // Priority: External edges first, then Internal edges
    half outlineType = 0.0h;
    
    if (depth_edge > 0.0h && _External)
        outlineType = 1.0h;
    else if ((depth_diff_sum >= 0.0h && _Convex) || (depth_diff_sum < 0.0h && _Concave))
        outlineType = 2.0h * normal_edge;
    
    return outlineType;
}

#endif