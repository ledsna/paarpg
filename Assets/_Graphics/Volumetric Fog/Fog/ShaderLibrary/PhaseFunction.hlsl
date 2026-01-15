#pragma warning (disable: 3571)

// Henyey-Greenstein
float hg(float a, float g)
{
    float g2 = g * g;
    // ignore warning: f = 1 + g2 - 2 * g * (a), where a = cos(x) never will be negate:
    // 1 + g2 - 2ga + a2 - a2 =
    // 1 + (g - a)2 - a2 =
    // 1 + (g - cos(x))^2 - (cos(x))^2
    return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
}
            
float phase(float a, float4 phaseParams)
{
    float blend = .5;
    float hgBlend = hg(a, phaseParams.x) * (1 - blend) + hg(a, -phaseParams.y) * blend;
    return phaseParams.z + hgBlend * phaseParams.w;
}
