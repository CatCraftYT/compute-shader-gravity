shader_type canvas_item;
uniform sampler2D positionData;
uniform vec3 col1 = vec3(0, 0, 0);
uniform vec3 col2 = vec3(0.08, 0.08, 0.2);
uniform vec3 col3 = vec3(1, 0.4, 0.4);
uniform vec3 col4 = vec3(1, 1, 1);

//https://stackoverflow.com/q/47285778
vec3 sample_gradient(float t) {
	vec3 color = mix(col1, col2, smoothstep(0, 0.33, t));
	color = mix(color, col3, smoothstep(0.33, 0.66, t));
	color = mix(color, col4, smoothstep(0.66, 1, t));
	return color;
}

void fragment() {
	float t = texture(positionData, UV).r * 2.0;
	COLOR = vec4(sample_gradient(t), 1);
	//COLOR = vec4(t,t,t,1);
}
