Texture2D<float4> CapturedDesktop : register(t0);
SamplerState LinearClampSampler : register(s0);

cbuffer LensConstants : register(b0)
{
    float2 center;
    float influenceRadius;
    float strength;
    float2 textureSize;
    float dpiScale;
    float deltaSeconds;
};

struct PixelInput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

float4 PSMain(PixelInput input) : SV_Target
{
    float2 uv = input.uv;
    float2 delta = uv - center;
    float distanceToCenter = max(length(delta), 0.0001);
    float influence = saturate(1.0 - distanceToCenter / influenceRadius);
    float eased = influence * influence * (3.0 - 2.0 * influence);
    float radialOffset = 0.055 * eased * eased * saturate(strength);
    float tangentialOffset = 0.018 * eased * saturate(strength);
    float2 radial = length(delta) > 0.0001
        ? delta / distanceToCenter
        : float2(0.0, 0.0);
    float2 tangent = float2(-radial.y, radial.x);
    float2 sampleUv = uv + radial * radialOffset + tangent * tangentialOffset;

    float2 halfTexel = 0.5 / max(textureSize, float2(1.0, 1.0));
    sampleUv = clamp(sampleUv, halfTexel, 1.0 - halfTexel);
    float4 captured = CapturedDesktop.Sample(LinearClampSampler, sampleUv);

    // Cross-fade into the actual desktop at the edge so the overlay has no
    // visible rectangular seam. RGB is premultiplied for DirectComposition.
    float lensMask =
        1.0 - smoothstep(influenceRadius * 0.82, influenceRadius, length(delta));
    float alpha = captured.a * lensMask;
    return float4(captured.rgb * alpha, alpha);
}
