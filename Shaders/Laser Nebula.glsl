// Laser Nebula - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float t = iTime * 0.65;

    vec2 p = uv * 2.2;
    for (int i = 1; i < 5; i++) {
        float fi = float(i);
        p.x += 0.35 / fi * sin(fi * 2.5 * p.y + t + iBass * 1.4);
        p.y += 0.35 / fi * cos(fi * 2.5 * p.x + t + iMid * 1.2);
    }

    float r = 0.5 + 0.5 * sin(p.x + p.y + iBass * 2.0);
    float g = 0.5 + 0.5 * sin(p.x - p.y + 2.0 + iMid * 1.5);
    float b = 0.5 + 0.5 * cos(p.x * p.y + 4.0 + iHigh * 1.5);

    vec3 col = vec3(r * 0.8, g * 0.35 + b * 0.25, b * 0.95);

    for (int k = 0; k < 3; k++) {
        float fk = float(k);
        float angle = sin(t * (0.8 + fk * 0.3) + fk * 2.1) * 0.85;
        vec2 dir = vec2(cos(angle), sin(angle));
        float beamDist = abs(dot(uv - vec2(0.0, -0.45), vec2(-dir.y, dir.x)));
        float beam = 0.006 / (beamDist + 0.006) * (0.35 + iHigh * 0.85);
        vec3 beamCol = (k == 0) ? vec3(0.1, 1.0, 0.6) : ((k == 1) ? vec3(1.0, 0.15, 0.7) : vec3(0.2, 0.7, 1.0));
        col += beamCol * beam * 0.45;
    }

    fragColor = vec4(clamp(col * (0.55 + iEnergy * 0.55), 0.0, 1.0), 1.0);
}
