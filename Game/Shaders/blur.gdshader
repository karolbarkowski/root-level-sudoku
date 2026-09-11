shader_type canvas_item;

uniform float radius : hint_range(0.0, 8.0) = 2.0;

void fragment()
{
    vec2 px = TEXTURE_PIXEL_SIZE * radius;

    vec4 sum = vec4(0.0);

    for (int x = -2; x <= 2; x++)
    {
        for (int y = -2; y <= 2; y++)
        {
            sum += texture(TEXTURE, UV + vec2(float(x), float(y)) * px);
        }
    }

    COLOR = sum / 25.0;
}