shader_type canvas_item;
render_mode unshaded;

uniform float front = 0.0;
varying vec2 offset;
void vertex() { offset = VERTEX; }

float noise_at(vec2 p) { return fract(sin(dot(p, vec2(12.9898,78.233))) * 43758.5453); }

void fragment() {
    vec2 p = floor(offset / 3.0) * 3.0;
    float radius = length(p);
    float ripple = sin(p.x * 0.09 + p.y * 0.06) * sin(p.y * 0.12) * 6.0;
    float age = front - radius + ripple;
    float grain = noise_at(floor(p / 6.0));
    vec4 flame = vec4(0.0);
    if (age >= 0.0 && age < 8.0)
        flame = vec4(1.0, 0.88, 0.47, 0.9);
    else if (age >= 8.0 && age < 27.0 + grain * 12.0)
        flame = vec4(1.0, 0.30 + grain * 0.27, 0.04, 0.74);
    else if (age >= 27.0 && age < 66.0)
        flame = vec4(0.87, 0.16, 0.04, (1.0 - age / 66.0) * 0.55);
    if (age > 36.0 && age < 84.0 && grain > 0.94)
        flame = vec4(1.0, 0.63, 0.18, 1.0 - age / 84.0);
    COLOR = flame;
}
