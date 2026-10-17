using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
	// NONG TRAI CUA NGUOI CHOI KHAC tai toa do rieng cua ho (cach nhau >= 5 km, xem backend farmService).
	//  - Moi ~20s hoi server cac nong trai trong ban kinh ~4 km -> ban do (MapUI), vung an toan
	//    (do nguy hiem quai), vung khong moc cay/khong sinh quai.
	//  - Di toi gan (< ~1.1 km): dung BAN SAO nong trai (nha, hang rao, chuong, cay, nen dat...) tu chinh
	//    nong trai cua minh, BO script/vat nuoi/NPC/cua/am thanh (chi la canh tinh co va cham) + bien ten chu.
	//  - Buoc vao trong tuong nong trai do: tu "tham" (VisitSystem) -> hien ruong that cua chu nha
	//    (doc tu server), ban be tuoi ho duoc. Ra khoi tuong -> ket thuc tham.
	public partial class FarmReplicas : Node
	{
		public class FarmInfo
		{
			public int Id;
			public string Name;
			public Vector3 Origin;
			public Vector3 Center => Origin + FarmSpace.LocalWallCenter;
			public Vector3 House => Origin + FarmSpace.LocalFarmhouse;
			public bool Contains(Vector3 p, float margin = 0f) => FarmSpace.InLocalWall(p - Origin, margin);
		}

		public static readonly Dictionary<int, FarmInfo> Known = new();
		public static FarmReplicas Instance { get; private set; }

		private const float PollRadius = 80000f;
		private const float CreateDist = 22000f, FreeDist = 30000f;
		private const int MaxReplicas = 3;

		private readonly Dictionary<int, Node3D> _replicas = new();
		private readonly HashSet<int> _zoned = new();
		private Node3D _template;
		private double _pollIn = 2.0, _checkIn;
		private bool _polling;
		private int _visitPending;

		public override void _EnterTree() => Instance = this;

		public override void _ExitTree()
		{
			if (Instance == this) Instance = null;
			Known.Clear();
			WorldStreamer.ExtraNoEnemyCenters.Clear();
			_template?.Free();
		}

		public static FarmInfo At(Vector3 p) => Known.Values.FirstOrDefault(f => f.Contains(p));

		public void PollSoon() => _pollIn = 0;

		public override void _Process(double delta)
		{
			var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
			if (player == null) return;
			_pollIn -= delta;
			if (_pollIn <= 0 && !_polling) { _pollIn = 20.0; Poll(player.GlobalPosition); }
			_checkIn -= delta;
			if (_checkIn > 0) return;
			_checkIn = 1.0;
			UpdateReplicas(player.GlobalPosition);
			UpdateVisit(player.GlobalPosition);
		}

		private void Poll(Vector3 at)
		{
			var net = NetworkClient.Instance;
			if (net == null || net.MyId <= 0) return;
			_polling = true;
			var path = $"/api/farms/near?x={Mathf.RoundToInt(at.X)}&z={Mathf.RoundToInt(at.Z)}&r={(int)PollRadius}";
			BackendClient.Instance.GetJson(path, (ok, status, text) =>
			{
				_polling = false;
				if (!ok || status != 200 || !IsInstanceValid(this)) return;
				try
				{
					using var doc = JsonDocument.Parse(text);
					foreach (var f in doc.RootElement.EnumerateArray())
					{
						int id = f.GetProperty("playerId").GetInt32();
						if (id == net.MyId) continue;
						var origin = new Vector3((float)f.GetProperty("x").GetDouble(), 0, (float)f.GetProperty("z").GetDouble());
						string name = f.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : "";
						if (string.IsNullOrEmpty(name)) name = $"#{id}";
						Known[id] = new FarmInfo { Id = id, Name = name, Origin = origin };
						RegisterZones(Known[id]);
					}
				}
				catch (System.Exception e) { GD.PushWarning($"FarmReplicas: {e.Message}"); }
				RebuildSafeCenters();
			});
		}

		// Vung khong moc cay/da + khong sinh quai quanh nong trai nguoi khac (giong nong trai minh).
		private void RegisterZones(FarmInfo f)
		{
			if (!_zoned.Add(f.Id)) return;
			WorldStreamer.ExclusionZones.Add((f.Center, FarmSpace.WallHalfSize * 1.45f));
			WorldStreamer.ExtraNoEnemyCenters.Add(f.Center);
		}

		private static void RebuildSafeCenters()
		{
			var safe = WorldBiomes.SafeCenters;
			safe.Clear();
			var wc = FarmSpace.ToWorld(FarmSpace.LocalWallCenter);
			safe.Add(new Vector2(wc.X, wc.Z));
			foreach (var f in Known.Values) safe.Add(new Vector2(f.Center.X, f.Center.Z));
		}

		private void UpdateReplicas(Vector3 p)
		{
			bool changed = false;
			foreach (var id in _replicas.Keys.ToList())
			{
				var node = _replicas[id];
				if (Known.TryGetValue(id, out var f) && Flat(p, f.Center) <= FreeDist && IsInstanceValid(node)) continue;
				if (IsInstanceValid(node)) node.QueueFree();
				if (f != null) WorldStreamer.RemoveFarmReserved(f.Origin);
				_replicas.Remove(id);
				changed = true;
			}
			foreach (var f in Known.Values.OrderBy(f => Flat(p, f.Center)))
			{
				if (_replicas.Count >= MaxReplicas || Flat(p, f.Center) > CreateDist) break;
				if (_replicas.ContainsKey(f.Id)) continue;
				var r = Create(f);
				if (r == null) break;
				_replicas[f.Id] = r;
				changed = true;
			}
			if (changed)
				GroundMaterial.SetFarmCenters(_replicas.Keys.Where(Known.ContainsKey).Select(id => new Vector2(Known[id].Center.X, Known[id].Center.Z)).ToList());
		}

		// Trong tuong nong trai nguoi khac -> tham tu dong (xem ruong that); ra ngoai -> ket thuc.
		private void UpdateVisit(Vector3 p)
		{
			var inside = At(p);
			if (inside != null)
			{
				if ((VisitSystem.Active && VisitSystem.HostId == inside.Id) || _visitPending == inside.Id) return;
				_visitPending = inside.Id;
				VisitSystem.Start(inside.Id, inside.Name, GetTree(), travel: false, onDone: () => _visitPending = 0);
				return;
			}
			if (VisitSystem.Active && Known.TryGetValue(VisitSystem.HostId, out var host) && !host.Contains(p, 300f))
				VisitSystem.End(GetTree());
		}

		private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

		private Node3D Create(FarmInfo f)
		{
			EnsureTemplate();
			if (_template == null) return null;
			var r = (Node3D)_template.Duplicate(0);
			r.Name = $"FarmReplica_{f.Id}";
			r.Position = f.Origin;
			r.AddChild(new Label3D
			{
				Text = string.Format(Loc.T("farm.owner_fmt"), f.Name) + "\n" + string.Format(Loc.T("farm.coord_fmt"),
					Mathf.RoundToInt(f.House.X / MapCoords.UnitsPerMeter), Mathf.RoundToInt(f.House.Z / MapCoords.UnitsPerMeter)),
				Position = FarmSpace.LocalFarmhouse + new Vector3(0, 280, 0),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 72, PixelSize = 0.6f, OutlineSize = 12, Modulate = new Color(1f, 0.92f, 0.7f),
				VisibilityRangeEnd = 9000f,
			});
			GetTree().CurrentScene.AddChild(r);
			WorldStreamer.AddFarmReserved(f.Origin);
			return r;
		}

		// ==== Mau ban sao (dung 1 lan tu chinh nong trai cua minh) ====
		private static bool Exclude(Node n) =>
			n is CharacterBody3D || n is RigidBody3D || n is Area3D || n is VehicleBody3D
			|| n is AudioStreamPlayer3D || n is AudioStreamPlayer || n is GpuParticles3D || n is CpuParticles3D
			|| n is NavigationRegion3D || n is NavigationAgent3D || n is Camera3D || n is OmniLight3D || n is SpotLight3D
			|| n is FarmPlot || n.IsInGroup("placed_objects") || n.IsInGroup("scarecrows");

		private void EnsureTemplate()
		{
			if (_template != null) return;
			var t = new Node3D { Name = "FarmReplicaTemplate" };
			int count = 0;
			foreach (var n in Main.FarmNodes)
			{
				if (!IsInstanceValid(n) || Exclude(n)) continue;
				if (n.Duplicate(0) is not Node3D c) continue;
				Prune(n, c);
				c.Position = n.Position - FarmSpace.Origin;
				t.AddChild(c);
				count++;
			}
			// Nen dat o ruong luoi co dinh (ruong that cua chu nha hien khi buoc vao - VisitSystem).
			var soil = new BoxMesh { Size = new Vector3(76, 1.2f, 76) };
			var soilMat = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.25f, 0.16f), Roughness = 1f };
			foreach (Node n in GetTree().GetNodesInGroup("farm_plots"))
				if (n is FarmPlot fp && fp.GridX >= 0 && !fp.IsVisitTemp)
					t.AddChild(new MeshInstance3D { Mesh = soil, MaterialOverride = soilMat, Position = fp.GlobalPosition - FarmSpace.Origin + Vector3.Up * 0.3f });
			_template = count > 0 ? t : null;
			if (_template == null) t.Free();
			GD.Print($"FarmReplicas: mau ban sao {count} node");
		}

		// Duplicate (khong co co Scripts) giu dung thu tu con -> duyet song song de bo cac con khong muon.
		private static void Prune(Node orig, Node copy)
		{
			var oc = orig.GetChildren();
			var cc = copy.GetChildren();
			if (oc.Count != cc.Count) return;
			for (int i = oc.Count - 1; i >= 0; i--)
			{
				if (Exclude(oc[i])) { copy.RemoveChild(cc[i]); cc[i].Free(); }
				else Prune(oc[i], cc[i]);
			}
		}
	}
}
