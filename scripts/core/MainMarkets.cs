using Godot;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
	// Khu cho rai khap ban do (data/markets.json - toa do co dinh, dung chung voi server). Moi cho: san
	// lat da, cac sap hang mai vai, Thuong Lai (NPC ban mui ten/cung/thuoc/vat lieu), Bang Cho Nguoi
	// Choi (mua ban voi nguoi khac) va den long. Ban kinh SafeRadius quanh cho la VUNG AN TOAN (server
	// chan danh nguoi/pha/xay) - noi hen gap, giao dich. Cho thi tran (Built = false) dung cho co san.
	public partial class Main
	{
		private static readonly string[] MarketShopItems =
		{
			"mui_ten", "cung_go", "potion", "da", "fertilizer_basic", "thucan_giasuc",
			"wheat_seed", "carrot_seed", "potato_seed", "tomato_seed", "pumpkin_seed", "cabbage_seed",
		};

		private void BuildMarkets()
		{
			foreach (var m in ItemDatabase.Instance.Markets)
			{
				var pos = new Vector3(m.X, 0, m.Z);
				WorldStreamer.ExclusionZones.Add((pos, 260f)); // khong moc cay/da/quai giua cho
				if (!m.Built) continue;
				BuildMarketSite(m, pos);
			}
		}

		private void BuildMarketSite(MarketDef m, Vector3 pos)
		{
			var site = new Node3D { Name = "Market_" + m.Id, Position = pos };
			_world.AddChild(site);
			var rng = new RandomNumberGenerator { Seed = (ulong)m.Id.GetHashCode() };

			// San lat da + va cham rieng (NPC/nguoi choi dung duoc ke ca truoc khi chunk dat kip tai).
			site.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 190f, BottomRadius = 196f, Height = 2f, RadialSegments = 40 },
				Position = Vector3.Up * 0.4f,
				MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.52f, 0.47f), Roughness = 1f },
			});
			var floor = new StaticBody3D();
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(400, 20, 400) }, Position = Vector3.Down * 10f });
			site.AddChild(floor);

			// Sap hang: vong tron quanh quang truong, mai vai nhieu mau.
			Color[] cloth = { new(0.75f, 0.2f, 0.18f), new(0.2f, 0.45f, 0.7f), new(0.85f, 0.7f, 0.25f), new(0.3f, 0.6f, 0.3f), new(0.6f, 0.3f, 0.6f), new(0.9f, 0.5f, 0.2f) };
			for (int i = 0; i < 6; i++)
			{
				float a = Mathf.Tau * i / 6f + 0.3f;
				var stall = BuildStall(cloth[i % cloth.Length], rng);
				stall.Position = new Vector3(Mathf.Cos(a) * 130f, 0, Mathf.Sin(a) * 130f);
				stall.Rotation = new Vector3(0, -a + Mathf.Pi / 2f, 0);
				site.AddChild(stall);
			}

			// Thuong Lai cho (moi cho 1 NPC rieng - than thiet tinh rieng theo NpcId).
			var trader = _npcScene.Instantiate<NPC>();
			trader.NpcId = "trader_" + m.Id;
			trader.NpcName = Loc.Current == Loc.Lang.VI ? "Thương Lái " + m.Name : m.NameEn + " Trader";
			trader.ShopItems = MarketShopItems;
			trader.DialogueLow = new[] { "Mũi tên, cung, thuốc... đủ cả! Ở chợ không ai dám động thủ đâu." };
			trader.DialogueMid = new[] { "Lại gặp cậu! Hàng mới về đây." };
			trader.DialogueHigh = new[] { "Khách ruột của ta - cứ chọn thoải mái." };
			trader.DialogueLowEn = new[] { "Arrows, bows, potions... I have it all! Nobody fights in the market." };
			trader.DialogueMidEn = new[] { "You again! Fresh stock just arrived." };
			trader.DialogueHighEn = new[] { "My best customer - take your pick." };
			trader.Position = pos + new Vector3(0, 0, -40f);
			_world.AddChild(trader);

			var board = new MarketBoard { Position = pos + new Vector3(55f, 0, 25f) };
			_world.AddChild(board);

			for (int i = 0; i < 4; i++)
			{
				float a = Mathf.Tau * i / 4f + Mathf.Pi / 4f;
				var lp = new Vector3(Mathf.Cos(a) * 80f, 0, Mathf.Sin(a) * 80f);
				site.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.4f, BottomRadius = 1.8f, Height = 70f }, Position = lp + Vector3.Up * 35f,
					MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.22f, 0.15f) } });
				site.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 5f, Height = 10f }, Position = lp + Vector3.Up * 72f,
					MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.45f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.35f), EmissionEnergyMultiplier = 2.5f } });
				site.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.75f, 0.45f), LightEnergy = 1.3f, OmniRange = 140f, Position = lp + Vector3.Up * 70f,
					DistanceFadeEnabled = true, DistanceFadeBegin = 900f, DistanceFadeLength = 200f });
			}

			string label = (Loc.Current == Loc.Lang.VI ? m.Name : m.NameEn) + "  " + MapCoords.Format(m.X, m.Z);
			AddBuildingLabelZone(pos, m.SafeRadius, label);
			GraphicsSettings.SetVisibilityRange(site, 3500f);
		}

		private static Node3D BuildStall(Color cloth, RandomNumberGenerator rng)
		{
			var stall = new Node3D();
			var wood = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.17f), Roughness = 1f };
			for (int i = -1; i <= 1; i += 2)
				for (int j = -1; j <= 1; j += 2)
					stall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.5f, 46f, 2.5f) }, Position = new Vector3(i * 28f, 23f, j * 16f), MaterialOverride = wood });
			stall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(56f, 14f, 10f) }, Position = new Vector3(0, 7f, 12f), MaterialOverride = wood }); // quay hang
			stall.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(64f, 12f, 40f) }, Position = new Vector3(0, 52f, 0),
				MaterialOverride = new StandardMaterial3D { AlbedoColor = cloth, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
			});
			// Hang hoa tren quay (hop/thung mau ngau nhien).
			for (int k = 0; k < 4; k++)
				stall.AddChild(new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = Vector3.One * rng.RandfRange(5f, 8f) },
					Position = new Vector3(-20f + k * 13f, 17f, 12f),
					MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHsv(rng.Randf(), 0.5f, 0.8f) },
				});
			var col = new StaticBody3D();
			col.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(56f, 14f, 10f) }, Position = new Vector3(0, 7f, 12f) });
			stall.AddChild(col);
			return stall;
		}
	}
}
