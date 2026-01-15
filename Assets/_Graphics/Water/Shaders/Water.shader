Shader "Ledsna/WaterTess"
{
    Properties
    {
        [Header(Surface)]
        _Tint("Absorption Tint", Color) = (0.66,0.77,0.88,1)
        _ScatterColor("Scatter Color (Deep)", Color) = (0.05, 0.2, 0.3, 1)
        _Smoothness("Smoothness", Range(0.01, 1)) = 0.8
        _SpecularColor("Specular Color", Color) = (1,1,1,0.5)
        
        [Header(Depth Effects)]
        _Density("Water Density", Range(0, 1)) = 1
        _FoamThreshold("Foam Depth", Range(0.001, 0.2)) = 0.03
        _RefractionStrength("Refraction Strength", Float) = 0.02
        // _ChromaticAberration("Chromatic Aberration", Range(0, 0.05)) = 0.005
        
        [Header(SSR)]
        _SSRStr("Strength", Range(0, 1)) = 1
        
        [Header(Ripples)]
        [HideInInspector]
        _RippleMap("Ripple Map", 2D) = "black" {}
        
        [Header(Surface Noise)]
        _NoiseScale("Scale", Float) = 5.0
        _NoiseSpeed("Speed", Float) = 0.3
        _NoiseStrength("Strength", Range(0, 0.1)) = 0.015

        [Header(Large Waves)]
        _WaveScale("Wave Scale", Float) = 8.0
        _WaveSpeed("Wave Speed", Float) = 0.5
        _WaveHeight("Wave Height", Range(0, 1)) = 0.1
        
        [Header(Tessellation)]
        _TessellationFactor("Factor", Range(1, 64)) = 16
        _TessellationMinDistance("Min Distance", Float) = 5
        _TessellationMaxDistance("Max Distance", Float) = 50
        
        [Header(Highlight Line)]
        _HighlightWidth("Width", Float) = 0.1
        _HighlightOscillation("Oscillation Amount", Float) = 0.1
        _HighlightSpeed("Oscillation Speed", Float) = 5.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            // Merged pass: Stencil removed to render for all pixels and handle logic internally

            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation

            // Stages
            #pragma vertex Vert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag

            // URP feature toggles (REDUCED)
            // Shadows always enabled and soft
            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #define _SHADOWS_SOFT

            // Keep only what varies:
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_COOKIES

            // Removed: _FORWARD_PLUS (not used)
            #include "WaterForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SSRPrepass"
            Tags { "LightMode" = "SSRPrepass" }
            ZWrite On
            ColorMask RGBA
            Cull Back

            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation
            #pragma vertex Vert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment SSRPrepassFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RippleMap);
            float4 _RippleMap_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Smoothness;
                float4 _SpecularColor;
                float _Density;
                float _FoamThreshold;
                float _RefractionStrength;
                // float _ChromaticAberration;
                float _ReflectionUVOffset;
                float4 _RippleMapOrigin;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;                
                float _WaveScale;
                float _WaveSpeed;
                float _WaveHeight;
                float _TessellationFactor;
                float _TessellationMinDistance;
                float _TessellationMaxDistance;
                float _HighlightWidth;
                float _HighlightOscillation;
                float _HighlightSpeed;
            CBUFFER_END

            struct Attributes { 
                float4 positionOS: POSITION; 
                float3 normalOS: NORMAL;
            };

            struct TessControlPoint
            {
                float4 positionOS : INTERNALTESSPOS;
                float3 positionWS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            struct TessFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            // === Noise Functions ===
            float2 Unity_GradientNoise_Dir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x % 289;
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float Unity_GradientNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(Unity_GradientNoise_Dir(ip), fp);
                float d01 = dot(Unity_GradientNoise_Dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(Unity_GradientNoise_Dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(Unity_GradientNoise_Dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * (3 - 2 * fp);
                return lerp(lerp(d00, d10, fp.x), lerp(d01, d11, fp.x), fp.y);
            }

            float GetWaveHeight(float3 posWS)
            {
                // Layer 1 (Large)
                float2 p1 = posWS.xz * _WaveScale + float2(_Time.y * _WaveSpeed, _Time.y * _WaveSpeed * 0.8);
                float h1 = Unity_GradientNoise(p1);

                // Layer 2 (Detail)
                float2 p2 = posWS.xz * _WaveScale * 2.0 - float2(_Time.y * _WaveSpeed * 1.2, 0);
                float h2 = Unity_GradientNoise(p2) * 0.5;
                
                return (h1 + h2) * _WaveHeight; 
            }

            // === Ripple Displacement Function ===
            float3 ComputeRipple(float3 posWS)
            {
                float2 uv = (posWS.xz - _RippleMapOrigin.xy) / _RippleMapOrigin.z + 0.5;
                float dist = distance(posWS, _WorldSpaceCameraPos);
                float lod = saturate((dist - _TessellationMinDistance) / (_TessellationMaxDistance - _TessellationMinDistance)) * 4.0;
                float2 texelSize = _RippleMap_TexelSize.xy;
                
                float h = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv, lod).r;
                float h_right = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv + float2(texelSize.x, 0), lod).r;
                float h_up = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv + float2(0, texelSize.y), lod).r;
                
                float2 distFromEdge = min(uv, 1.0 - uv);
                float mask = saturate(min(distFromEdge.x, distFromEdge.y) * 20.0);
                h *= mask; h_right *= mask; h_up *= mask;
                
                float dH_du = (h_right - h);
                float dH_dv = (h_up - h);
                float worldStep = max(_RippleMapOrigin.z * texelSize.x, 0.0001); // Prevent div by zero if uninitialized
                float dHdx = dH_du / worldStep;
                float dHdz = dH_dv / worldStep;

                // --- Procedural Noise Waves ---
                float noiseStep = 0.005;
                float wave_c = GetWaveHeight(posWS);
                float wave_r = GetWaveHeight(posWS + float3(noiseStep, 0, 0));
                float wave_u = GetWaveHeight(posWS + float3(0, 0, noiseStep));
                
                float wave_dHdx = (wave_r - wave_c) / noiseStep;
                float wave_dHdz = (wave_u - wave_c) / noiseStep;
                
                h += wave_c;
                dHdx += wave_dHdx;
                dHdz += wave_dHdz;

                return float3(h, dHdx, dHdz);
            }

            TessControlPoint Vert(Attributes v)
            {
                TessControlPoint o;
                o.positionOS = v.positionOS;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalOS = v.normalOS;
                return o;
            }

            TessFactors PatchConstantFunc(InputPatch<TessControlPoint, 3> patch)
            {
                TessFactors factors;
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 edge0 = patch[2].positionWS - patch[1].positionWS;
                float3 edge1 = patch[0].positionWS - patch[2].positionWS;
                float3 edge2 = patch[1].positionWS - patch[0].positionWS;
                float edgeLen0 = length(edge0);
                float edgeLen1 = length(edge1);
                float edgeLen2 = length(edge2);
                float3 edge0Mid = 0.5 * (patch[1].positionWS + patch[2].positionWS);
                float3 edge1Mid = 0.5 * (patch[2].positionWS + patch[0].positionWS);
                float3 edge2Mid = 0.5 * (patch[0].positionWS + patch[1].positionWS);
                float dist0 = distance(edge0Mid, cameraPos);
                float dist1 = distance(edge1Mid, cameraPos);
                float dist2 = distance(edge2Mid, cameraPos);
                float rcpRange = rcp(_TessellationMaxDistance - _TessellationMinDistance);
                float distFade0 = saturate(1.0 - (dist0 - _TessellationMinDistance) * rcpRange);
                float distFade1 = saturate(1.0 - (dist1 - _TessellationMinDistance) * rcpRange);
                float distFade2 = saturate(1.0 - (dist2 - _TessellationMinDistance) * rcpRange);
                float tess0 = edgeLen0 * _TessellationFactor * distFade0;
                float tess1 = edgeLen1 * _TessellationFactor * distFade1;
                float tess2 = edgeLen2 * _TessellationFactor * distFade2;
                factors.edge[0] = clamp(tess0, 1.0, 64.0);
                factors.edge[1] = clamp(tess1, 1.0, 64.0);
                factors.edge[2] = clamp(tess2, 1.0, 64.0);
                factors.inside = clamp((tess0 + tess1 + tess2) * 0.333333, 1.0, 64.0);
                return factors;
            }

            [domain("tri")]
            [partitioning("integer")]
            [outputtopology("triangle_cw")]
            [patchconstantfunc("PatchConstantFunc")]
            [outputcontrolpoints(3)]
            TessControlPoint Hull(InputPatch<TessControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<TessControlPoint, 3> patch, float3 baryCoords : SV_DomainLocation)
            {
                Varyings o;
                float4 posOS = patch[0].positionOS * baryCoords.x + patch[1].positionOS * baryCoords.y + patch[2].positionOS * baryCoords.z;
                float3 normalOS = patch[0].normalOS * baryCoords.x + patch[1].normalOS * baryCoords.y + patch[2].normalOS * baryCoords.z;

                float3 baseWS = TransformObjectToWorld(posOS.xyz);
                float3 geomNormalWS = TransformObjectToWorldNormal(normalOS);
                
                float3 rippleData = ComputeRipple(baseWS);
                
                float verticalDisplacement = rippleData.x;
                float dHdx = rippleData.y;
                float dHdz = rippleData.z;
                
                // Displace along the geometric normal to allow for rotation
                float3 posWS = baseWS + geomNormalWS * verticalDisplacement;
                
                // Normal Calculation
                // 1. Construct a Tangent Basis from the geometric normal
                float3 up = float3(0, 1, 0);
                float3 tangent = normalize(cross(geomNormalWS, up + float3(1, 0, 0) * 0.001));
                float3 bitangent = cross(geomNormalWS, tangent);
                
                // 2. Map world derivatives (dHdx/dHdz) to this local basis
                // This assumes the ripples are "projected" onto the surface.
                // dHdx is change in Height per unit X.
                // n = normalize( N - dHdx * T - dHdz * B )
                float3 nWS = normalize(geomNormalWS - tangent * dHdx - bitangent * dHdz);
                
                o.positionWS = posWS;
                o.normalWS   = nWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.positionOS = posOS.xyz;

                return o;
            }

            half4 SSRPrepassFrag(Varyings i) : SV_Target
            {
                float3 normalWS = normalize(i.normalWS);

                // --- Per-Pixel Wave Normals ---
                float wStep = 0.005;
                float w_c = GetWaveHeight(i.positionWS);
                float w_r = GetWaveHeight(i.positionWS + float3(wStep, 0, 0));
                float w_u = GetWaveHeight(i.positionWS + float3(0, 0, wStep));
                float w_dHdx = (w_r - w_c) / wStep;
                float w_dHdz = (w_u - w_c) / wStep;

                normalWS = normalize(normalWS + float3(-w_dHdx, 0, -w_dHdz));

                // Output World Space Normals (Raw -1..1 range) in RGB
                // Output Smoothness in Alpha (Standard definition)
                return half4(normalWS, _Smoothness);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}

