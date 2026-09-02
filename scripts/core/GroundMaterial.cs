using Godot;

namespace HiepSiVeVuon.Core
{
    // Vat lieu co/dat that (ambientCG, CC0) dung chung cho mat dat San Main va cac chunk WorldStreamer.
    public static class GroundMaterial
    {
        private const string GrassColorPath = "res://assets3d/textures/grass/grass_color.jpg";
        private const string GrassNormalPath = "res://assets3d/textures/grass/grass_normal.jpg";
        private const string GrassRoughnessPath = "res://assets3d/textures/grass/grass_roughness.jpg";

        private const string EarthColorPath = "res://assets3d/textures/ground/ground_color.jpg";
        private const string EarthNormalPath = "res://assets3d/textures/ground/ground_normal.jpg";
        private const string EarthRoughnessPath = "res://assets3d/textures/ground/ground_roughness.jpg";

        private const float TileSize = 70f; // 1 lan lap texture ~ 70 don vi the gioi

        private static Texture2D _grassColor, _grassNormal, _grassRoughness;
        private static Texture2D _earthColor, _earthNormal, _earthRoughness;
        private static Shader _groundShader;

        // Do sau khoi dat dac ben duoi lop co (xem CreateEarthMass).
        public const float EarthDepth = 400f;

        // So lan chia luoi (Subdivide) can co tren PlaneMesh de shader GO NHE (ground.gdshader)
        // co du dinh de tao bum muot - mat KHONG chia (mac dinh) chi co 4 dinh goc, shader se
        // khong the "uon" gi ca. San chinh va chunk WorldStreamer dung ham nay (khac kich thuoc)
        // de tinh so o luoi hop ly (~30-50 don vi/o, du muot ma khong qua nhieu dinh).
        public static int SubdivisionsFor(float sizeUnits) => Mathf.Clamp((int)(sizeUnits / 40f), 8, 80);

        // Luu y: khong nhan tham so tint/mau rieng - moi mat dat (San chinh va tat ca chunk)
        // phai dung dung 1 texture giong het nhau, neu khong se tao duong ranh sang giua
        // cac o dat rieng biet (da tung xay ra khi WorldStreamer tung tint ngau nhien tung chunk).
        //
        // ShaderMaterial (thay StandardMaterial3D truoc day) de them go nhe hinh hoc THAT qua
        // vertex shader (xem ground.gdshader) - PlaneMesh goi ham nay PHAI co Subdivide du (xem
        // SubdivisionsFor) de bum hien ra duoc.
        //
        // bumpHeight: MAC DINH 2.5 (rat nhe, dung cho San chinh/Thi tran - dat nong trai da duoc
        // "san phang" de canh tac, khong nen gap ghenh). Vung hoang da (WorldStreamer.GenerateChunk)
        // truyen gia tri LON HON (xem WildernessBumpHeight) de tao cam giac "dia hinh tu nhien gap
        // gho" ro ret hon, theo dung yeu cau "ngoai nong trai thi mat dat gap gho". Van GIU CHUNG 1
        // ham NOISE trong shader (chi doi bien do), nen bien dang van lien tuc/muot giua cac o luoi
        // ke nhau du bumpHeight khac nhau - CHI co the "bac thang" o ranh gioi giua 2 vung dung
        // bumpHeight khac nhau (vd tuong da nong trai), tuong tu tuong da von da la 1 ranh gioi thi
        // giac ro rang san co, khong tao cam giac loi.
        public static ShaderMaterial CreateGrass(float worldWidth, float worldDepth, float bumpHeight = 2.5f)
        {
            _grassColor ??= GD.Load<Texture2D>(GrassColorPath);
            _grassNormal ??= GD.Load<Texture2D>(GrassNormalPath);
            _grassRoughness ??= GD.Load<Texture2D>(GrassRoughnessPath);
            _groundShader ??= GD.Load<Shader>("res://assets/shaders/ground.gdshader");

            var mat = new ShaderMaterial { Shader = _groundShader };
            mat.SetShaderParameter("albedo_texture", _grassColor);
            mat.SetShaderParameter("normal_texture", _grassNormal);
            mat.SetShaderParameter("roughness_texture", _grassRoughness);
            mat.SetShaderParameter("uv_scale", new Vector2(worldWidth / TileSize, worldDepth / TileSize));
            // Co that khong bong: giam phan xa Fresnel goc hep, tranh hien tuong ca dam co "anh
            // len" mau troi thanh mot dai sang xanh o duong chan troi.
            mat.SetShaderParameter("specular_val", 0.05f);
            mat.SetShaderParameter("bump_height", bumpHeight);
            return mat;
        }

        // Khoi dat dac (texture dat/da that, ambientCG, CC0) nam ngay duoi mat co - de nhin tu
        // bat ky goc nao (ke ca gan ngang tam mat) van thay dat that co chieu sau va chat lieu
        // dat that, khong phai mot mat phang mong mau don "lo lung tren troi".
        public static MeshInstance3D CreateEarthMass(float width, float depth)
        {
            _earthColor ??= GD.Load<Texture2D>(EarthColorPath);
            _earthNormal ??= GD.Load<Texture2D>(EarthNormalPath);
            _earthRoughness ??= GD.Load<Texture2D>(EarthRoughnessPath);

            var mat = new StandardMaterial3D
            {
                AlbedoTexture = _earthColor,
                NormalEnabled = _earthNormal != null,
                NormalTexture = _earthNormal,
                RoughnessTexture = _earthRoughness,
                RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Grayscale,
                Uv1Scale = new Vector3(width / TileSize, EarthDepth / TileSize, depth / TileSize),
                Roughness = 1f,
                MetallicSpecular = 0.05f
            };

            return new MeshInstance3D
            {
                Name = "EarthMass",
                Mesh = new BoxMesh { Size = new Vector3(width, EarthDepth, depth) },
                MaterialOverride = mat,
                Position = new Vector3(0, -EarthDepth / 2f - 0.5f, 0)
            };
        }
    }
}
