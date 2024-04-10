#version 330 core

in vec4 fragColor;
in vec2 texPos;
flat in uint texId;

uniform vec3 ambientColor;
uniform float ambientStrength;
uniform sampler2D tex;

out vec4 outputColor;

void main()
{
    vec4 color;
    if (texId > 0u)
        color = texture(tex, texPos);
    else
        color = fragColor;

    vec3 ambient = ambientStrength * ambientColor;

    color *= vec4(ambient, 1.0);
    
    outputColor = color;
}