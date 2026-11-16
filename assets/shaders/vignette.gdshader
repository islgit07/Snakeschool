shader_type canvas_item;

// 肥胖警报暗角：体重越高边缘越红，pulse 模拟心跳脉冲
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float pulse : hint_range(0.0, 1.0) = 0.0;
uniform vec4 danger_color : source_color = vec4(0.85, 0.18, 0.12, 1.0);

void fragment() {
	vec2 c = UV - vec2(0.5);
	float d = length(c * vec2(1.0, 1.35)) * 2.0;   // 0 中心 → 边缘
	float edge = smoothstep(0.45, 1.3, d);
	float a = intensity * edge * (0.6 + pulse * 0.42);
	COLOR = vec4(danger_color.rgb, clamp(a, 0.0, 0.6));
}
