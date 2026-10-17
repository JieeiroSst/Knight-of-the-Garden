using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // IMA-ADPCM 4 bit/mau: nen giong noi 16 bit -> 4 bit (16 kHz ~ 64 kbps, chi khi dang noi). Godot
    // 4.3 khong co san Opus; ADPCM du ro cho giong noi, rat nhe CPU va viet duoc o ca 2 dau. Bo nen
    // giu trang thai LIEN TUC giua cac goi (khong khoi dong lai buoc luong tu -> khong meo dau goi)
    // va ghi trang thai do vao dau moi goi -> moi goi van giai nen doc lap, mat goi khong anh huong.
    public static class AdpcmCodec
    {
        private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        private static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97,
            107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871,
            5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
            27086, 29794, 32767,
        };

        public const int HeaderBytes = 3; // int16 predictor + byte index

        // Ghi header + du lieu nen cua samples vao output tu vi tri offset; tra ve so byte da ghi.
        // predictor/index = trang thai bo nen, cap nhat de dung tiep cho goi sau.
        public static int Encode(short[] samples, int count, byte[] output, int offset, ref int predictor, ref int index)
        {
            output[offset] = (byte)(predictor & 0xFF);
            output[offset + 1] = (byte)((predictor >> 8) & 0xFF);
            output[offset + 2] = (byte)index;
            int o = offset + HeaderBytes;
            for (int i = 0; i < count; i += 2)
            {
                int lo = EncodeSample(samples[i], ref predictor, ref index);
                int hi = i + 1 < count ? EncodeSample(samples[i + 1], ref predictor, ref index) : 0;
                output[o++] = (byte)(lo | (hi << 4));
            }
            return o - offset;
        }

        private static int EncodeSample(int sample, ref int predictor, ref int index)
        {
            int step = StepTable[index];
            int diff = sample - predictor;
            int code = 0;
            if (diff < 0) { code = 8; diff = -diff; }
            int delta = step >> 3;
            if (diff >= step) { code |= 4; diff -= step; delta += step; }
            if (diff >= step >> 1) { code |= 2; diff -= step >> 1; delta += step >> 1; }
            if (diff >= step >> 2) { code |= 1; delta += step >> 2; }
            predictor += (code & 8) != 0 ? -delta : delta;
            predictor = Mathf.Clamp(predictor, -32768, 32767);
            index = Mathf.Clamp(index + IndexTable[code], 0, StepTable.Length - 1);
            return code;
        }

        // Giai nen data[offset..offset+length) thanh mau float [-1,1].
        public static float[] Decode(byte[] data, int offset, int length)
        {
            if (length <= HeaderBytes) return System.Array.Empty<float>();
            int predictor = (short)(data[offset] | (data[offset + 1] << 8));
            int index = Mathf.Clamp(data[offset + 2], 0, StepTable.Length - 1);
            int n = (length - HeaderBytes) * 2;
            var outSamples = new float[n];
            int k = 0;
            for (int i = offset + HeaderBytes; i < offset + length; i++)
            {
                outSamples[k++] = DecodeNibble(data[i] & 0x0F, ref predictor, ref index);
                outSamples[k++] = DecodeNibble(data[i] >> 4, ref predictor, ref index);
            }
            return outSamples;
        }

        private static float DecodeNibble(int code, ref int predictor, ref int index)
        {
            int step = StepTable[index];
            int delta = step >> 3;
            if ((code & 4) != 0) delta += step;
            if ((code & 2) != 0) delta += step >> 1;
            if ((code & 1) != 0) delta += step >> 2;
            predictor += (code & 8) != 0 ? -delta : delta;
            predictor = Mathf.Clamp(predictor, -32768, 32767);
            index = Mathf.Clamp(index + IndexTable[code], 0, StepTable.Length - 1);
            return predictor / 32768f;
        }
    }

    // Voice chat theo khoang cach: noi chuyen bang micro voi nguoi choi o GAN (cung kenh, trong
    // ~VoiceRange don vi - server tu loc theo vi tri that, xa hon khong nghe/khong nhan duoc goi).
    // Giong phat ra tu chinh nhan vat (AudioStreamPlayer3D tren RemotePlayer) nen nghe duoc huong va
    // nho dan theo khoang cach.
    //
    // 3 che do (luu user://settings.cfg muc "voice"): Tat (mac dinh - khong mo micro), Bam de noi
    // (giu [T]), Mic mo (tu bat khi co tieng noi vuot nguong). Tat tieng tung nguoi qua menu [E].
    public partial class VoiceChat : Node
    {
        public enum Mode { Off, PushToTalk, Open }

        public static VoiceChat Instance { get; private set; }

        public const int SampleRate = 16000;
        private const int FrameSamples = 640; // 40 ms / goi
        public const float VoiceRange = 900f; // PHAI khop VOICE_RANGE trong backend gateway.js
        public const byte PacketTag = 0x56; // 'V'
        private const string MicBus = "VoiceMic";
        public const string OutBus = "Voice";
        private const string CfgPath = "user://settings.cfg";
        private const float HangoverSec = 0.4f;

        public Mode CurrentMode { get; private set; } = Mode.Off;
        public float OutputVolume { get; private set; } = 1f;   // 0..1.5
        public float MicThreshold { get; private set; } = 0.02f; // RMS mo mic (che do Mic mo)
        public bool MuteAll { get; private set; }
        public bool IsTransmitting { get; private set; }
        public float MicLevel { get; private set; }

        // Nguoi bi tat tieng (theo PlayerId) - chi trong phien choi nay.
        public readonly HashSet<int> Muted = new();

        private AudioStreamPlayer _micPlayer;
        private AudioEffectCapture _capture;
        private readonly List<short> _pending = new(FrameSamples * 2);
        private float _resamplePos;
        private float _resampleAcc;
        private int _resampleCount;
        private float _hangover;
        private byte _seq;
        private int _encPredictor, _encIndex;
        private readonly short[] _frame = new short[FrameSamples];
        private readonly byte[] _packet = new byte[2 + AdpcmCodec.HeaderBytes + FrameSamples / 2];

        public event System.Action Changed;

        public override void _EnterTree() => Instance = this;

        private Label _talkingLabel;

        public override void _Ready()
        {
            EnsureBuses();
            LoadSettings();
            ApplyMode();

            // Chi bao "dang noi" goc trai duoi - nguoi choi biet micro dang phat (quyen rieng tu).
            var layer = new CanvasLayer { Layer = 60 };
            AddChild(layer);
            _talkingLabel = new Label { Position = new Vector2(12, 470), Visible = false };
            _talkingLabel.AddThemeColorOverride("font_color", new Color(0.45f, 1f, 0.5f));
            _talkingLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _talkingLabel.AddThemeConstantOverride("outline_size", 6);
            layer.AddChild(_talkingLabel);
            Changed += () =>
            {
                _talkingLabel.Visible = IsTransmitting;
                _talkingLabel.Text = Loc.T("voice.talking");
            };
        }

        private void EnsureBuses()
        {
            if (AudioServer.GetBusIndex(OutBus) < 0)
            {
                AudioServer.AddBus();
                int i = AudioServer.BusCount - 1;
                AudioServer.SetBusName(i, OutBus);
                AudioServer.SetBusSend(i, "Master");
            }
            if (AudioServer.GetBusIndex(MicBus) < 0)
            {
                AudioServer.AddBus();
                int i = AudioServer.BusCount - 1;
                AudioServer.SetBusName(i, MicBus);
                // Tat tieng bus micro: van thu duoc qua hieu ung Capture nhung khong tu nghe lai giong minh.
                AudioServer.SetBusMute(i, true);
                AudioServer.AddBusEffect(i, new AudioEffectCapture { BufferLength = 0.5f });
            }
            _capture = AudioServer.GetBusEffect(AudioServer.GetBusIndex(MicBus), 0) as AudioEffectCapture;
        }

        private void LoadSettings()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(CfgPath) != Error.Ok) return;
            CurrentMode = (Mode)Mathf.Clamp((int)cfg.GetValue("voice", "mode", 0), 0, 2);
            OutputVolume = (float)cfg.GetValue("voice", "volume", 1f);
            MicThreshold = (float)cfg.GetValue("voice", "threshold", 0.02f);
            MuteAll = (bool)cfg.GetValue("voice", "mute_all", false);
        }

        private void SaveSettings()
        {
            var cfg = new ConfigFile();
            cfg.Load(CfgPath);
            cfg.SetValue("voice", "mode", (int)CurrentMode);
            cfg.SetValue("voice", "volume", OutputVolume);
            cfg.SetValue("voice", "threshold", MicThreshold);
            cfg.SetValue("voice", "mute_all", MuteAll);
            cfg.Save(CfgPath);
        }

        public void SetMode(Mode mode) { CurrentMode = mode; SaveSettings(); ApplyMode(); Changed?.Invoke(); }
        public void SetVolume(float v) { OutputVolume = Mathf.Clamp(v, 0f, 1.5f); SaveSettings(); ApplyMode(); }
        public void SetThreshold(float t) { MicThreshold = Mathf.Clamp(t, 0.002f, 0.2f); SaveSettings(); }
        public void SetMuteAll(bool m) { MuteAll = m; SaveSettings(); ApplyMode(); Changed?.Invoke(); }

        public void ToggleMute(int playerId)
        {
            if (!Muted.Add(playerId)) Muted.Remove(playerId);
            Changed?.Invoke();
        }

        private void ApplyMode()
        {
            int outIdx = AudioServer.GetBusIndex(OutBus);
            AudioServer.SetBusVolumeDb(outIdx, Mathf.LinearToDb(Mathf.Max(OutputVolume, 0.0001f)));
            AudioServer.SetBusMute(outIdx, MuteAll || OutputVolume <= 0.001f);

            bool wantMic = CurrentMode != Mode.Off;
            // Micro chi mo khi nguoi choi bat voice (he dieu hanh chi hoi quyen micro luc nay).
            if (wantMic && _micPlayer == null)
            {
                if (!(bool)ProjectSettings.GetSetting("audio/driver/enable_input", false))
                    Notify.Show(Loc.T("voice.input_disabled"));
                _micPlayer = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = MicBus };
                AddChild(_micPlayer);
                _micPlayer.Play();
                _capture?.ClearBuffer();
            }
            else if (!wantMic && _micPlayer != null)
            {
                _micPlayer.Stop();
                _micPlayer.QueueFree();
                _micPlayer = null;
                IsTransmitting = false;
            }
        }

        public override void _Process(double delta)
        {
            if (_micPlayer == null || _capture == null) return;
            int available = _capture.GetFramesAvailable();
            if (available <= 0) return;
            var frames = _capture.GetBuffer(available);

            // Ha tan so lay mau (44.1/48 kHz -> 16 kHz) bang trung binh cong tung khoang: vua doi
            // tan so vua loc bot tan cao (tranh rang cua).
            float ratio = AudioServer.GetMixRate() / SampleRate;
            foreach (var f in frames)
            {
                _resampleAcc += (f.X + f.Y) * 0.5f;
                _resampleCount++;
                _resamplePos += 1f;
                if (_resamplePos >= ratio)
                {
                    _resamplePos -= ratio;
                    float s = _resampleAcc / _resampleCount;
                    _resampleAcc = 0f;
                    _resampleCount = 0;
                    _pending.Add((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
                }
            }

            while (_pending.Count >= FrameSamples)
            {
                double sum = 0;
                for (int i = 0; i < FrameSamples; i++)
                {
                    _frame[i] = _pending[i];
                    double v = _frame[i] / 32768.0;
                    sum += v * v;
                }
                _pending.RemoveRange(0, FrameSamples);
                float rms = (float)System.Math.Sqrt(sum / FrameSamples);
                MicLevel = rms;

                bool gate = CurrentMode switch
                {
                    Mode.PushToTalk => Input.IsActionPressed("voice_ptt"),
                    Mode.Open => rms >= MicThreshold || (_hangover -= FrameSamples / (float)SampleRate) > 0f,
                    _ => false,
                };
                if (CurrentMode == Mode.Open && rms >= MicThreshold) _hangover = HangoverSec;
                bool canSend = gate && NetworkClient.Instance?.CanSendVoice(VoiceRange) == true;
                if (canSend != IsTransmitting) { IsTransmitting = canSend; Changed?.Invoke(); }
                if (!canSend) continue;

                _packet[0] = PacketTag;
                _packet[1] = _seq++;
                int len = AdpcmCodec.Encode(_frame, FrameSamples, _packet, 2, ref _encPredictor, ref _encIndex);
                NetworkClient.Instance.SendVoice(_packet, 2 + len);
            }
        }

        // Goi tu NetworkClient khi nhan goi giong noi: [tag][u32 speakerId][seq][adpcm...]
        public void OnVoicePacket(byte[] data, RemotePlayer speaker)
        {
            if (speaker == null || MuteAll || Muted.Contains(speaker.PlayerId)) return;
            const int off = 1 + 4 + 1;
            if (data.Length <= off + AdpcmCodec.HeaderBytes) return;
            speaker.PushVoice(AdpcmCodec.Decode(data, off, data.Length - off));
        }
    }
}
