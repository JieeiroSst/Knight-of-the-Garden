using Godot;

namespace HiepSiVeVuon.Systems
{
    // Autoload -> nhac nen phat lien tuc kieu Stardew Valley: het bai nay tu dong sang bai
    // khac trong danh sach, het danh sach thi quay lai tu dau. Tat ca nhac deu la CC0
    // (OpenGameArt.org), khong can ghi cong:
    // "Gone Fishin'" by memoraphile, "Down the river" (It's time for adventure vol.2) by
    // Komiku, "bgm" (Aimless Autumn) by dearyekate, "Meadow Thoughts" by (OpenGameArt CC0),
    // "Forest Ambience" (OpenGameArt CC0).
    public partial class MusicPlayer : Node
    {
        private static readonly string[] Playlist =
        {
            "res://assets/music/gone_fishin.mp3",
            "res://assets/music/down_the_river.mp3",
            "res://assets/music/aimless_autumn.mp3",
            "res://assets/music/meadow_thoughts.ogg",
            "res://assets/music/forest_ambience.mp3",
        };

        private AudioStreamPlayer _player;
        private int _trackIndex = 0;

        public override void _Ready()
        {
            _player = new AudioStreamPlayer { VolumeDb = -8f };
            AddChild(_player);
            _player.Finished += PlayNextTrack;

            // Bat dau tu 1 bai ngau nhien de moi lan mo game khong luon nghe dung 1 bai dau tien.
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _trackIndex = rng.RandiRange(0, Playlist.Length - 1);
            PlayCurrentTrack();
        }

        private void PlayCurrentTrack()
        {
            var stream = GD.Load<AudioStream>(Playlist[_trackIndex]);
            if (stream == null) return;
            _player.Stream = stream;
            _player.Play();
        }

        private void PlayNextTrack()
        {
            _trackIndex = (_trackIndex + 1) % Playlist.Length;
            PlayCurrentTrack();
        }
    }
}
