#pragma once

float4 SoftBlending(float4 color, float godRays, float3 godRayColor = 1)
{
    // Soft falloff (e.g., exponential)
    float softenedRays = 1.0 - exp(-godRays);

    // Blend with original color
    return color + float4(softenedRays * godRayColor, 0);
}

float4 ScreenBlending(float4 color, float godRays, float3 godRayColor = 1)
{
    float3 rays = godRays * godRayColor;
    float3 screenBlend = 1 - (1 - rays) * (1 - color.rgb);
    return float4(screenBlend, color.a);
}

float4 LerpBlending(float4 color, float godRays, float godRayPower, float3 godRayColor = 1)
{
    float intensity = pow(saturate(godRays), godRayPower); // _GodRayPower ~ 0.5 - 2.0
    float3 result = lerp(color.rgb, godRayColor, intensity); // Blend towards bright color
    return float4(result, color.a);
}

float4 SaturateAdditionalBlending(float4 color, float godRays, float Intensity, float3 godRayColor = 1)
{
    float3 finalShaft = saturate(godRays) * normalize(godRayColor) * Intensity;
    return color + float4(finalShaft, 1);
}

float4 AlphaBlending(float4 color, float godRays, float Intensity, float3 godRayColor = 1)
{
    // Use screen blend instead of additive for more natural look
    // This prevents over-brightening while still allowing light shafts to show
    float3 rays = saturate(godRays * Intensity) * godRayColor;
    float3 result = 1.0 - (1.0 - color.rgb) * (1.0 - rays);
    return float4(result, color.a);
}
