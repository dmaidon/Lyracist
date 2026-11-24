// Electric Kaleidoscope - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float r = length(uv);
    float a = atan(uv.y, uv.x);

    float segments = 8.0;
    a = mod(a, 6.2831853 / segments);
    a = abs(a - 3.1415926 / segments);

    vec2 p = vec2(cos(a), sin(a)) * r;
    float t = iTime * 0.8 + iBass * 0.5;

    vec3 col = vec3(0.0);
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        p = abs(p) / clamp(dot(p, p), 0.25, 1.2) - vec2(0.55 + 0.15 * sin(t + fi), 0.45 + 0.12 * cos(t * 0.7));
        float d = abs(length(p) - 0.25 - iMid * 0.15);
        float glow = 0.015 / (d + 0.015);
        vec3 layerCol = vec3(
            0.5 + 0.5 * sin(fi * 2.1 + t),
            0.5 + 0.5 * sin(fi * 2.1 + t + 2.09),
            0.5 + 0.5 * sin(fi * 2.1 + t + 4.18)
        );
        col += layerCol * glow * (0.28 + iEnergy * 0.4);
    }

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
