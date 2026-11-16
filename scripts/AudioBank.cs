using Godot;
using System;

/// <summary>
/// 程序化音频库：不依赖任何外部音频文件，运行时合成 8-bit 风格音效与循环背景音乐。
/// 所有音频都是 AudioStreamWav（16 位单声道），按需生成并缓存。
/// </summary>
public static class AudioBank
{
    private const int Rate = 22050;

    private enum Wave { Square, Triangle, Sine, Noise }

    private static AudioStreamWav _eat;
    private static AudioStreamWav _spit;
    private static AudioStreamWav _die;
    private static AudioStreamWav _win;
    private static AudioStreamWav _click;
    private static AudioStreamWav _turn;
    private static AudioStreamWav _land;
    private static AudioStreamWav _denied;
    private static AudioStreamWav _music;

    public static AudioStreamWav Eat => _eat ??= BuildEat();
    public static AudioStreamWav Spit => _spit ??= BuildSpit();
    public static AudioStreamWav Die => _die ??= BuildDie();
    public static AudioStreamWav Win => _win ??= BuildWin();
    public static AudioStreamWav Click => _click ??= BuildClick();
    public static AudioStreamWav Turn => _turn ??= BuildTurn();
    public static AudioStreamWav Land => _land ??= BuildLand();
    public static AudioStreamWav Denied => _denied ??= BuildDenied();
    public static AudioStreamWav Music => _music ??= BuildMusic();

    // ---------- 音效 ----------

    private static AudioStreamWav BuildEat()
    {
        float[] buf = new float[(int)(Rate * 0.25f)];
        AddNote(buf, 0.00f, 520f, 780f, 0.09f, 0.5f, Wave.Square);
        AddNote(buf, 0.08f, 780f, 1040f, 0.12f, 0.5f, Wave.Square);
        return ToStream(buf, false);
    }

    private static AudioStreamWav BuildSpit()
    {
        float[] buf = new float[(int)(Rate * 0.3f)];
        AddNote(buf, 0.00f, 420f, 90f, 0.22f, 0.55f, Wave.Triangle);
        AddNote(buf, 0.00f, 0f, 0f, 0.12f, 0.35f, Wave.Noise);
        return ToStream(buf, false);
    }

    private static AudioStreamWav BuildDie()
    {
        float[] buf = new float[(int)(Rate * 0.9f)];
        AddNote(buf, 0.00f, 440f, 330f, 0.2f, 0.5f, Wave.Square);
        AddNote(buf, 0.20f, 330f, 220f, 0.2f, 0.5f, Wave.Square);
        AddNote(buf, 0.40f, 220f, 80f, 0.45f, 0.5f, Wave.Square);
        return ToStream(buf, false);
    }

    private static AudioStreamWav BuildWin()
    {
        float[] buf = new float[(int)(Rate * 1.1f)];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < notes.Length; i++)
            AddNote(buf, i * 0.13f, notes[i], notes[i], 0.25f, 0.45f, Wave.Square);
        AddNote(buf, 0.52f, 1046.5f, 1046.5f, 0.5f, 0.35f, Wave.Triangle);
        return ToStream(buf, false);
    }

    private static AudioStreamWav BuildClick()
    {
        float[] buf = new float[(int)(Rate * 0.1f)];
        AddNote(buf, 0.00f, 880f, 660f, 0.07f, 0.4f, Wave.Square);
        return ToStream(buf, false);
    }

    private static AudioStreamWav BuildTurn()
    {
        float[] buf = new float[(int)(Rate * 0.05f)];
        AddNote(buf, 0.00f, 300f, 260f, 0.04f, 0.18f, Wave.Triangle);
        return ToStream(buf, false);
    }

    // 肉块砸到地面的闷响
    private static AudioStreamWav BuildLand()
    {
        float[] buf = new float[(int)(Rate * 0.18f)];
        AddNote(buf, 0.00f, 180f, 55f, 0.15f, 0.6f, Wave.Triangle);
        AddNote(buf, 0.00f, 0f, 0f, 0.08f, 0.25f, Wave.Noise);
        return ToStream(buf, false);
    }

    // 催吐失败（无处可吐 / 本步已吐过）的低音双哔
    private static AudioStreamWav BuildDenied()
    {
        float[] buf = new float[(int)(Rate * 0.18f)];
        AddNote(buf, 0.00f, 220f, 220f, 0.07f, 0.4f, Wave.Square);
        AddNote(buf, 0.10f, 160f, 160f, 0.08f, 0.4f, Wave.Square);
        return ToStream(buf, false);
    }

    // ---------- 背景音乐：8 小节可循环 ----------

    private static AudioStreamWav BuildMusic()
    {
        const float step = 0.22f;   // 每步时长（秒）
        const int steps = 32;
        float[] buf = new float[(int)(Rate * step * steps)];

        // C 大调五声音阶，轻快、有点滑稽的味道
        float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f, 659.25f };
        int[] melody =
        {
            0, 2, 4, 2, 3, 2, 1, 2,
            0, 2, 4, 5, 4, 3, 2, 1,
            2, 4, 5, 6, 5, 4, 3, 2,
            4, 3, 2, 1, 0, 1, 2, 0
        };
        float[] bass = { 130.81f, 130.81f, 146.83f, 98.00f };

        for (int i = 0; i < steps; i++)
        {
            float t = i * step;
            float f = scale[melody[i]];
            AddNote(buf, t, f, f, step * 0.8f, 0.16f, Wave.Square);
            if (i % 4 == 0)
            {
                float b = bass[(i / 8) % bass.Length];
                AddNote(buf, t, b, b, step * 3.2f, 0.28f, Wave.Triangle);
            }
            if (i % 2 == 1)
                AddNote(buf, t, 0f, 0f, 0.03f, 0.05f, Wave.Noise); // 轻微节拍
        }
        return ToStream(buf, true);
    }

    // ---------- 合成内核 ----------

    private static void AddNote(float[] buf, float start, float f0, float f1, float dur, float vol, Wave wave)
    {
        int s0 = (int)(start * Rate);
        int n = (int)(dur * Rate);
        double phase = 0.0;
        var rng = new Random(1234 + s0);
        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;

            float p = (float)i / Math.Max(1, n);
            float freq = f0 + (f1 - f0) * p;
            phase += freq / Rate;
            phase -= Math.Floor(phase);

            float v;
            switch (wave)
            {
                case Wave.Square: v = phase < 0.5 ? 1f : -1f; break;
                case Wave.Triangle: v = (float)(Math.Abs(phase * 4.0 - 2.0) - 1.0); break;
                case Wave.Sine: v = (float)Math.Sin(phase * Math.PI * 2.0); break;
                default: v = (float)(rng.NextDouble() * 2.0 - 1.0); break;
            }

            // 包络：2ms 起音 + 线性衰减，避免爆音
            float attack = Math.Min(1f, i / (Rate * 0.002f));
            float env = attack * (1f - p);
            buf[idx] += v * vol * env;
        }
    }

    private static AudioStreamWav ToStream(float[] buf, bool loop)
    {
        byte[] data = new byte[buf.Length * 2];
        for (int i = 0; i < buf.Length; i++)
        {
            float c = Math.Clamp(buf[i], -1f, 1f);
            short s = (short)(c * 30000f);
            data[i * 2] = (byte)(s & 0xFF);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data
        };
        if (loop)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = buf.Length;
        }
        return wav;
    }
}
