#version 330 core

in vec2 vTexCoord;

uniform vec3 ambientColor;
uniform float ambientStrength;

out vec4 outputColor;

void main()
{
    // Checkerboard pattern
    vec2 tileCoords = floor(vTexCoord * 1.0); // How many squares in a unit
    vec3 color = (mod(tileCoords.x + tileCoords.y, 2.0) + 1.0) * vec3(0.3, 0.3, 0.3);
    
    vec3 ambient = ambientStrength * ambientColor;

    color *= ambient;
    
    outputColor = vec4(color, 1.0);
}
