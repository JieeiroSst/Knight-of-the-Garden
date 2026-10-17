using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Am thanh dong vat / quai (3D - nho dan theo khoang cach). Phan lon file .wav trong assets/sfx do
    // tools tong hop (synth) tao ra, khong can tai tu ngoai.
    public static class AnimalSounds
    {
        private static readonly Dictionary<string, AudioStream> _cache = new();

        public static string CallOf(string species) => species switch
        {
            "wolf" => GameManager.Instance.IsNight ? "wolf_howl" : "wolf_growl",
            "fox" => "fox_bark",
            "deer" => "deer_call",
            "rabbit" => "rabbit_squeak",
            "duck" => "duck_quack",
            "pig" => "pig_oink",
            "sheep" => "sheep_baa",
            "goat" => "goat_bleat",
            "cow" => "cow_moo",
            "chicken" => "chicken_cluck",
            "horse" => "horse_neigh",
            _ => null,
        };

        private static AudioStream Load(string name)
        {
            if (name == null) return null;
            if (_cache.TryGetValue(name, out var s)) return s;
            s = ResourceLoader.Exists($"res://assets/sfx/{name}.wav") ? GD.Load<AudioStream>($"res://assets/sfx/{name}.wav")
                : ResourceLoader.Exists($"res://assets/sfx/{name}.mp3") ? GD.Load<AudioStream>($"res://assets/sfx/{name}.mp3") : null;
            _cache[name] = s;
            return s;
        }

        // Phat 1 lan tai `at` (nguon am di theo con vat), tu xoa khi xong.
        public static void Play(Node3D at, string name, float pitch = 1f, float volumeDb = 0f, float maxDistance = 520f)
        {
            if (at == null || !GodotObject.IsInstanceValid(at) || !at.IsInsideTree()) return;
            var stream = Load(name);
            if (stream == null) return;
            var p = new AudioStreamPlayer3D
            {
                Stream = stream, PitchScale = pitch * (float)GD.RandRange(0.93, 1.07), VolumeDb = volumeDb,
                MaxDistance = maxDistance, UnitSize = 40f, Position = Vector3.Up * 15f,
            };
            at.AddChild(p);
            p.Finished += p.QueueFree;
            p.Play();
        }

        public static void Call(Node3D at, string species) => Play(at, CallOf(species));

        // Trung don: tieng keu cua loai (cao giong hon) + tieng keu dau.
        public static void Hurt(Node3D at, string species)
        {
            Play(at, "animal_hurt", species is "cow" or "horse" or "pig" ? 0.7f : species is "rabbit" or "chicken" or "duck" ? 1.4f : 1f);
            string call = CallOf(species);
            if (call != null && call != "wolf_howl") Play(at, call, 1.25f, -4f);
        }
    }
}
