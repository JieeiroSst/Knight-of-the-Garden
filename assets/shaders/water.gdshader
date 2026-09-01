// Nuoc dong (song/gon song qua dinh + phan chieu fresnel) - thay the mau phang truoc day
// (StandardMaterial3D 1 mau) dung cho moi mat nuoc trong game (ho/song/dam lay, xem
// Main.BuildWaterRegion). Khong dung texture nao (dung ky thuat toan hoc thuan) de khong phu
// thuoc asset ngoai - dinh mesh (PlaneMesh) can DU luoi con (SubdivideWidth/Depth) de song hien
// ro, mat phang qua thua se lam song nhin "gay khuc".
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 shallow_color : source_color = vec4(0.32, 0.62, 0.68, 0.82);
uniform vec4 deep_color : source_color = vec4(0.05, 0.22, 0.36, 0.95);
uniform float wave_speed = 0.5;
uniform float wave_height = 1.6;
uniform float wave_frequency = 0.045;
uniform float roughness_val : hint_range(0.0, 1.0) = 0.06;
uniform float metallic_val : hint_range(0.0, 1.0) = 0.35;

varying float wave_h;

void vertex() {
    float t = TIME * wave_speed;
    // 2 lop song CHONG NHAU (tan so/huong khac nhau) de trong tu nhien hon 1 song sin don gian.
    float w1 = sin(VERTEX.x * wave_frequency + t) * cos(VERTEX.z * wave_frequency * 0.8 + t * 1.3);
    float w2 = sin((VERTEX.x + VERTEX.z) * wave_frequency * 1.7 - t * 1.6) * 0.5;
    wave_h = (w1 + w2) * wave_height;
    VERTEX.y += wave_h;
}

void fragment() {
    // Fresnel: nhin gan vuong goc (tu tren xuong) thay day nuoc (mau sau), nhin xieu/xa thay
    // phan chieu be mat nhieu hon (mau nong/sang hon) - dung y "nuoc that" thay vi 1 mau dong nhat.
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    vec3 color = mix(deep_color.rgb, shallow_color.rgb, fresnel);
    // Dinh song nhap nho sang hon 1 chut (gia lap "bot song") - chi dua vao do cao song da tinh
    // o vertex(), khong can texture bot rieng.
    float foam = smoothstep(1.0, 1.6, wave_h);
    color = mix(color, vec3(0.9, 0.95, 0.95), foam * 0.4);

    ALBEDO = color;
    ALPHA = mix(deep_color.a, shallow_color.a, fresnel);
    ROUGHNESS = roughness_val;
    METALLIC = metallic_val;
    SPECULAR = 0.6;
}
