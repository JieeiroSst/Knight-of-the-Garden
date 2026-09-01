// Mat dat co GO NHE THAT (noise, khong con la 1 mat phang tuyet doi) - truoc day San chinh/moi
// chunk hoang da la 1 PlaneMesh HOAN TOAN PHANG (0 phan chia, chi 4 dinh goc) chi dua vao texture
// mau+normal de "gia" chat lieu, nen tu tren nhin xuong van thay ro rang la 1 tam phang cung.
// Shader nay them nhap nho hinh hoc THAT (khong chi anh sang gia qua normal map) qua noise theo
// toa do THE GIOI (khong phai UV cuc bo) de San chinh va cac chunk WorldStreamer noi lien mach,
// khong co duong gay giua cac o rieng biet.
//
// QUAN TRONG: bien do RAT NHO (bump_height mac dinh 2.5 don vi) CO CHU DICH - san va cham
// (StaticBody3D) cua nguoi choi/NPC van la 1 mat phang TUYET DOI (khong doi theo shader nay,
// doi collision that theo se can HeightMapShape3D + dong bo lai HANG NGAN vi tri da dat san
// trong game - rui ro qua lon). Bien do nho giu do "gia lap khong bang phang" chi mang tinh THI
// GIAC, khong du lon de nguoi choi/vat the nhin thay "noi/chim" ro ret so voi mat dat.
shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_texture : source_color;
uniform sampler2D normal_texture : hint_normal;
uniform sampler2D roughness_texture : hint_roughness_g;
uniform vec2 uv_scale = vec2(1.0, 1.0);
uniform float bump_height = 2.5;
uniform float bump_frequency = 0.02;
uniform float specular_val = 0.05;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

// Value noise don gian (khong can texture noise rieng) - lien tuc/muot, khop ca 2 huong X/Z.
float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

void vertex() {
    // Dung toa do THE GIOI (MODEL_MATRIX * VERTEX), KHONG phai VERTEX cuc bo - de San chinh va
    // moi chunk hoang da (moi cai co goc toa do rieng) cung "doc" chung 1 truong noise lien tuc,
    // khong bi lech pha/gay khuc tai duong ranh giua 2 mat.
    vec2 world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    float n1 = value_noise(world_xz * bump_frequency);
    float n2 = value_noise(world_xz * bump_frequency * 2.3 + vec2(37.0, 91.0)) * 0.4;
    // Tru ~0.7 (gan trung binh cua n1+n2) de mat dat KHONG bi "noi" dan len tren so voi san va
    // cham phang - chi dao dong quanh Y=0 goc, khong dich chuyen he thong.
    VERTEX.y += (n1 + n2 - 0.7) * bump_height;
}

void fragment() {
    vec2 uv = UV * uv_scale;
    ALBEDO = texture(albedo_texture, uv).rgb;
    NORMAL_MAP = texture(normal_texture, uv).rgb;
    ROUGHNESS = texture(roughness_texture, uv).r;
    METALLIC = 0.0;
    SPECULAR = specular_val;
}
