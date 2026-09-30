shader_type canvas_item;
render_mode unshaded;

uniform sampler2D previous_fog : filter_linear, repeat_disable;
uniform float step_blend : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    float previous_alpha = texture(previous_fog, UV).a;
    float current_alpha = texture(TEXTURE, UV).a;
    COLOR = vec4(0.0, 0.0, 0.0, mix(previous_alpha, current_alpha, step_blend));
}