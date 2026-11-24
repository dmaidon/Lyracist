// Firestorm Pulse - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    uv.y = -uv.y;

    float t = iTime * 1.3;
    vec2 p = uv * 3.0;
    p.y -= t + iBass * 0.8;

    float n = 0.0;
    float amp = 0.55;
    for (int i = 0; i < 4; i++) {
        n += amp * (sin(p.x * 1.8 + sin(p.y * 1.4 + t)) * cos(p.y * 1.6 - cos(p.x * 1.3)));
        p = p * 1.9 + vec2(1.7, -t * 0.6);
        amp *= 0.5;
    }

    float heat = clamp((0.45 - uv.y) * 1.1 + n * 0.45 + iBass * 0.45, 0.0, 1.2);
    vec3 col = vec3(
        smoothstep(0.05, 0.55, heat),
        smoothstep(0.35, 0.85, heat) * 0.75,
        smoothstep(0.65, 1.15, heat) * 0.9
    );

    float r = length(uv);
    float ring = 0.014 / (abs(r - (0.22 + iBass * 0.14)) + 0.018);
    col += vec3(1.0, 0.45, 0.1) * ring * (0.4 + iHigh * 0.8);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
