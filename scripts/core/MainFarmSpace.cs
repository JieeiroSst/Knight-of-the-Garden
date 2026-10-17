using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
	// Dua nong trai cua minh toi TOA DO RIENG do server cap (xem FarmSpace, backend farmService):
	// the gioi van dung nhu cu (nong trai quanh goc), sau do moi node nam trong vung nong trai duoc doi
	// toi FarmSpace.Origin. Thi tran / khu vuc / cho / the gioi vo han giu nguyen.
	public partial class Main
	{
		// Cac node cap cao nhat thuoc nong trai (da doi toi Origin) - dung lam mau ban sao nong trai nguoi khac.
		public static readonly List<Node3D> FarmNodes = new();
		private bool _farmRelocated;

		// KHONG Reparent: doi cha lam node ra/vao tree -> _ExitTree cua bo/ga/cuu... huy dang ky su kien gio
		// (dang ky lai chi trong _Ready, khong chay lai). Chi cong Origin vao Position cua tung node.
		private void RelocateFarm()
		{
			// Thu nghiem (khong dang nhap): HSVV_FARM_ORIGIN="x,z".
			var dbg = OS.GetEnvironment("HSVV_FARM_ORIGIN");
			if (FarmSpace.Origin == Vector3.Zero && !string.IsNullOrEmpty(dbg) && dbg.Split(',') is { Length: 2 } xz
				&& float.TryParse(xz[0], out var dx) && float.TryParse(xz[1], out var dz))
				FarmSpace.Origin = new Vector3(dx, 0, dz);
			var origin = FarmSpace.Origin;
			FarmNodes.Clear();
			foreach (Node child in _world.GetChildren()) CollectFarm(child);
			// Mot so thu sinh thang vao Main (ngua da thuan hoa...) - chi xet 1 lan luc doi.
			foreach (Node child in GetChildren())
				if (child != _world && child is not DirectionalLight3D && child is Node3D c3 && c3.Position.LengthSquared() >= 1f) CollectFarm(child);
			int shifted = 0;
			// Gia tri tuyet doi (dat lai moi lan vao game - bien tinh con tu phien truoc).
			WorldStreamer.MoveFarmReservedZone(origin);
			HorseRegistry.HomePos = HorseRegistry.LocalHomePos + origin;
			if (origin != Vector3.Zero)
			{
				foreach (var n in FarmNodes) { n.Position += origin; shifted += ShiftTree(n, origin); }
				ShiftFarmGlobals(origin);
				// Nguoi choi (khong tinh vao nong trai) cung doi theo.
				foreach (Node n in GetTree().GetNodesInGroup("player")) if (n is Node3D p) p.GlobalPosition += origin;
			}
			_farmRelocated = true;
			// Cac thu SINH THEM luc choi (be con, quai, bu nhin...) bang toa do nong trai cu -> doi theo luon.
			_world.ChildEnteredTree += OnWorldChildEntered;
			WorldBiomes.SafeCenters.Clear();
			var wc = FarmSpace.ToWorld(FarmSpace.LocalWallCenter);
			WorldBiomes.SafeCenters.Add(new Vector2(wc.X, wc.Z));
			AddChild(new FarmReplicas { Name = "FarmReplicas" });
			GroundMaterial.SetFarmCenters();
			if (origin != Vector3.Zero)
			{
				var home = FarmSpace.ToWorld(FarmSpace.LocalFarmhouse);
				Callable.From(() => UI.Notify.Show(string.Format(Loc.T("farm.my_coord_fmt"),
					Mathf.RoundToInt(home.X / MapCoords.UnitsPerMeter), Mathf.RoundToInt(home.Z / MapCoords.UnitsPerMeter)))).CallDeferred();
			}
			GD.Print($"FarmSpace: nong trai tai {origin} - doi {FarmNodes.Count} node, {shifted} truong vi tri.");
		}

		private static bool IsSharedRoot(Node n) =>
			n is WorldStreamer || n is DayNightCycle || n is Player || n.IsInGroup("player");

		// Node3D o vi tri trong vung nong trai -> doi ca node. Node "hop chua" dat o goc toa do -> xet
		// tung con (hop chua co ca do nong trai lan do khu chung thi tach rieng tung con).
		private static void CollectFarm(Node n)
		{
			if (IsSharedRoot(n) || n is not Node3D n3) return;
			bool atOrigin = n3.Position.LengthSquared() < 1f && n3.Basis.IsEqualApprox(Basis.Identity);
			var kids = n3.GetChildren().OfType<Node3D>().ToList();
			if (!atOrigin || kids.Count == 0)
			{
				if (FarmSpace.InLocalFarmArea(n3.Position)) FarmNodes.Add(n3);
				return;
			}
			if (kids.All(k => FarmSpace.InLocalFarmArea(k.Position))) { FarmNodes.Add(n3); return; }
			foreach (var k in kids) CollectFarm(k);
		}

		private static int ShiftTree(Node n, Vector3 by)
		{
			int c = FarmSpace.ShiftAbsoluteFields(n, by, FarmSpace.IsLocalFarmPos);
			foreach (Node k in n.GetChildren()) c += ShiftTree(k, by);
			return c;
		}

		// Phat ra khi node VUA vao tree (truoc _Ready cua no) -> doi vi tri + cac truong dich da gan san.
		private void OnWorldChildEntered(Node n)
		{
			if (!_farmRelocated || FarmSpace.Origin == Vector3.Zero || IsSharedRoot(n) || n is not Node3D n3) return;
			if (!FarmSpace.InLocalFarmArea(n3.Position) || n3.Position.LengthSquared() < 1f) return;
			n3.Position += FarmSpace.Origin;
			ShiftTree(n3, FarmSpace.Origin);
		}

		// Cac dang ky TOAN CUC dung toa do nong trai cu.
		private void ShiftFarmGlobals(Vector3 by)
		{
			var ez = WorldStreamer.ExclusionZones;
			for (int i = 0; i < ez.Count; i++)
				if (FarmSpace.InLocalFarmArea(ez[i].Center)) ez[i] = (ez[i].Center + by, ez[i].Radius);
			if (WorldStreamer.NoEnemyZoneCenter is Vector3 ne && FarmSpace.InLocalFarmArea(ne)) WorldStreamer.NoEnemyZoneCenter = ne + by;
			ZoneMap.FarmCenter += by;
			VisitSystem.FarmCenter += by;
			var sp = Scarecrow.ExtraPoints;
			for (int i = 0; i < sp.Count; i++) if (FarmSpace.InLocalFarmArea(sp[i])) sp[i] += by;
			if (FarmSpace.InLocalFarmArea(NpcEconomy.RestockPos)) NpcEconomy.RestockPos += by;
			var adv = AdventureSystem.Instance;
			if (adv != null)
				for (int i = 0; i < adv.Pois.Count; i++)
				{
					var p = adv.Pois[i];
					if (FarmSpace.InLocalFarmArea(p.pos)) adv.Pois[i] = (p.kind, p.id, p.pos + by, p.labelKey);
				}
		}
	}
}
