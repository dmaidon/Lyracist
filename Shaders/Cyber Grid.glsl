// Cyber Grid - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    uv.y = -uv.y;

    vec3 col = vec3(0.03, 0.01, 0.08);
    float horizon = -0.05;

    if (uv.y > horizon) {
        vec2 sunUv = uv - vec2(0.0, 0.16);
        float sunRadius = 0.24 + iBass * 0.05;
        float d = length(sunUv);
        if (d < sunRadius) {
            float stripes = step(0.18, sin(uv.y * 95.0 + iTime * 2.5) + (uv.y - 0.05) * 4.5);
            vec3 sunCol = mix(vec3(1.0, 0.08, 0.55), vec3(1.0, 0.85, 0.15), (uv.y + 0.05) * 2.6);
            col = mix(col, sunCol, stripes);
        }
        float sunGlow = exp(-d * 4.2) * (0.55 + iBass * 0.6);
        col += vec3(1.0, 0.18, 0.6) * sunGlow;
    } else {
        float py = abs(uv.y - horizon) + 0.002;
        float z = 0.25 / py;
        float x = uv.x * z * 1.4;

        float gx = abs(fract(x) - 0.5);
        float gz = abs(fract(z - iTime * 2.4 - iBass * 0.6) - 0.5);
        float line = smoothstep(0.06, 0.01, min(gx, gz));

        float fade = exp(-py * 1.5) * smoothstep(0.0, 0.08, py);
        vec3 gridCol = mix(vec3(0.0, 0.9, 1.0), vec3(1.0, 0.1, 0.8), sin(z * 0.4 - iTime) * 0.5 + 0.5);
        col = mix(vec3(0.06, 0.01, 0.14), gridCol * (0.8 + iBass * 0.7), line * fade);
    }

    float hGlow = 0.012 / (abs(uv.y - horizon) + 0.012);
    col += vec3(0.95, 0.2, 0.85) * hGlow * (0.45 + iMid * 0.65);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
