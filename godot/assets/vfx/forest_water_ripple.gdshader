shader_type canvas_item;

uniform sampler2D water_frames : source_color, filter_nearest;
uniform float frame_rate = 5.0;
uniform float blend_strength : hint_range(0.0, 1.0) = 0.18;

varying vec2 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 base_color = COLOR;
	float water_mask = step(0.025, base_color.b)
		* step(base_color.r * 1.35, base_color.g)
		* step(base_color.g * 1.2, base_color.b);

	if (water_mask > 0.5) {
		vec2 local_uv = fract(world_position / 64.0);
		float frame = mod(floor(TIME * frame_rate), 4.0);
		vec2 frame_uv = vec2((frame + local_uv.x) / 4.0, local_uv.y);
		vec3 ripple_color = texture(water_frames, frame_uv).rgb;
		base_color.rgb = mix(base_color.rgb, ripple_color, blend_strength);
	}

	COLOR = base_color;
}
