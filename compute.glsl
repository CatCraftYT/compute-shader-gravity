#[compute]
#version 450

const float GRAV_CONSTANT = 0.001;//6.6743015e-11;
const int N_PARTICLES = 18000;
const float MAX_IMPULSE = 0.00001;
const float EXPANSION_FACTOR = -0.00001;//0.000002;

// Invocations in the (x, y, z) dimension
layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

// A binding to the buffer we create in our script
layout(set = 0, binding = 0, std430) restrict buffer PositionDataBuffer {
    float data[];
}
position_buffer;

layout(set = 0, binding = 1, std430) restrict buffer VelocityDataBuffer {
    float data[];
}
velocity_buffer;

vec2 GetVelocityVector(uint index) {
    return vec2(velocity_buffer.data[index], velocity_buffer.data[index + N_PARTICLES]);
}
void AddVelocityVector(vec2 value, uint index) {
    velocity_buffer.data[index] += value.x;
    velocity_buffer.data[index + N_PARTICLES] += value.y;
}

vec2 GetPositionVector(uint index) {
    return vec2(position_buffer.data[index], position_buffer.data[index + N_PARTICLES]);
}

/*
// Vector-based
void main() {
    if (gl_GlobalInvocationID.x > N_PARTICLES) { return; }
    vec2 position = GetPositionVector(gl_GlobalInvocationID.x);
    
    for (uint i = 0; i < N_PARTICLES; i++)
    {
        if (i == gl_GlobalInvocationID.x) { continue; }
        vec2 otherPosition = GetPositionVector(i);
        vec2 displacement = otherPosition - position;
        AddVelocityVector(GRAV_CONSTANT / pow(length(displacement), 3) * displacement, gl_GlobalInvocationID.x);
    }
}
*/

// Impulse-based
void main() {
    if (gl_GlobalInvocationID.x > N_PARTICLES) { return; }
    vec2 position = GetPositionVector(gl_GlobalInvocationID.x);
    
    for (uint i = 0; i < N_PARTICLES; i++)
    {
        if (i == gl_GlobalInvocationID.x) { continue; }
        vec2 otherPosition = GetPositionVector(i);

        // https://en.wikipedia.org/wiki/Newton's_law_of_universal_gravitation
        vec2 directionVec = otherPosition - position;
        float distSqr = dot(directionVec, directionVec);
        vec2 directionUnitVec = normalize(directionVec);

        float impulse = min(MAX_IMPULSE, GRAV_CONSTANT / distSqr);

        AddVelocityVector(impulse * directionUnitVec, gl_GlobalInvocationID.x);
    }
    AddVelocityVector(position * EXPANSION_FACTOR, gl_GlobalInvocationID.x);

}
