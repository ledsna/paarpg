#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir)
{
    // Adapted from: http://jcgt.org/published/0007/03/04/
    float3 t0 = (boundsMin - rayOrigin) * invRaydir;
    float3 t1 = (boundsMax - rayOrigin) * invRaydir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);

    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(tmax.x, min(tmax.y, tmax.z));

    // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
    // dstA is dst to nearest intersection, dstB dst to far intersection

    // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
    // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

    // CASE 3: ray misses box (dstA > dstB)

    float dstToBox = max(0, dstA);
    float dstInsideBox = max(0, dstB - dstToBox);
    return float2(dstToBox, dstInsideBox);
}

float random01(float2 p)
{
    return frac(sin(dot(p, float2(41, 289))) * 45758.5453);
}

// Interleaved Gradient Noise - superior to white noise for raymarching
// Produces well-distributed noise that minimizes visible patterns
float InterleavedGradientNoise(float2 pixelCoord)
{
    float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
    return frac(magic.z * frac(dot(pixelCoord, magic.xy)));
}

// Temporal blue noise - adds frame variation for TAA integration
float TemporalBlueNoise(float2 pixelCoord, float time)
{
    float noise = InterleavedGradientNoise(pixelCoord);
    // Golden ratio for even temporal distribution
    return frac(noise + frac(time * 0.61803398875));
}

float GetCorrectDepth(float2 uv)
{
    #if UNITY_REVERSED_Z
    real depth = SampleSceneDepth(uv);
    #else
    // Adjust z to match NDC for OpenGL
    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
    #endif
    return depth;
}
