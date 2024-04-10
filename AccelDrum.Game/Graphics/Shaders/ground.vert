#version 330 core

layout (location = 0) in vec3 pos;
layout (location = 1) in vec4 color;
layout (location = 2) in vec2 texPos;
layout (location = 3) in uint texId;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec2 vTexCoord;

void main()
{
    // Transform vertices
    gl_Position = projection * view * model * vec4(pos, 1.0);
    
    // Pass texture coordinates to fragment shader
    vTexCoord = pos.xz; // Scale and shift to [0, 1] range
}