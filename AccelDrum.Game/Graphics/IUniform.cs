using AccelDrum.Game.Graphics.Shaders;

namespace AccelDrum.Game.Graphics;

public interface IUniform
{
    string Name { get; }
    void Update(Shader shader);
    object Get();
}
