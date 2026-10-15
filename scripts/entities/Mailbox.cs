using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Hop Thu truoc nha - bam [E] mo Nhat Ky o tab Thu (xem MailSystem.cs/JournalUI.cs).
    public partial class Mailbox : FarmFacility
    {
        public override void _Ready()
        {
            AddToGroup("mailboxes");
            AddCollisionBox(new Vector3(16, 36, 16));
            AddBox(new Vector3(4, 28, 4), new Vector3(0, 14, 0), new Color(0.45f, 0.3f, 0.18f));
            AddBox(new Vector3(14, 10, 20), new Vector3(0, 32, 0), new Color(0.2f, 0.35f, 0.7f));
            AddBox(new Vector3(2, 8, 2), new Vector3(8, 36, -6), new Color(0.85f, 0.15f, 0.15f)); // co bao thu
        }

        public override void Interact()
        {
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUI)?.Open(JournalUI.Tab.Mail);
        }
    }
}
