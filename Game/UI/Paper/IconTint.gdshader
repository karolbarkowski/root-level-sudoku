shader_type canvas_item;

// Paints each draw in its own draw colour and keeps only the texture's coverage, so a black SVG icon
// can be tinted any colour. Flat shapes and text already use white textures, so they look unchanged.
varying vec4 tint;

void vertex() {
	// Vertex colour here already includes the node's modulate, so fades keep working.
	tint = COLOR;
}

void fragment() {
	COLOR = vec4(tint.rgb, tint.a * texture(TEXTURE, UV).a);
}
