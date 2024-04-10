#version 330 core
#include "utils.h.vert"

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec2 aTexPos;
layout (location = 3) in uint aTexId;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform float time;

out vec4 fragColor;
out vec2 texPos;
flat out uint texId;

void main(void)
{
    texPos = aTexPos;
    texId = aTexId;

    gl_Position = projection * view * model * vec4(aPos, 1.0);
    
    fragColor = aColor;
    if (aColor.x >= -500 && aColor.x <= -400)
    {
        fragColor = hsv2rgb(vec4(
            mod(time + aColor.x + 500, 1), aColor.yzw));
    }
}
