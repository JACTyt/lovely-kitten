// Recolours the cat sheet's three greys (light #e0e0e0, mid #b5b5b5, outline #2f2f2e). Other colours pass through.
shader_type canvas_item;

uniform vec4 light_col : source_color = vec4(0.878, 0.878, 0.878, 1.0);
uniform vec4 mid_col : source_color = vec4(0.71, 0.71, 0.71, 1.0);
uniform vec4 dark_col : source_color = vec4(0.184, 0.184, 0.18, 1.0);

varying vec4 modulate;

void vertex() {
	modulate = COLOR;
}

void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec3 o = c.rgb;
	if (abs(c.r - c.g) < 0.03 && abs(c.g - c.b) < 0.03) {
		if (c.r > 0.8) o = light_col.rgb;
		else if (c.r > 0.55) o = mid_col.rgb;
		else if (c.r < 0.25) o = dark_col.rgb;
	}
	COLOR = vec4(o, c.a) * modulate;
}
