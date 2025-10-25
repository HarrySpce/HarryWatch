sampler2D input : register(s0);

float width : register(C0);
float height : register(C1);
float power : register(C2);

float2 barrelPincushion(float2 uv, float k)
{
    float2 st = uv - 0.5;
    st.y += 0.75;
    float radius = sqrt(dot(st, st));
    st.y *= 1.0 + k * pow(radius, 2.0);
    st.y -= 0.75;

    return 0.5 + st;
}

float4 main(float4 fragCoord : SV_POSITION) : SV_TARGET
{
    float2 st = fragCoord.xy / float2(width, height);

    float k = sin(power);
    st = barrelPincushion(st, k);

    return tex2D(input, st);
}