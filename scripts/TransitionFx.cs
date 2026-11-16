using Godot;
using System;

/// <summary>
/// 转场层：使用 transition.gdshader（MrEliptik 风格的渐变形状擦除），
/// Run(cb) 时棕色波浪遮罩盖下 → 执行回调（切场景）→ 遮罩掀开。
/// </summary>
public partial class TransitionFx : CanvasLayer
{
    private ColorRect _rect;
    private ShaderMaterial _mat;
    private bool _busy;

    public override void _Ready()
    {
        Layer = 100;

        var gradient = new Gradient
        { Colors = new[] { Colors.White, Colors.Black }, Offsets = new[] { 0f, 1f } };
        var gradientTex = new GradientTexture2D
        { Gradient = gradient, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1) };

        // 纯白形状贴图 = 干净的柔和渐变扫过（泡泡遮罩效果不佳，已回退）
        var img = Image.CreateEmpty(2, 2, false, Image.Format.Rgb8);
        img.Fill(Colors.White);
        var whiteTex = ImageTexture.CreateFromImage(img);

        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/transition.gdshader") };
        _mat.SetShaderParameter("base_color", new Color("54402a"));
        _mat.SetShaderParameter("gradient_texture", gradientTex);
        _mat.SetShaderParameter("shape_texture", whiteTex);
        _mat.SetShaderParameter("gradient_fixed", false);
        _mat.SetShaderParameter("width", 0.25f);
        _mat.SetShaderParameter("shape_tiling", 8f);
        _mat.SetShaderParameter("shape_feathering", 0.15f);
        _mat.SetShaderParameter("factor", 0f);

        Vector2 size = GetViewport().GetVisibleRect().Size;
        _mat.SetShaderParameter("node_resolution", size);

        _rect = new ColorRect
        {
            Color = Colors.White, // shader 覆盖输出
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
            Material = _mat,
        };
        _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    /// <summary>盖下 → 回调 → 掀开。回调里切换游戏状态。scale 仅用于调试放慢。</summary>
    public async void Run(Action mid, float scale = 1f)
    {
        if (_busy) return;
        _busy = true;
        _rect.Visible = true;

        var t = CreateTween();
        t.TweenMethod(Callable.From<float>(v => _mat.SetShaderParameter("factor", v)), 0.0f, 1.0f, 0.32f * scale)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(t, Tween.SignalName.Finished);

        mid?.Invoke();
        await ToSignal(GetTree().CreateTimer(0.06 * scale), "timeout");

        var t2 = CreateTween();
        t2.TweenMethod(Callable.From<float>(v => _mat.SetShaderParameter("factor", v)), 1.0f, 0.0f, 0.32f * scale)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(t2, Tween.SignalName.Finished);

        _rect.Visible = false;
        _busy = false;
    }
}
