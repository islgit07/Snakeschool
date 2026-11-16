using Godot;
using System;

/// <summary>
/// Q弹开关（借鉴 MrEliptik/godot_ui_components 的 squishy_toggle 的动画节奏）。
/// 木槽 + 奶油旋钮：滑动 + 挤压 + 微旋转，用于音效开关。
/// </summary>
public partial class SquishyToggle : Control
{
    public bool IsOn { get; private set; }
    public event Action<bool> Toggled;

    private Panel _trackOff;
    private Panel _trackOn;
    private TextureRect _knob;
    private Tween _tween;

    private const float Duration = 0.22f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(96, 48);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;

        _trackOff = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _trackOff.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _trackOff.AddThemeStyleboxOverride("panel", TrackStyle(UiKit.TrackBrown));
        AddChild(_trackOff);

        _trackOn = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _trackOn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _trackOn.AddThemeStyleboxOverride("panel", TrackStyle(UiKit.TrackGreen));
        AddChild(_trackOn);

        _knob = new TextureRect
        {
            Texture = UiKit.KnobCream,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        _knob.Size = new Vector2(38, 40);
        _knob.Position = new Vector2(3, 4);
        _knob.PivotOffset = new Vector2(19, 20);
        AddChild(_knob);
    }

    private static StyleBoxTexture TrackStyle(AtlasTexture region)
    {
        var sb = new StyleBoxTexture { Texture = region.Atlas, RegionRect = region.Region };
        sb.TextureMarginLeft = 7; sb.TextureMarginRight = 7;
        sb.TextureMarginTop = 7; sb.TextureMarginBottom = 7;
        return sb;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            SetOn(!IsOn, silent: false);
        }
    }

    /// <summary>外部同步状态（不触发事件）。</summary>
    public void SetOn(bool on, bool silent = true)
    {
        IsOn = on;
        _trackOff.Visible = !on;
        _trackOn.Visible = on;
        _knob.Texture = on ? UiKit.KnobGreen : UiKit.KnobCream;

        if (_tween != null && _tween.IsRunning()) _tween.Kill();
        float targetX = on ? Size.X - _knob.Size.X - 3f : 3f;
        _tween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        _tween.Parallel().TweenProperty(_knob, "position:x", targetX, Duration);
        // Q 弹挤压
        _tween.Parallel().TweenProperty(_knob, "scale", new Vector2(0.78f, 1.24f), Duration * 0.4f);
        _tween.Parallel().TweenProperty(_knob, "rotation_degrees", on ? 360f : 0f, Duration);
        _tween.TweenProperty(_knob, "scale", Vector2.One, Duration * 0.6f)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Elastic);

        if (!silent)
        {
            Toggled?.Invoke(on);
            AudioManager.Instance?.PlayMinor(AudioBank.Click, on ? 1.2f : 0.9f);
        }
    }
}
