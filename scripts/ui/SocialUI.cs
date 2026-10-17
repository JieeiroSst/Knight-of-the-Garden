using Godot;
using System;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Cua so xa hoi [O]: Ban be (online, ket ban, tham nong trai) / Bang hoi (lap, moi, thanh vien, kho
    // chung) / Hang ngay (diem danh + nhiem vu ngay/tuan - server kiem tra tien do) / Xep hang.
    public partial class SocialUI : CanvasLayer
    {
        public enum Tab { Friends, Guild, Daily, Bounty, Ranking }
        private Tab _tab = Tab.Friends;
        private VBoxContainer _content;
        private HBoxContainer _tabs;
        private string _rankStat = "gold";

        public override void _Ready()
        {
            AddToGroup("social_ui");
            var panel = UiStyle.MakePanel(new Vector2(130, 24), new Vector2(700, 492));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 12), CustomMinimumSize = new Vector2(664, 0) };
            panel.AddChild(vb);
            vb.AddChild(UiStyle.Title(Loc.T("social.title")));
            _tabs = new HBoxContainer();
            vb.AddChild(_tabs);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(664, 360) };
            vb.AddChild(scroll);
            _content = new VBoxContainer { CustomMinimumSize = new Vector2(648, 0) };
            _content.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_content);
            var close = new Button { Text = Loc.T("social.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            NetworkClient.Instance.SocialEvent += (type, _) => { if (Visible && type is "friend_request" or "friend_accepted" or "guild_invite" or "guild_changed") Refresh(); };
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (ChatUI.IsTyping) return;
            if (e.IsActionPressed("toggle_social")) { if (Visible) Visible = false; else Open(_tab); GetViewport().SetInputAsHandled(); }
            else if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open(Tab tab)
        {
            _tab = tab;
            Visible = true;
            Refresh();
        }

        private void Refresh()
        {
            UiStyle.Clear(_tabs);
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var b = new Button { Text = Loc.T("social.tab." + t), ToggleMode = true, ButtonPressed = t == _tab };
                var tt = t;
                b.Pressed += () => { _tab = tt; Refresh(); };
                _tabs.AddChild(b);
            }
            UiStyle.Clear(_content);
            _content.AddChild(UiStyle.Small(Loc.T("social.loading")));
            switch (_tab)
            {
                case Tab.Friends: LoadFriends(); break;
                case Tab.Guild: LoadGuild(); break;
                case Tab.Daily: LoadDaily(); break;
                case Tab.Ranking: LoadRanking(); break;
                case Tab.Bounty: LoadBounty(); break;
            }
        }

        // ==== Tien ich goi API ====
        private void Get(string path, Action<JsonElement> onOk) =>
            BackendClient.Instance.GetJson(path, (ok, status, text) => Handle(ok, status, text, onOk));

        private void Post(string path, object body, Action<JsonElement> onOk, bool refresh = true) =>
            BackendClient.Instance.PostJson(path, body == null ? null : JsonSerializer.Serialize(body), (ok, status, text) =>
                Handle(ok, status, text, e => { onOk?.Invoke(e); if (refresh) Refresh(); }));

        private void Handle(bool ok, int status, string text, Action<JsonElement> onOk)
        {
            if (!ok || status < 200 || status >= 300) { Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error"))); return; }
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(text) ? "{}" : text);
            onOk?.Invoke(doc.RootElement.Clone());
        }

        private static Label Line(string text, Color? color = null)
        {
            var l = new Label { Text = text, ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (color is Color c) l.AddThemeColorOverride("font_color", c);
            return l;
        }

        private HBoxContainer InputRow(string placeholder, string button, Action<string> onSubmit)
        {
            var row = new HBoxContainer();
            var input = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(260, 0), MaxLength = 24 };
            var btn = new Button { Text = button };
            btn.Pressed += () => { if (input.Text.Trim().Length > 0) onSubmit(input.Text.Trim()); };
            input.TextSubmitted += t => { if (t.Trim().Length > 0) onSubmit(t.Trim()); };
            row.AddChild(input);
            row.AddChild(btn);
            return row;
        }

        // ==== Ban be ====
        private void LoadFriends() => Get("/api/friends", list =>
        {
            UiStyle.Clear(_content);
            _content.AddChild(InputRow(Loc.T("social.friend_name"), Loc.T("social.add_friend"), name =>
                Post("/api/friends", new { name }, e => Notify.Show(e.GetProperty("status").GetString() == "accepted" ? Loc.T("social.now_friends") : Loc.T("social.request_sent")))));
            var arr = list.EnumerateArray().ToList();
            if (arr.Count == 0) _content.AddChild(UiStyle.Small(Loc.T("social.no_friends")));
            foreach (var f in arr.OrderByDescending(x => x.GetProperty("online").GetBoolean()))
            {
                int id = f.GetProperty("playerId").GetInt32();
                string name = f.GetProperty("displayName").GetString();
                bool online = f.GetProperty("online").GetBoolean();
                string st = f.GetProperty("status").GetString();
                var row = new HBoxContainer();
                _content.AddChild(row);
                row.AddChild(Line((online ? "● " : "○ ") + name, online ? new Color(0.5f, 1f, 0.55f) : UiStyle.Dim));
                if (st == "pending")
                {
                    if (f.GetProperty("incoming").GetBoolean())
                    {
                        var acc = new Button { Text = Loc.T("social.accept") };
                        acc.Pressed += () => Post($"/api/friends/{id}/accept", null, null);
                        row.AddChild(acc);
                    }
                    else row.AddChild(UiStyle.Small(Loc.T("social.waiting")));
                }
                else
                {
                    var visit = new Button { Text = Loc.T("social.visit_farm") };
                    visit.Pressed += () => { Visible = false; VisitSystem.Start(id, name, GetTree()); };
                    row.AddChild(visit);
                }
                var rm = new Button { Text = "x" };
                rm.Pressed += () => BackendClient.Instance.DeleteJson($"/api/friends/{id}", (ok, s2, t2) => Refresh());
                row.AddChild(rm);
            }
            if (VisitSystem.Active)
            {
                var home = new Button { Text = Loc.T("social.go_home") };
                home.Pressed += () => { VisitSystem.End(GetTree()); Visible = false; };
                _content.AddChild(home);
            }
        });

        // ==== Bang hoi ====
        private void LoadGuild() => Get("/api/guild", g =>
        {
            UiStyle.Clear(_content);
            if (g.GetProperty("guild").ValueKind == JsonValueKind.Null)
            {
                foreach (var inv in g.GetProperty("invites").EnumerateArray())
                {
                    int gid = inv.GetProperty("id").GetInt32();
                    var row = new HBoxContainer();
                    _content.AddChild(row);
                    row.AddChild(Line(string.Format(Loc.T("social.guild_invite_fmt"), inv.GetProperty("name").GetString(), inv.GetProperty("tag").GetString())));
                    var join = new Button { Text = Loc.T("social.join") };
                    join.Pressed += () => Post($"/api/guild/{gid}/join", null, null);
                    row.AddChild(join);
                }
                _content.AddChild(UiStyle.Small(string.Format(Loc.T("social.guild_create_hint"), 1000), UiStyle.Gold));
                var row2 = new HBoxContainer();
                _content.AddChild(row2);
                var name = new LineEdit { PlaceholderText = Loc.T("social.guild_name"), CustomMinimumSize = new Vector2(240, 0), MaxLength = 24 };
                var tag = new LineEdit { PlaceholderText = Loc.T("social.guild_tag"), CustomMinimumSize = new Vector2(80, 0), MaxLength = 4 };
                var create = new Button { Text = Loc.T("social.guild_create") };
                create.Pressed += () => EconomyClient.Instance.FlushNow(_ => Post("/api/guild", new { name = name.Text.Trim(), tag = tag.Text.Trim() }, __ => EconomyClient.Instance.RequestSync()));
                row2.AddChild(name);
                row2.AddChild(tag);
                row2.AddChild(create);
                return;
            }
            var guild = g.GetProperty("guild");
            string myRole = guild.GetProperty("myRole").GetString();
            bool canManage = myRole is "leader" or "officer";
            _content.AddChild(UiStyle.Title($"[{guild.GetProperty("tag").GetString()}] {guild.GetProperty("name").GetString()}"));
            _content.AddChild(UiStyle.Small(Loc.T("social.guild_chat_hint")));
            if (canManage)
                _content.AddChild(InputRow(Loc.T("social.friend_name"), Loc.T("social.guild_invite"), n => Post("/api/guild/invite", new { name = n }, __ => Notify.Show(Loc.T("social.invite_sent")))));
            _content.AddChild(UiStyle.Small(Loc.T("social.members"), UiStyle.Gold));
            foreach (var m in g.GetProperty("members").EnumerateArray())
            {
                int pid = m.GetProperty("playerId").GetInt32();
                string role = m.GetProperty("role").GetString();
                bool online = m.GetProperty("online").GetBoolean();
                var row = new HBoxContainer();
                _content.AddChild(row);
                row.AddChild(Line($"{(online ? "● " : "○ ")}{m.GetProperty("displayName").GetString()}  ({Loc.T("social.role." + role)})", online ? new Color(0.5f, 1f, 0.55f) : UiStyle.Dim));
                if (myRole == "leader" && role != "leader")
                {
                    var promote = new Button { Text = role == "officer" ? Loc.T("social.demote") : Loc.T("social.promote") };
                    promote.Pressed += () => Post($"/api/guild/members/{pid}/role", new { role = role == "officer" ? "member" : "officer" }, null);
                    row.AddChild(promote);
                }
                if (canManage && role != "leader" && !(role == "officer" && myRole != "leader"))
                {
                    var kick = new Button { Text = Loc.T("social.kick") };
                    kick.Pressed += () => Post($"/api/guild/members/{pid}/kick", null, null);
                    row.AddChild(kick);
                }
            }
            _content.AddChild(UiStyle.Small(Loc.T("social.storage"), UiStyle.Gold));
            foreach (var st in g.GetProperty("storage").EnumerateArray())
            {
                string item = st.GetProperty("itemId").GetString();
                var row = new HBoxContainer();
                _content.AddChild(row);
                row.AddChild(Line($"{ItemDatabase.Instance.GetDisplayName(item)} x{st.GetProperty("count").GetInt32()}"));
                var take = new Button { Text = Loc.T("social.withdraw") };
                take.Pressed += () => Post("/api/guild/storage/withdraw", new { itemId = item, count = 1 }, __ => EconomyClient.Instance.RequestSync());
                row.AddChild(take);
            }
            var dep = new HBoxContainer();
            _content.AddChild(dep);
            var pick = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
            var ids = Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().Where(id => ItemDatabase.Instance.GetItem(id)?.Type != Data.ItemType.Quest).ToList();
            foreach (var id in ids) pick.AddItem($"{ItemDatabase.Instance.GetDisplayName(id)} x{Inventory.Instance.CountOf(id)}");
            var cnt = new SpinBox { MinValue = 1, MaxValue = 999, Value = 1 };
            var depBtn = new Button { Text = Loc.T("social.deposit") };
            depBtn.Pressed += () =>
            {
                if (pick.Selected < 0 || pick.Selected >= ids.Count) return;
                string item = ids[pick.Selected];
                EconomyClient.Instance.FlushNow(_ => Post("/api/guild/storage/deposit", new { itemId = item, count = (int)cnt.Value }, __ => EconomyClient.Instance.RequestSync()));
            };
            dep.AddChild(pick);
            dep.AddChild(cnt);
            dep.AddChild(depBtn);
            var leave = new Button { Text = Loc.T("social.leave_guild") };
            leave.Pressed += () => Post("/api/guild/leave", null, null);
            _content.AddChild(leave);
        });

        // ==== Hang ngay ====
        private void LoadDaily() => Get("/api/daily", d =>
        {
            UiStyle.Clear(_content);
            var c = d.GetProperty("checkin");
            bool claimed = c.GetProperty("claimedToday").GetBoolean();
            int next = c.GetProperty("nextStreak").GetInt32();
            var rewards = c.GetProperty("rewards").EnumerateArray().ToList();
            var r = rewards[(next - 1) % rewards.Count];
            string rewardText = $"{r.GetProperty("gold").GetInt32()}g" + (r.TryGetProperty("item", out var it) ? $" + {ItemDatabase.Instance.GetDisplayName(it.GetString())} x{r.GetProperty("n").GetInt32()}" : "");
            _content.AddChild(UiStyle.Title(Loc.T("social.checkin")));
            _content.AddChild(Line(string.Format(Loc.T("social.streak_fmt"), c.GetProperty("streak").GetInt32()) + "   " + (claimed ? Loc.T("social.checked") : string.Format(Loc.T("social.today_reward_fmt"), rewardText))));
            if (!claimed)
            {
                var b = new Button { Text = Loc.T("social.checkin_btn") };
                b.Pressed += () => Post("/api/daily/checkin", null, e => { Notify.Show(string.Format(Loc.T("social.checkin_done_fmt"), e.GetProperty("streak").GetInt32())); EconomyClient.Instance.RequestSync(); });
                _content.AddChild(b);
            }
            QuestList(Loc.T("social.daily_quests"), d.GetProperty("daily"), "d");
            QuestList(Loc.T("social.weekly_quests"), d.GetProperty("weekly"), "w");
            _content.AddChild(UiStyle.Small(Loc.T("social.quest_sync_hint")));
        });

        private void QuestList(string title, JsonElement quests, string period)
        {
            _content.AddChild(new HSeparator());
            _content.AddChild(UiStyle.Title(title));
            foreach (var q in quests.EnumerateArray())
            {
                int idx = q.GetProperty("idx").GetInt32();
                int prog = q.GetProperty("progress").GetInt32(), target = q.GetProperty("target").GetInt32();
                bool done = q.GetProperty("claimed").GetBoolean();
                var row = new HBoxContainer();
                _content.AddChild(row);
                string itemReward = q.TryGetProperty("item", out var qi) && qi.ValueKind == JsonValueKind.String
                    ? $" + {ItemDatabase.Instance.GetDisplayName(qi.GetString())} x{q.GetProperty("n").GetInt32()}" : "";
                row.AddChild(Line($"{string.Format(Loc.T("quest_d." + q.GetProperty("id").GetString()), target)}  {prog}/{target}  (+{q.GetProperty("gold").GetInt32()}g{itemReward})",
                    done ? UiStyle.Dim : prog >= target ? UiStyle.Gold : UiStyle.Text));
                if (!done && prog >= target)
                {
                    var b = new Button { Text = Loc.T("social.claim") };
                    b.Pressed += () => Post("/api/daily/claim", new { period, idx }, e =>
                    {
                        string extra = e.TryGetProperty("item", out var ri) && ri.ValueKind == JsonValueKind.String ? $" + {ItemDatabase.Instance.GetDisplayName(ri.GetString())} x{e.GetProperty("n").GetInt32()}" : "";
                        Notify.Show($"+{e.GetProperty("gold").GetInt32()}g{extra}");
                        EconomyClient.Instance.RequestSync();
                    });
                    row.AddChild(b);
                }
                else if (done) row.AddChild(UiStyle.Small(Loc.T("social.claimed")));
            }
        }

        // ==== Truy na ====
        private void LoadBounty() => Get("/api/bounties", d =>
        {
            UiStyle.Clear(_content);
            _content.AddChild(UiStyle.Title(Loc.T("bounty.title")));
            _content.AddChild(UiStyle.Small(Loc.T("bounty.rules")));
            long mine = d.GetProperty("me").GetProperty("amount").GetInt64();
            _content.AddChild(Line(mine > 0 ? string.Format(Loc.T("bounty.me_fmt"), mine) : Loc.T("bounty.me_clean"), mine > 0 ? new Color(1f, 0.4f, 0.3f) : UiStyle.Text));
            _content.AddChild(new HSeparator());
            int rank = 0;
            foreach (var b in d.GetProperty("top").EnumerateArray())
            {
                rank++;
                int id = b.GetProperty("playerId").GetInt32();
                string name = b.GetProperty("name").GetString();
                var row = new HBoxContainer();
                _content.AddChild(row);
                row.AddChild(Line(string.Format(Loc.T("bounty.row_fmt"), rank, name, b.GetProperty("amount").GetInt64(), b.GetProperty("kills").GetInt32()), UiStyle.Gold));
                var add = new Button { Text = Loc.T("bounty.add_100") };
                add.Pressed += () => Post("/api/bounties", new { targetId = id, amount = 100 }, e => { Notify.Show(string.Format(Loc.T("bounty.placed_fmt"), name, e.GetProperty("amount").GetInt64())); EconomyClient.Instance.RequestSync(); BountyBoard.Instance?.Refresh(); });
                row.AddChild(add);
            }
            if (rank == 0) _content.AddChild(Line(Loc.T("bounty.none"), UiStyle.Dim));
            _content.AddChild(new HSeparator());
            _content.AddChild(UiStyle.Small(Loc.T("bounty.place_hint")));
            var amount = new SpinBox { MinValue = 100, MaxValue = 100000, Step = 50, Value = 200, CustomMinimumSize = new Vector2(120, 0) };
            var row2 = InputRow(Loc.T("bounty.target_name"), Loc.T("bounty.place_btn"), name =>
                Post("/api/bounties", new { targetName = name, amount = (int)amount.Value }, e => { Notify.Show(string.Format(Loc.T("bounty.placed_fmt"), name, e.GetProperty("amount").GetInt64())); EconomyClient.Instance.RequestSync(); BountyBoard.Instance?.Refresh(); }));
            row2.AddChild(amount);
            _content.AddChild(row2);
        });

        // ==== Xep hang ====
        private static readonly string[] Boards = { "gold", "level", "pvp", "fish_caught", "crops_harvested", "bosses_defeated", "monsters_killed", "ores_mined" };

        private void LoadRanking()
        {
            // Gui chi so moi nhat truoc khi xem (StatsReporter cung tu gui dinh ky).
            StatsReporter.Report(() => Get($"/api/leaderboard/{_rankStat}", b =>
            {
                UiStyle.Clear(_content);
                var sel = new HBoxContainer();
                _content.AddChild(sel);
                foreach (var stat in Boards)
                {
                    var btn = new Button { Text = Loc.T("rank." + stat), ToggleMode = true, ButtonPressed = stat == _rankStat };
                    var s2 = stat;
                    btn.Pressed += () => { _rankStat = s2; Refresh(); };
                    sel.AddChild(btn);
                }
                if (b.GetProperty("me").ValueKind != JsonValueKind.Null)
                    _content.AddChild(UiStyle.Small(string.Format(Loc.T("rank.me_fmt"), b.GetProperty("me").GetProperty("rank").GetInt32(), b.GetProperty("me").GetProperty("value").GetInt64()), UiStyle.Gold));
                foreach (var row in b.GetProperty("top").EnumerateArray())
                {
                    int rank = row.GetProperty("rank").GetInt32();
                    var color = rank == 1 ? new Color(1f, 0.85f, 0.3f) : rank <= 3 ? new Color(0.85f, 0.85f, 0.95f) : UiStyle.Text;
                    string extra = _rankStat == "pvp" ? $"  ({row.GetProperty("wins").GetInt32()}W/{row.GetProperty("losses").GetInt32()}L)" : "";
                    _content.AddChild(Line($"#{rank}  {row.GetProperty("name").GetString()}  -  {row.GetProperty("value").GetInt64()}{extra}", color));
                }
            }));
        }
    }
}
