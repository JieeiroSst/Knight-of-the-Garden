using Godot;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Cho nguoi choi (backend marketService) - 3 tab: Mua (tim theo ten, sap gia), Dang ban (chon do
    // trong tui + so luong + gia, do bi server giu ky quy), Tin cua toi (huy -> hoan do). Thue 5%
    // tru vao tien nguoi ban. Mo tai Bang Cho o thi tran (MarketBoard).
    public partial class MarketUI : CanvasLayer
    {
        private enum Tab { Browse, Sell, Mine }
        private Tab _tab = Tab.Browse;
        private VBoxContainer _content;
        private HBoxContainer _tabs;
        private LineEdit _search;
        private Label _gold;

        public override void _Ready()
        {
            AddToGroup("market_ui");
            var panel = UiStyle.MakePanel(new Vector2(150, 30), new Vector2(660, 480));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 12), CustomMinimumSize = new Vector2(624, 0) };
            panel.AddChild(vb);
            var head = new HBoxContainer();
            vb.AddChild(head);
            head.AddChild(UiStyle.Title(Loc.T("market.title")));
            _gold = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right };
            _gold.AddThemeColorOverride("font_color", UiStyle.Gold);
            head.AddChild(_gold);
            _tabs = new HBoxContainer();
            vb.AddChild(_tabs);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(624, 350) };
            vb.AddChild(scroll);
            _content = new VBoxContainer { CustomMinimumSize = new Vector2(608, 0) };
            scroll.AddChild(_content);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            GameManager.Instance.StatsChanged += () => _gold.Text = $"{GameManager.Instance.Gold}g";
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open()
        {
            Visible = true;
            _gold.Text = $"{GameManager.Instance.Gold}g";
            ShowTab(_tab);
        }

        private void ShowTab(Tab tab)
        {
            _tab = tab;
            UiStyle.Clear(_tabs);
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                var b = new Button { Text = Loc.T("market.tab." + t), ToggleMode = true, ButtonPressed = t == tab };
                var tt = t;
                b.Pressed += () => ShowTab(tt);
                _tabs.AddChild(b);
            }
            UiStyle.Clear(_content);
            if (tab == Tab.Browse) BuildBrowse();
            else if (tab == Tab.Sell) BuildSell();
            else LoadMine();
        }

        private void Fail(string text) => Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error")));

        // ==== Mua ====
        private void BuildBrowse()
        {
            var row = new HBoxContainer();
            _content.AddChild(row);
            _search = new LineEdit { PlaceholderText = Loc.T("market.search_hint"), CustomMinimumSize = new Vector2(360, 0) };
            row.AddChild(_search);
            var find = new Button { Text = Loc.T("market.search_btn") };
            row.AddChild(find);
            var results = new VBoxContainer();
            _content.AddChild(results);
            void Load()
            {
                string q = System.Uri.EscapeDataString(_search.Text.Trim());
                BackendClient.Instance.GetJson($"/api/market/listings?sort=price&q={q}", (ok, status, text) =>
                {
                    if (!ok || status != 200) { Fail(text); return; }
                    UiStyle.Clear(results);
                    using var doc = JsonDocument.Parse(text);
                    var list = doc.RootElement.EnumerateArray().ToList();
                    if (list.Count == 0) results.AddChild(UiStyle.Small(Loc.T("market.empty")));
                    foreach (var l in list)
                    {
                        int id = l.GetProperty("id").GetInt32();
                        string item = l.GetProperty("itemId").GetString();
                        int count = l.GetProperty("count").GetInt32();
                        long price = l.GetProperty("price").GetInt64();
                        string seller = l.GetProperty("sellerName").ValueKind == JsonValueKind.String ? l.GetProperty("sellerName").GetString() : "?";
                        var r = new HBoxContainer();
                        results.AddChild(r);
                        r.AddChild(new TextureRect
                        {
                            Texture = ItemDatabase.Instance.GetItemIcon(item), CustomMinimumSize = new Vector2(26, 26),
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        });
                        r.AddChild(new Label
                        {
                            Text = string.Format(Loc.T("market.row_fmt"), ItemDatabase.Instance.GetDisplayName(item), count, price, seller),
                            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true,
                        });
                        var buy = new Button { Text = Loc.T("market.buy_btn"), Disabled = GameManager.Instance.Gold < price };
                        buy.Pressed += () => BackendClient.Instance.PostJson($"/api/market/listings/{id}/buy", null, (ok2, st2, t2) =>
                        {
                            if (!ok2 || st2 != 200) { Fail(t2); return; }
                            Notify.Show(string.Format(Loc.T("market.bought_fmt"), ItemDatabase.Instance.GetDisplayName(item), count));
                            EconomyClient.Instance.RequestSync();
                            Load();
                        });
                        r.AddChild(buy);
                    }
                });
            }
            find.Pressed += Load;
            _search.TextSubmitted += _ => Load();
            Load();
        }

        // ==== Dang ban ====
        private void BuildSell()
        {
            _content.AddChild(UiStyle.Small(Loc.T("market.sell_hint")));
            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
            {
                var def = ItemDatabase.Instance.GetItem(id);
                if (def == null || def.Type == Data.ItemType.Quest) continue;
                int have = Inventory.Instance.CountOf(id);
                var r = new HBoxContainer();
                _content.AddChild(r);
                r.AddChild(new Label { Text = $"{ItemDatabase.Instance.GetDisplayName(id)} x{have}", CustomMinimumSize = new Vector2(220, 0), ClipText = true });
                var count = new SpinBox { MinValue = 1, MaxValue = Mathf.Min(999, have), Value = 1, CustomMinimumSize = new Vector2(80, 0) };
                r.AddChild(count);
                // Goi y gia = gia ban cua hang x so luong.
                var price = new SpinBox { MinValue = 1, MaxValue = 1_000_000_000, Value = Mathf.Max(1, def.SellPrice), CustomMinimumSize = new Vector2(120, 0), Suffix = "g" };
                count.ValueChanged += v => price.Value = Mathf.Max(1, def.SellPrice) * v;
                r.AddChild(price);
                var post = new Button { Text = Loc.T("market.post_btn") };
                post.Pressed += () =>
                {
                    var body = JsonSerializer.Serialize(new { itemId = id, count = (int)count.Value, price = (long)price.Value });
                    // Gui het thay doi chua dong bo truoc - server can thay dung so do trong tui.
                    EconomyClient.Instance.FlushNow(_ => BackendClient.Instance.PostJson("/api/market/listings", body, (ok, st, t) =>
                    {
                        if (!ok || st != 201) { Fail(t); return; }
                        Notify.Show(Loc.T("market.posted"));
                        EconomyClient.Instance.RequestSync();
                        ShowTab(Tab.Mine);
                    }));
                };
                r.AddChild(post);
            }
        }

        // ==== Tin cua toi ====
        private void LoadMine()
        {
            BackendClient.Instance.GetJson("/api/market/mine", (ok, status, text) =>
            {
                if (!ok || status != 200) { Fail(text); return; }
                UiStyle.Clear(_content);
                using var doc = JsonDocument.Parse(text);
                var list = doc.RootElement.EnumerateArray().ToList();
                if (list.Count == 0) _content.AddChild(UiStyle.Small(Loc.T("market.no_listings")));
                foreach (var l in list)
                {
                    int id = l.GetProperty("id").GetInt32();
                    string st = l.GetProperty("status").GetString();
                    var r = new HBoxContainer();
                    _content.AddChild(r);
                    r.AddChild(new Label
                    {
                        Text = string.Format(Loc.T("market.mine_row_fmt"), ItemDatabase.Instance.GetDisplayName(l.GetProperty("itemId").GetString()),
                            l.GetProperty("count").GetInt32(), l.GetProperty("price").GetInt64(), Loc.T("market.status." + st)),
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    });
                    if (st != "active") continue;
                    var cancel = new Button { Text = Loc.T("market.cancel_btn") };
                    cancel.Pressed += () => BackendClient.Instance.DeleteJson($"/api/market/listings/{id}", (ok2, st2, t2) =>
                    {
                        if (!ok2 || st2 != 200) { Fail(t2); return; }
                        EconomyClient.Instance.RequestSync();
                        LoadMine();
                    });
                    r.AddChild(cancel);
                }
            });
        }
    }
}
