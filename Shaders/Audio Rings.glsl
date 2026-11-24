// Audio Rings - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
    float r = length(uv);
    float a = atan(uv.y, uv.x);

    vec3 col = vec3(0.02, 0.03, 0.07);

    for (int i = 1; i <= 4; i++) {
        float fi = float(i);
        float level = (i == 1) ? iBass : ((i == 2) ? iMid : ((i == 3) ? iHigh : iEnergy));
        float wobble = sin(a * (3.0 + fi * 2.0) + iTime * (1.5 + fi * 0.4) * (mod(fi, 2.0) * 2.0 - 1.0)) * (0.015 + level * 0.045);
        float targetR = fi * 0.11 + level * 0.06 + wobble;
        float dist = abs(r - targetR);
        float glow = 0.007 / (dist + 0.005);

        vec3 ringCol = vec3(
            0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7),
            0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 2.094),
            0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 4.188)
        );
        col += ringCol * glow * (0.35 + level * 0.75);
    }

    float core = 0.025 / (r + 0.03) * (0.3 + iBass * 0.9);
    col += vec3(0.35, 0.75, 1.0) * core;
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
