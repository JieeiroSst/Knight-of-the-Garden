using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Debug
{
    public partial class DebugRegisterTest : Node
    {
        public override void _Ready()
        {
            GD.Print("DEBUG: bat dau test Register...");
            BackendClient.Instance.Register("debuguser_" + Time.GetTicksMsec(), "debugpass123", (ok, msg) =>
            {
                GD.Print($"DEBUG RESULT: ok={ok} msg={msg}");
                GetTree().Quit();
            });
        }
    }
}
