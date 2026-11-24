// Star Warp - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float t = iTime * 0.45 + iBass * 0.15;

    vec3 col = vec3(0.01, 0.02, 0.06);

    for (int layer = 0; layer < 3; layer++) {
        float fl = float(layer);
        float depth = fract(fl * 0.333 + t);
        float scale = mix(12.0, 1.2, depth);
        float fade = depth * smoothstep(1.0, 0.8, depth);

        vec2 p = uv * scale + vec2(fl * 17.3, fl * 9.1);
        vec2 id = floor(p);
        vec2 gv = fract(p) - 0.5;

        float n = hash21(id);
        vec2 offset = vec2(n - 0.5, fract(n * 34.56) - 0.5) * 0.7;
        float d = length(gv - offset);

        float star = (0.018 * (1.0 + iHigh * 1.2)) / (d + 0.008);
        star *= smoothstep(0.45, 0.05, d);

        vec3 starCol = mix(vec3(0.4, 0.75, 1.0), vec3(1.0, 0.5, 0.9), fract(n * 19.1));
        col += starCol * star * fade * 0.45;
    }

    float r = length(uv);
    col += vec3(0.18, 0.06, 0.35) * exp(-r * 2.2) * (0.6 + iBass * 0.8);
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
