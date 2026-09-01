using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HiepSiVeVuon.Systems
{
    // Ket noi backend that su qua Internet (Node.js/Express + PostgreSQL, xem thu muc backend/)
    // - THAY THE HOAN TOAN viec luu file JSON local (user://savegame.json) truoc day. Day la he
    // thong MANG/BAT DONG BO DAU TIEN trong du an: moi request tao 1 HttpRequest con rieng (them
    // vao scene tree, tu huy sau khi xong) vi 1 HttpRequest chi xu ly duoc 1 request/luc - tao rieng
    // moi lan goi de FetchSave/PushSave/Login... khong tranh chap nhau neu goi gan nhau.
    public partial class BackendClient : Node
    {
        public static BackendClient Instance { get; private set; }

        // Doi thanh URL server that sau khi tu deploy backend (VPS/Render/Railway/Fly.io...) -
        // xem backend/README.md. Mac dinh tro ve may nay de kiem tra cuc bo qua "docker compose up".
        [Export] public string BaseUrl = "http://localhost:3000";

        // JWT CHI giu trong bo nho, KHONG ghi ra dia - dung tinh than "khong luu tru local" ap
        // dung ca cho thong tin dang nhap, khong chi save data. Mat khi thoat game, phai dang
        // nhap lai moi lan mo game (chap nhan duoc, khong yeu cau "nho dang nhap").
        public string AuthToken { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

        public override void _EnterTree()
        {
            Instance = this;
        }

        public void Register(string username, string password, Action<bool, string> onDone)
            => PostAuth("/api/register", username, password, onDone);

        public void Login(string username, string password, Action<bool, string> onDone)
            => PostAuth("/api/login", username, password, onDone);

        // onDone(ok, tokenOrErrorMessage)
        private void PostAuth(string path, string username, string password, Action<bool, string> onDone)
        {
            string body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "username", username },
                { "password", password }
            });

            SendRequest(path, HttpClient.Method.Post, body, needsAuth: false, onDone: (ok, status, text) =>
            {
                if (!ok)
                {
                    onDone?.Invoke(false, "Không kết nối được server.");
                    return;
                }
                if (!TryParse(text, out JsonDocument doc))
                {
                    onDone?.Invoke(false, "Server trả về dữ liệu không hợp lệ.");
                    return;
                }
                using (doc)
                {
                    if ((status == 200 || status == 201) && doc.RootElement.TryGetProperty("token", out var tokenEl))
                    {
                        AuthToken = tokenEl.GetString();
                        onDone?.Invoke(true, AuthToken);
                    }
                    else
                    {
                        string err = doc.RootElement.TryGetProperty("error", out var errEl) ? errEl.GetString() : "Lỗi không rõ.";
                        onDone?.Invoke(false, err);
                    }
                }
            });
        }

        // onDone(found, saveJsonOrNull) - found=false + null = nguoi choi moi (chua tung luu),
        // found=false + non-null (bat dau bang "ERR:") = loi that su (mat mang, server loi...).
        public void FetchSave(Action<bool, string> onDone)
        {
            SendRequest("/api/save", HttpClient.Method.Get, null, needsAuth: true, onDone: (ok, status, text) =>
            {
                if (!ok)
                {
                    onDone?.Invoke(false, "ERR:Không kết nối được server.");
                    return;
                }
                if (status == 404)
                {
                    onDone?.Invoke(false, null);
                    return;
                }
                if (status != 200 || !TryParse(text, out JsonDocument doc))
                {
                    onDone?.Invoke(false, "ERR:Lỗi tải dữ liệu lưu.");
                    return;
                }
                using (doc)
                {
                    onDone?.Invoke(true, doc.RootElement.GetProperty("data").GetRawText());
                }
            });
        }

        // onDone(ok, errorMessageOrNull)
        public void PushSave(string saveDataJson, Action<bool, string> onDone)
        {
            string wrapped = "{\"data\":" + saveDataJson + "}";
            SendRequest("/api/save", HttpClient.Method.Put, wrapped, needsAuth: true, onDone: (ok, status, _) =>
            {
                onDone?.Invoke(ok && status == 200, ok && status == 200 ? null : "Không lưu được lên server.");
            });
        }

        private static bool TryParse(string text, out JsonDocument doc)
        {
            try
            {
                doc = JsonDocument.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                doc = null;
                return false;
            }
        }

        // onDone(ok, httpStatus, bodyText) - ok=false nghia la loi mang (khong toi duoc server),
        // KHAC voi status>=400 (server toi duoc nhung tra loi).
        private void SendRequest(string path, HttpClient.Method method, string body, bool needsAuth, Action<bool, int, string> onDone)
        {
            if (needsAuth && !IsLoggedIn)
            {
                onDone?.Invoke(false, 0, "Chưa đăng nhập.");
                return;
            }

            var req = new HttpRequest();
            AddChild(req);

            var headers = new List<string> { "Content-Type: application/json" };
            if (needsAuth) headers.Add($"Authorization: Bearer {AuthToken}");

            req.RequestCompleted += (long resultCode, long responseCode, string[] respHeaders, byte[] bodyBytes) =>
            {
                bool ok = resultCode == (long)HttpRequest.Result.Success;
                string text = bodyBytes.Length > 0 ? Encoding.UTF8.GetString(bodyBytes) : "";
                onDone?.Invoke(ok, (int)responseCode, text);
                req.QueueFree();
            };

            Error err = req.Request(BaseUrl + path, headers.ToArray(), method, body ?? "");
            if (err != Error.Ok)
            {
                req.QueueFree();
                onDone?.Invoke(false, 0, "");
            }
        }
    }
}
