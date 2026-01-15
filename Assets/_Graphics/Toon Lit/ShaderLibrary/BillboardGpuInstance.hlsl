// Struct Data From CPU (must match C# GrassData struct layout exactly!)
struct GrassData
{
    float3 position;
    float3 normal;
    float2 lightmapUV;
    int materialIndex;
};

StructuredBuffer<GrassData> _SourcePositionGrass;

float _Scale;
float _WildGrassChance;
// Inputs
float4x4 m_RS;
// Globals
float3 normalWS;
float3 positionWS;
float2 lightmapUV;
int materialIndex;
int textureIndex;
int isWildGrass;

// Flower properties (per-material)
float _FlowerSizeMultiplier;
float _FlowerSizeVariation;

// Simple hash for random values
float HashSimple(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Is called for each instance before vertex stage
void Setup()
{
    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    InitIndirectDrawArgs(0);
    uint instanceID = GetIndirectInstanceID_Base(unity_InstanceID);
    GrassData instanceData = _SourcePositionGrass[instanceID];

    normalWS = instanceData.normal;
    positionWS = instanceData.position;
    lightmapUV = instanceData.lightmapUV;
    materialIndex = instanceData.materialIndex;

    // Set position first
    unity_ObjectToWorld._m03_m13_m23_m33 = float4(positionWS + instanceData.normal * _Scale / 2 , 1.0);

    // Apply the scale to the transformation matrix
    unity_ObjectToWorld._m00_m11_m22 = float3(_Scale, _Scale, _Scale);
    unity_ObjectToWorld = mul(unity_ObjectToWorld, m_RS);

    uint hashX = asuint(positionWS.x) * 1664525u;
    uint hashZ = asuint(positionWS.z) * 1013904223u;
    uint texHash = hashX ^ hashZ;
    textureIndex = int(texHash % 8u);

    float wildHash = frac(sin(dot(positionWS.xz, float2(12.9898, 78.233))) * 43758.5453);
    isWildGrass = wildHash < _WildGrassChance;

    positionWS += normalWS * 0.2;
    #endif
}