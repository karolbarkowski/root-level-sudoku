shader_type canvas_item;

// A soft, uneven vertical gradient around the ColorRect's colour: that colour is the middle tone,
// the top lifts a little and the bottom sinks darker. The boundary between light and dark is bent
// by slow waves, and two faint pools (lighter high on the left, deeper low on the right) keep it
// from reading as a straight ramp.

// How much lighter the top gets, and how much darker the bottom, as a share of the base colour.
uniform float lift = 0.10;
uniform float sink = 0.34;
// How far the waves bend the gradient, in screen heights.
uniform float bend = 0.07;
uniform float pools = 0.06;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float blob(vec2 uv, vec2 centre, vec2 spread) {
	vec2 d = (uv - centre) / spread;
	return exp(-dot(d, d));
}

void fragment() {
	vec2 uv = UV;
	float wave = sin(uv.x * 2.3 + 0.6) * 0.6
		+ sin(uv.x * 5.1 + 2.1) * 0.25
		+ sin((uv.x + uv.y) * 3.7 + 1.3) * 0.15;
	float t = smoothstep(0.0, 1.0, uv.y + wave * bend) * 2.0 - 1.0; // -1 top, 0 middle, 1 bottom
	float shade = t < 0.0 ? -t * lift : -t * sink;
	shade += pools * (blob(uv, vec2(0.18, 0.2), vec2(0.55, 0.3)) - 1.4 * blob(uv, vec2(0.9, 0.92), vec2(0.6, 0.28)));

	vec3 colour = COLOR.rgb * (1.0 + shade);
	// Dark gradients band on 8-bit displays; a pixel of noise hides the steps.
	colour += (hash(FRAGCOORD.xy) - 0.5) / 255.0;
	COLOR = vec4(colour, COLOR.a);
}
