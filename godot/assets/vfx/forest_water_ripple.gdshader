shader_type canvas_item;

uniform sampler2D water_frames : source_color, filter_nearest;
uniform float frame_rate = 5.0;
uniform float blend_strength : hint_range(0.0, 1.0) = 0.28;
uniform float ripple_pattern_size = 96.0;
uniform vec2 flow_velocity = vec2(18.0, 9.0);
uniform vec2 canvas_to_world_x = vec2(1.0, 0.0);
uniform vec2 canvas_to_world_y = vec2(0.0, 1.0);
uniform vec2 canvas_to_world_origin = vec2(0.0, 0.0);

void fragment() {
	vec4 base_color = COLOR;
	vec2 world_position = canvas_to_world_x * FRAGCOORD.x
		+ canvas_to_world_y * FRAGCOORD.y
		+ canvas_to_world_origin;
	float water_mask = step(0.025, base_color.b)
		* step(base_color.r * 1.35, base_color.g)
		* step(base_color.g * 1.2, base_color.b);

	if (water_mask > 0.5) {
		vec2 local_uv = fract((world_position - TIME * flow_velocity) / ripple_pattern_size);
		float frame = mod(floor(TIME * frame_rate), 4.0);
		vec2 frame_uv = vec2((frame + local_uv.x) / 4.0, local_uv.y);
		vec3 ripple_color = texture(water_frames, frame_uv).rgb;
		base_color.rgb = mix(base_color.rgb, ripple_color, blend_strength);
	}

	COLOR = base_color;
}
