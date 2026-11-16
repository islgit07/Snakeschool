using Godot;

/// <summary>
/// 液态进度条（改编自 MrEliptik/godot_ui_components 的 loading_fluid，MIT）。
/// 用作体重条：液面高度 = 当前体重占比，两层波浪持续起伏。
/// </summary>
public partial class LiquidBar : ColorRect
{
    private ShaderMaterial _mat;

    public float Progress
    {
        get => (float)_mat.GetShaderParameter("progress").AsSingle();
        set => _mat.SetShaderParameter("progress", Mathf.Clamp(value, 0f, 1f));
    }

    public override void _Ready()
    {
        Color = Colors.White; // 实际颜色由 shader 输出
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/liquid_bar.gdshader") };
        _mat.SetShaderParameter("bg_color", new Color("e9d3a3"));
        _mat.SetShaderParameter("wave_1_color", new Color("8fbf4d"));
        _mat.SetShaderParameter("wave_2_color", new Color("aede66"));
        _mat.SetShaderParameter("wave_seperation", 0.02f);
        _mat.SetShaderParameter("progress", 1f);
        Material = _mat;
    }
}
