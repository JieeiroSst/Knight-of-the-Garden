using Godot;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Lo Ren: tab REN (cong thuc Station="forge" - kiem/giap tu thoi kim loai, server kiem nguyen lieu
    // qua op craft) va tab CUONG HOA (+1..+10, server tung xuc xac ti le thanh cong - POST /api/forge/enhance).
    public partial class ForgeUI : CanvasLayer
    {
        private bool _enhanceTab;
        private VBoxContainer _list;
        private HBoxContainer _tabs;
        private Label _title;

        public override void _Ready()
        {
            AddToGroup("forge_ui");
            var panel = UiStyle.MakePanel(new Vector2(200, 30), new Vector2(560, 480));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 12), CustomMinimumSize = new Vector2(524, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title(Loc.T("forge.title"));
            vb.AddChild(_title);
            _tabs = new HBoxContainer();
            vb.AddChild(_tabs);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(524, 350) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(510, 0) };
            _list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open()
        {
            Visible = true;
            EnhanceClient.Refresh(Refresh);
            Refresh();
        }

        private void Refresh()
        {
            UiStyle.Clear(_tabs);
            foreach (var enh in new[] { false, true })
            {
                var b = new Button { Text = Loc.T(enh ? "forge.tab_enhance" : "forge.tab_smith"), ToggleMode = true, ButtonPressed = enh == _enhanceTab };
                bool e2 = enh;
                b.Pressed += () => { _enhanceTab = e2; Refresh(); };
                _tabs.AddChild(b);
            }
            UiStyle.Clear(_list);
            if (_enhanceTab) BuildEnhance(); else BuildSmith();
        }

        private static bool Unlocked(RecipeDef r) =>
            string.IsNullOrEmpty(r.UnlockSkill)
            || (System.Enum.TryParse<SkillType>(r.UnlockSkill, out var s) && SkillSystem.Instance.GetLevel(s) >= r.UnlockLevel);

        private void BuildSmith()
        {
            var db = ItemDatabase.Instance;
            foreach (var r in db.Recipes.Where(x => x.Station == "forge"))
            {
                var outDef = db.GetItem(r.OutputId);
                if (outDef == null) continue;
                bool unlocked = Unlocked(r);
                bool can = unlocked && r.Ingredients.All(i => Inventory.Instance.CountOf(i.ItemId) >= i.Count);
                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new TextureRect { Texture = db.GetItemIcon(r.OutputId), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(info);
                string stat = outDef.Type == ItemType.Weapon ? $"ATK {outDef.Damage}" : $"DEF {outDef.Defense}";
                var name = new Label { Text = $"{db.GetDisplayName(r.OutputId)}  ({stat})" };
                name.AddThemeColorOverride("font_color", can ? UiStyle.Text : UiStyle.Dim);
                info.AddChild(name);
                info.AddChild(UiStyle.Small(unlocked
                    ? string.Join(", ", r.Ingredients.Select(i => $"{db.GetDisplayName(i.ItemId)} {Inventory.Instance.CountOf(i.ItemId)}/{i.Count}"))
                    : string.Format(Loc.T("craft.locked_fmt"), Loc.T("skill." + r.UnlockSkill), r.UnlockLevel)));
                var btn = new Button { Text = Loc.T("forge.smith_btn"), Disabled = !can };
                var rr = r;
                btn.Pressed += () =>
                {
                    if (EconomyClient.Instance.Craft(rr, cooking: false))
                    {
                        AchievementSystem.Instance.Add("items_crafted");
                        Notify.Show(string.Format(Loc.T("craft.crafted_fmt"), db.GetDisplayName(rr.OutputId), 1));
                    }
                    Refresh();
                };
                row.AddChild(btn);
            }
        }

        private void BuildEnhance()
        {
            var db = ItemDatabase.Instance;
            _list.AddChild(UiStyle.Small(Loc.T("forge.enhance_hint")));
            var owned = Inventory.Instance.Slots.Select(s => s.ItemId).Concat(Backpack.Instance.Slots.Select(s => s.ItemId)).Distinct()
                .Where(id => db.GetItem(id)?.Type is ItemType.Weapon or ItemType.Armor or ItemType.Tool).ToList();
            foreach (var id in owned)
            {
                int lvl = EnhanceClient.Level(id);
                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new TextureRect { Texture = db.GetItemIcon(id), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(info);
                info.AddChild(new Label { Text = $"{db.GetDisplayName(id)} +{lvl}" });
                if (lvl >= EnhanceClient.Steps.Length) { info.AddChild(UiStyle.Small(Loc.T("forge.max"), UiStyle.Gold)); continue; }
                var step = EnhanceClient.Steps[lvl];
                bool can = GameManager.Instance.Gold >= step.gold && step.mats.All(m => EconomyClient.TotalOwned(m.id) >= m.n);
                info.AddChild(UiStyle.Small(string.Format(Loc.T("forge.cost_fmt"), (int)(step.chance * 100), step.gold,
                    string.Join(", ", step.mats.Select(m => $"{db.GetDisplayName(m.id)} {EconomyClient.TotalOwned(m.id)}/{m.n}"))) + (lvl >= 6 ? "  " + Loc.T("forge.risk") : "")));
                var btn = new Button { Text = Loc.T("forge.enhance_btn"), Disabled = !can };
                btn.Pressed += () => Enhance(id);
                row.AddChild(btn);
            }
        }

        private void Enhance(string itemId)
        {
            // Dong bo het thay doi kinh te truoc (server can thay du vat lieu), roi cuong hoa.
            EconomyClient.Instance.FlushNow(_ => BackendClient.Instance.PostJson("/api/forge/enhance", JsonSerializer.Serialize(new { itemId }), (ok, status, text) =>
            {
                if (!ok || status != 200) { Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error"))); return; }
                using var doc = JsonDocument.Parse(text);
                bool success = doc.RootElement.GetProperty("success").GetBoolean();
                int level = doc.RootElement.GetProperty("level").GetInt32();
                EnhanceClient.Set(itemId, level);
                Notify.Show(string.Format(Loc.T(success ? "forge.success_fmt" : "forge.fail_fmt"), ItemDatabase.Instance.GetDisplayName(itemId), level));
                EconomyClient.Instance.RequestSync();
                Inventory.Instance.EmitSignal(Inventory.SignalName.InventoryChanged);
                Refresh();
            }));
        }
    }
}
