// Neon Tunnel - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float r = length(uv) + 0.001;
    float a = atan(uv.y, uv.x);

    float speed = iTime * 1.4 + iBass * 0.85;
    float z = 0.45 / r + speed;

    float ring = sin(z * 8.0) * 0.5 + 0.5;
    float spokes = sin(a * 6.0 + z * 2.5 - iTime * 1.8) * 0.5 + 0.5;
    float grid = smoothstep(0.75, 0.98, ring) + smoothstep(0.82, 0.98, spokes) * 0.7;

    vec3 col1 = vec3(0.05, 0.85, 1.0);
    vec3 col2 = vec3(1.0, 0.12, 0.75);
    vec3 col3 = vec3(0.45, 0.15, 1.0);

    float mixFactor = sin(z * 1.5 - iTime) * 0.5 + 0.5;
    vec3 baseCol = mix(col1, col2, mixFactor);
    baseCol = mix(baseCol, col3, iMid * 0.6);

    float pulse = 0.35 + iBass * 0.95;
    float glow = (0.08 / r) * (0.6 + iHigh * 0.8);
    float vignette = smoothstep(0.02, 0.28, r);

    vec3 finalCol = baseCol * grid * pulse * vignette + col2 * glow * 0.35;
    fragColor = vec4(clamp(finalCol, 0.0, 1.0), 1.0);
}
