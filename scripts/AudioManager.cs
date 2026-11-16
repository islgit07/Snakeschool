using Godot;

/// <summary>
/// 音频中枢：BGM 循环 + 主/次两条音效通道（避免同帧互相掐断），
/// 以及静音开关、最佳纪录存档（user://save.cfg）。
/// </summary>
public partial class AudioManager : Node
{
    private const string SavePath = "user://save.cfg";
    private const string BusMaster = "Master";

    private AudioStreamPlayer _music;
    private AudioStreamPlayer _sfxMain;
    private AudioStreamPlayer _sfxMinor;

    public bool Muted { get; private set; }

    /// <summary>全局单例引用，供无 Main 引用的控件（如 SquishyToggle）播放音效。</summary>
    public static AudioManager Instance { get; private set; }

    // 最佳纪录：通关时吃苹果的最少数（越少越强）、最快秒数、通关次数
    public int BestApples = -1;
    public float BestTime = -1f;
    public int Wins;

    public override void _Ready()
    {
        Instance = this;
        LoadPrefs();
        AudioServer.SetBusMute(AudioServer.GetBusIndex(BusMaster), Muted);

        _music = new AudioStreamPlayer { Stream = AudioBank.Music, VolumeDb = -9f };
        _sfxMain = new AudioStreamPlayer { VolumeDb = -2f };
        _sfxMinor = new AudioStreamPlayer { VolumeDb = -6f };
        AddChild(_music);
        AddChild(_sfxMain);
        AddChild(_sfxMinor);
        _music.Play();
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        AudioServer.SetBusMute(AudioServer.GetBusIndex(BusMaster), Muted);
        SavePrefs();
    }

    /// <summary>主音效：吃、吐、死、胜等关键事件。</summary>
    public void Play(AudioStreamWav stream, float pitch = 1f, float volumeDb = 0f)
    {
        _sfxMain.Stream = stream;
        _sfxMain.PitchScale = pitch;
        _sfxMain.VolumeDb = volumeDb - 2f;
        _sfxMain.Play();
    }

    /// <summary>次音效：转向、按钮、提示等轻量事件，不抢占主音效。</summary>
    public void PlayMinor(AudioStreamWav stream, float pitch = 1f)
    {
        _sfxMinor.Stream = stream;
        _sfxMinor.PitchScale = pitch;
        _sfxMinor.Play();
    }

    /// <summary>记录一次通关，返回是否刷新了纪录（用于"新纪录"徽标）。</summary>
    public bool ReportWin(int apples, float seconds)
    {
        Wins++;
        bool better = false;
        if (BestApples < 0 || apples < BestApples) { BestApples = apples; better = true; }
        if (BestTime < 0 || seconds < BestTime) { BestTime = seconds; better = true; }
        SavePrefs();
        return better;
    }

    private void LoadPrefs()
    {
        var cf = new ConfigFile();
        if (cf.Load(SavePath) != Error.Ok) return;
        Muted = (bool)cf.GetValue("audio", "muted", false);
        BestApples = (int)cf.GetValue("record", "best_apples", -1);
        BestTime = (float)cf.GetValue("record", "best_time", -1f);
        Wins = (int)cf.GetValue("record", "wins", 0);
    }

    private void SavePrefs()
    {
        var cf = new ConfigFile();
        cf.SetValue("audio", "muted", Muted);
        cf.SetValue("record", "best_apples", BestApples);
        cf.SetValue("record", "best_time", BestTime);
        cf.SetValue("record", "wins", Wins);
        cf.Save(SavePath);
    }
}
