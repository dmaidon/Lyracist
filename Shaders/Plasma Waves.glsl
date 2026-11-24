// Plasma Waves - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float t = iTime * 0.9;

    float v1 = sin(uv.x * 4.5 + t + iBass * 1.8);
    float v2 = sin(4.5 * (uv.x * sin(t * 0.5) + uv.y * cos(t * 0.35)) + t);
    float cx = uv.x + 0.5 * sin(t * 0.3);
    float cy = uv.y + 0.5 * cos(t * 0.45);
    float v3 = sin(sqrt(22.0 * (cx * cx + cy * cy) + 1.0) - t * 1.6 - iMid * 2.0);
    float v = (v1 + v2 + v3) * 0.333;

    vec3 col = vec3(
        0.5 + 0.5 * sin(3.14159 * v + 0.0 + iBass * 1.2),
        0.5 + 0.5 * sin(3.14159 * v + 2.094 + iMid),
        0.5 + 0.5 * sin(3.14159 * v + 4.188 + iHigh)
    );

    float waveY = sin(uv.x * 6.0 + t * 2.2) * (0.12 + iBass * 0.22);
    float ribbon = 0.018 / (abs(uv.y - waveY) + 0.015);
    col = col * (0.45 + 0.45 * iEnergy) + vec3(0.2, 0.85, 1.0) * ribbon * (0.4 + iHigh * 0.8);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
