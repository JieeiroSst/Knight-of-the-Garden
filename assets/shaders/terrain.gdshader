// Mat dat "nhu Trai Dat that": tron 8 lop vat lieu PBR that (ambientCG, CC0 - xem
// assets3d/textures/terrain) theo VUNG (ban do sinh canh do GroundMaterial ve tu cac vung cua the gioi:
// nui -> da/soi, rung -> tham la, dam lay -> bun, ho/song -> cat, lang/thanh pho -> dat/soi...)
// va theo NHIEU TU NHIEN (mang co xanh/co ua/dat tro loang lo nhu ngoai doi). Chuyen tiep giua cac
// lop theo DO CAO VAN BE MAT (soi/da "troi" len giua co, khong mo deu nhu pha mau), chong lap
// texture (2 lan lay mau xoay khac ti le), go dat co bong do that (phap tuyen tinh theo do doc).
//
// Toa do THE GIOI (khong phai UV cuc bo) -> San chinh, Thi tran va moi chunk WorldStreamer noi lien.
// San va cham van PHANG (xem ghi chu cu trong ground.gdshader) - go chi mang tinh thi giac.
shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

// 8 lop: 0 co xanh, 1 co lan dat, 2 dat kho, 3 dat bun, 4 tham la rung, 5 da reu, 6 cat, 7 soi.
// albedo.rgb + do cao (a); normal.rgb (OpenGL) + do nham (a).
uniform sampler2DArray layer_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray layer_normal : filter_linear_mipmap_anisotropic, repeat_enable;
// Kich thuoc 1 lan lap texture (don vi the gioi; nguoi cao ~40 = ~1.8m).
// Hang so (Godot 4.3 KHONG ho tro gia tri mac dinh cho uniform dang mang -> shader khong bien dich duoc).
const float layer_tile[8] = float[8](48.0, 70.0, 60.0, 60.0, 64.0, 140.0, 80.0, 36.0);

// Ban do sinh canh (trong so 8 lop, RGBA x2) phu len vung the gioi terrain_bounds (xy = goc, zw = kich thuoc).
global uniform sampler2D terrain_biome_a;
global uniform sampler2D terrain_biome_b;
global uniform vec4 terrain_bounds;
// Nong trai o toa do rieng (xem GroundMaterial.SetFarmCenters): xy = tam, z = nua canh, w = bat.
global uniform vec4 farm_c0;
global uniform vec4 farm_c1;
global uniform vec4 farm_c2;
global uniform vec4 farm_c3;

uniform float bump_height = 2.5;
uniform float bump_frequency = 0.02;

varying vec3 world_pos;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123); }

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	return vnoise(p) * 0.55 + vnoise(p * 2.03 + 17.0) * 0.3 + vnoise(p * 4.11 + 41.0) * 0.15;
}

// ==== Sinh canh thu tuc cua the gioi vo han ====
// CHEP Y HET scripts/core/WorldBiomes.cs (hash, nhieu, nguong) - de mat dat khop voi cay/da/quai
// do WorldStreamer sinh ra. Doi 1 ben thi PHAI doi ca ben kia.
float bhash(ivec2 c, uint seed) {
	uint h = (uint(c.x) * 2376512323u) ^ (uint(c.y) * 3625334849u) ^ (seed * 3407524639u);
	h ^= h >> 13u;
	h *= 2246822507u;
	h ^= h >> 16u;
	return float(h & 16777215u) / 16777215.0;
}

float bnoise(vec2 p, uint seed) {
	vec2 i = floor(p);
	vec2 f = p - i;
	ivec2 c = ivec2(i);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = bhash(c, seed);
	float b = bhash(c + ivec2(1, 0), seed);
	float cc = bhash(c + ivec2(0, 1), seed);
	float d = bhash(c + ivec2(1, 1), seed);
	float ab = a + (b - a) * u.x;
	float cd = cc + (d - cc) * u.x;
	return ab + (cd - ab) * u.y;
}

float bfbm(vec2 p, uint seed) {
	return bnoise(p, seed) * 0.6 + bnoise(p * 2.07 + vec2(5.3, 1.7), seed + 1u) * 0.28
		+ bnoise(p * 4.13 + vec2(9.1, 3.3), seed + 2u) * 0.12;
}

// Trong so 8 lop (co, co lan dat, dat, bun, la rung, da, cat, soi) cua sinh canh thu tuc.
void biome_proc(vec2 wxz, out vec4 wa, out vec4 wb) {
	// Plains (= vung hoang da cu)
	wa = vec4(0.50, 0.30, 0.10, 0.0);
	wb = vec4(0.06, 0.02, 0.0, 0.02);
	float wild = smoothstep(9500.0, 12500.0, length(wxz));
	if (wild <= 0.0) return;
	vec2 p = wxz / 5200.0;
	float t = bfbm(p, 11u);
	float m = bfbm(p + vec2(37.2, -19.4), 23u);
	float forest = smoothstep(0.54, 0.61, m) * (1.0 - smoothstep(0.5, 0.57, t));
	float marsh = smoothstep(0.54, 0.61, m) * smoothstep(0.5, 0.57, t);
	float barren = (1.0 - smoothstep(0.41, 0.48, m)) * smoothstep(0.52, 0.59, t);
	float high = (1.0 - smoothstep(0.38, 0.45, t)) * (1.0 - smoothstep(0.54, 0.61, m));
	float meadow = smoothstep(0.45, 0.5, t) * (1.0 - smoothstep(0.52, 0.59, t)) * (1.0 - smoothstep(0.47, 0.53, m));
	float plains = max(0.0, 1.0 - (forest + marsh + barren + high + meadow));
	vec4 pa = wa * plains
		+ vec4(0.28, 0.12, 0.0, 0.10) * forest
		+ vec4(0.08, 0.25, 0.08, 0.0) * high
		+ vec4(0.28, 0.05, 0.0, 0.50) * marsh
		+ vec4(0.06, 0.18, 0.26, 0.0) * barren
		+ vec4(0.30, 0.58, 0.08, 0.0) * meadow;
	vec4 pb = wb * plains
		+ vec4(0.45, 0.05, 0.0, 0.0) * forest
		+ vec4(0.0, 0.45, 0.0, 0.14) * high
		+ vec4(0.17, 0.0, 0.0, 0.0) * marsh
		+ vec4(0.0, 0.08, 0.36, 0.06) * barren
		+ vec4(0.0, 0.0, 0.0, 0.04) * meadow;
	wa = mix(wa, pa, wild);
	wb = mix(wb, pb, wild);
}

// Dat nong trai (= GroundMaterial.Farm) phu len vung vuong quanh 1 nong trai, vien mem 380.
void farm_blend(vec2 wxz, vec4 fc, inout vec4 wa, inout vec4 wb) {
	if (fc.w < 0.5) return;
	float d = max(abs(wxz.x - fc.x), abs(wxz.y - fc.y)) - fc.z;
	float m = 1.0 - smoothstep(-380.0, 380.0, d);
	if (m <= 0.001) return;
	wa = mix(wa, vec4(0.55, 0.22, 0.20, 0.0), m);
	wb = mix(wb, vec4(0.0, 0.0, 0.0, 0.03), m);
}

// Trong so goc tu ban do sinh canh (ngoai ban do = sinh canh thu tuc, xem biome_proc).
void biome(vec2 wxz, out vec4 wa, out vec4 wb) {
	vec2 uv = (wxz - terrain_bounds.xy) / max(terrain_bounds.zw, vec2(1.0));
	if (uv.x > 0.0 && uv.y > 0.0 && uv.x < 1.0 && uv.y < 1.0) {
		vec4 a = textureLod(terrain_biome_a, uv, 0.0);
		vec4 b = textureLod(terrain_biome_b, uv, 0.0);
		if (dot(a, vec4(1.0)) + dot(b, vec4(1.0)) > 0.05) { wa = a; wb = b; return; }
	}
	biome_proc(wxz, wa, wb);
	farm_blend(wxz, farm_c0, wa, wb);
	farm_blend(wxz, farm_c1, wa, wb);
	farm_blend(wxz, farm_c2, wa, wb);
	farm_blend(wxz, farm_c3, wa, wb);
}

float height_at(vec2 wxz, float amp) {
	return (fbm(wxz * bump_frequency) - 0.5) * 2.0 * amp;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec4 wa; vec4 wb;
	biome(world_pos.xz, wa, wb);
	// Vung da/soi go ghe ro hon, bai cat/bun bang phang hon.
	float amp = bump_height * (1.0 + 1.6 * wb.y + 0.6 * wb.w - 0.5 * wb.z - 0.4 * wa.w);
	float h = height_at(world_pos.xz, amp);
	VERTEX.y += h;
	// Phap tuyen theo do doc that cua go (sai phan huu han) - doi bong tu nhien theo huong mat troi.
	float e = 6.0;
	float hx = height_at(world_pos.xz + vec2(e, 0.0), amp) - height_at(world_pos.xz - vec2(e, 0.0), amp);
	float hz = height_at(world_pos.xz + vec2(0.0, e), amp) - height_at(world_pos.xz - vec2(0.0, e), amp);
	NORMAL = normalize(vec3(-hx, 2.0 * e, -hz));
	TANGENT = normalize(vec3(2.0 * e, hx, 0.0));
	BINORMAL = normalize(cross(NORMAL, TANGENT)) * -1.0;
	world_pos.y += h;
}

vec2 rot(vec2 v, float a) { float c = cos(a); float s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }

// Lay mau 1 lop chong lap: tron 2 lan lay mau (ti le/goc xoay khac nhau) theo nhieu vi mo.
void sample_layer(int i, vec2 wxz, float mix_t, out vec4 ah, out vec4 nr) {
	float tile = layer_tile[i];
	vec2 uv1 = wxz / tile;
	float ang = 1.1 + float(i) * 0.7;
	vec2 uv2 = rot(wxz, ang) / (tile * 2.37) + vec2(0.37, 0.71);
	vec4 a1 = texture(layer_albedo, vec3(uv1, float(i)));
	vec4 n1 = texture(layer_normal, vec3(uv1, float(i)));
	vec4 a2 = texture(layer_albedo, vec3(uv2, float(i)));
	vec4 n2 = texture(layer_normal, vec3(uv2, float(i)));
	// Phap tuyen cua lan lay mau xoay phai xoay nguoc lai ve truc the gioi.
	vec2 nxy = rot(n2.xy * 2.0 - 1.0, -ang);
	n2.xy = nxy * 0.5 + 0.5;
	ah = mix(a1, a2, mix_t);
	nr = mix(n1, n2, mix_t);
}

void fragment() {
	vec2 wxz = world_pos.xz;
	vec4 wa; vec4 wb;
	biome(wxz, wa, wb);
	float w[8] = float[8](wa.x, wa.y, wa.z, wa.w, wb.x, wb.y, wb.z, wb.w);

	// Loang lo tu nhien: moi lop 1 truong nhieu rieng (mang co/dat/soi chen nhau nhu ngoai doi).
	for (int i = 0; i < 8; i++) {
		// 2 tang: mang lon (~200 don vi) + chom nho (~50 don vi) - giong co/dat chen nhau ngoai doi.
		float n = 0.6 * fbm(wxz * (0.0045 + float(i) * 0.0007) + vec2(float(i) * 31.7, float(i) * 13.3))
			+ 0.4 * vnoise(wxz * 0.019 + vec2(float(i) * 7.1, float(i) * 3.9));
		w[i] = pow(max(w[i] * mix(0.1, 1.9, n), 0.0), 1.6);
	}
	// Chon 2 lop manh nhat tai diem nay (tiet kiem lay mau texture).
	int i1 = 0; int i2 = 1;
	if (w[1] > w[0]) { i1 = 1; i2 = 0; }
	for (int i = 2; i < 8; i++) {
		if (w[i] > w[i1]) { i2 = i1; i1 = i; }
		else if (w[i] > w[i2]) { i2 = i; }
	}
	float wsum = max(w[i1] + w[i2], 1e-4);
	float b2 = w[i2] / wsum;

	float mix_t = smoothstep(0.3, 0.7, vnoise(wxz * 0.012 + 7.0)) * 0.55;
	vec4 ah1; vec4 nr1; vec4 ah2; vec4 nr2;
	sample_layer(i1, wxz, mix_t, ah1, nr1);
	sample_layer(i2, wxz, mix_t, ah2, nr2);

	// Tron theo do cao van be mat: lop nao "cao" hon (soi, da, bui co) noi len tren.
	float ha = ah1.a + (1.0 - b2);
	float hb = ah2.a + b2;
	float depth = 0.18;
	float m = max(ha, hb) - depth;
	float k1 = max(ha - m, 0.0);
	float k2 = max(hb - m, 0.0);
	float t = k2 / max(k1 + k2, 1e-4);

	vec3 albedo = mix(ah1.rgb, ah2.rgb, t);
	vec4 nr = mix(nr1, nr2, t);
	// Soi trang qua sang so voi canh ngoai troi -> ha toi, nga am (soi dat that).
	if (i1 == 7 || i2 == 7) albedo = mix(albedo, albedo * vec3(0.62, 0.59, 0.53), (i1 == 7 ? 1.0 - t : t));

	// Bien thien mau dien rong (vung co xanh dam / nhat, dat am / kho) - pha vo cam giac lap.
	float macro = fbm(wxz * 0.0016 + 3.0);
	float macro2 = vnoise(wxz * 0.0007 + 11.0);
	albedo *= mix(0.86, 1.1, macro);
	vec3 dry = vec3(dot(albedo, vec3(0.45, 0.4, 0.15))) * vec3(1.08, 1.0, 0.72);
	albedo = mix(albedo, dry, smoothstep(0.55, 0.9, macro2) * 0.35);

	ALBEDO = albedo;
	NORMAL_MAP = nr.rgb;
	NORMAL_MAP_DEPTH = 1.0;
	ROUGHNESS = clamp(nr.a, 0.35, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.25;
}
