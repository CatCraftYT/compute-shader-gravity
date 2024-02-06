shader_type canvas_item;
uniform sampler2D positionData;

void fragment() {
	float t = texture(positionData, UV).r * 2.0;
	COLOR = mix(vec4(0,0,0,1), vec4(0.7,0.7,1,1), t);
	//COLOR = vec4(t,t,t,1);
}
