using Godot;

namespace HiepSiVeVuon.UI
{
    // Thong bao nhanh tren man hinh (toast) - truoc day phan lon phan hoi chi di qua GD.Print
    // (nguoi choi KHONG thay duoc). Goi tu bat ky dau: Notify.Show("..."). Van GD.Print song song
    // de giu log debug nhu cu.
    public static class Notify
    {
        public static void Show(string text)
        {
            GD.Print(text);
            var tree = Engine.GetMainLoop() as SceneTree;
            var hud = tree?.GetFirstNodeInGroup("hud") as HUD;
            hud?.ShowToast(text);
        }
    }
}
