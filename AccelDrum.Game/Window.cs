using AccelDrum.Game.Extensions;
using AccelDrum.Game.Graphics;
using AccelDrum.Game.Graphics.Shaders;
using AccelDrum.Game.Graphics.Textures;
using AccelDrum.Game.Utils;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Diagnostics;
using System.Linq;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace AccelDrum.Game;

public class Window : GameWindow
{
    private bool _disposed = false;
    private ImGuiController _controller = null!;
    private Vector3 _color = new Vector3(0.5f, 0.5f, 0.0f);
    private Camera _camera = null!;
    private Vector2 _lastMousePos;
    private float hue = 0.0f;
    private double lastFps = 0.0;
    private double lastFrametime = 0.0;
    private bool vsync = true;
    private Vector3 ambientColor = new(1);
    private float ambientStrength = 0.5f;
    private Stopwatch gameTimer = Stopwatch.StartNew();
    private bool demoWindow = false;
    private AccelDevice accel = null!;
    private SimpleFixedSizeHistoryQueue<float> frametimeHistory = new(500);

    private MeshManager meshManager = new();
    private Shader shaderMain = null!;
    private Shader shaderGround = null!;
    private Uniform<Matrix4> uniformView = null!;
    private Uniform<Matrix4> uniformProjection = null!;
    private Uniform<Vector3> uniformAmbientColor = null!;
    private Uniform<float> uniformAmbientStrength = null!;
    private Uniform<float> uniformTime = null!;
    private Mesh meshMain = null!;
    private Mesh meshCubes = null!;
    private Mesh meshGround = null!;
    private Mesh meshDebug = null!;
    private Texture textureContainer = null!;
    private Texture textureCea7d = null!;

    public Window() : base(
        GameWindowSettings.Default,
        new NativeWindowSettings()
        {
            ClientSize = new Vector2i(1024, 768),
            APIVersion = new Version(3, 3),
            NumberOfSamples = 0
        }
    )
    { }

    protected override void OnLoad()
    {
        base.OnLoad();
        changeWindowTitle();
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

        shaderMain = meshManager.CreateShader("main", "Graphics/Shaders/main.vert", "Graphics/Shaders/main.frag");
        shaderGround = meshManager.CreateShader("ground", "Graphics/Shaders/ground.vert", "Graphics/Shaders/ground.frag");

        meshMain = meshManager.CreateMesh("main", shaderMain);
        meshCubes = meshManager.CreateMesh("cubes", shaderMain);
        meshGround = meshManager.CreateMesh("ground", shaderGround);
        meshDebug = meshManager.CreateMesh("debug", shaderMain);

        uniformView = meshManager.CreateGlobalUniform<Matrix4>("view");
        uniformProjection = meshManager.CreateGlobalUniform<Matrix4>("projection");
        uniformAmbientColor = meshManager.CreateGlobalUniform<Vector3>("ambientColor");
        uniformAmbientStrength = meshManager.CreateGlobalUniform<float>("ambientStrength");
        uniformTime = meshManager.CreateGlobalUniform<float>("time");

        DebugRenderer.Init(meshManager, meshDebug);

        const float groundSizeHalf = 10;
        meshGround.Vertices.AddRange(ShapeUtils.Quad(
            new Vector3(-groundSizeHalf, 0, -groundSizeHalf),
            new Vector3(groundSizeHalf, 0, -groundSizeHalf),
            new Vector3(groundSizeHalf, 0, groundSizeHalf),
            new Vector3(-groundSizeHalf, 0, groundSizeHalf)
        ));
        meshGround.Position = new Vector3(0, -0.01f, 0);

        textureContainer = meshManager.CreateTexture("container", "Resources/container.jpg");
        textureCea7d = meshManager.CreateTexture("cea7d", "Resources/cea7d.png");

        initImGuiController();
        ImGui.GetStyle().WindowRounding = 9;
        ImGui.GetStyle().ChildRounding = 9;
        ImGui.GetStyle().FrameRounding = 9;
        ImGui.GetStyle().PopupRounding = 9;
        ImGui.GetStyle().GrabRounding = 9;

        _camera = new(new Vector3(0, 2, 2), (float)ClientSize.X / ClientSize.Y);

        this.VSync = VSyncMode.On;
        //this.UpdateFrequency = 70.0;

        accel = new AccelDevice(meshManager);
        DebugRenderer.Ins.AddAllMeshes();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        // Update the opengl viewport
        Vector2i fb = FramebufferSize;
        GL.Viewport(0, 0, fb.X, fb.Y);

        _camera.AspectRatio = (float)FramebufferSize.X / FramebufferSize.Y;

        // Tell ImGui of the new size
        _controller.WindowResized(ClientSize.X, ClientSize.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        UpdateInput(e);
        UpdateState(e);
        UpdateGui(e);
    }

    private void UpdateInput(FrameEventArgs e)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape))
            this.Close();
        if (KeyboardState.IsKeyPressed(Keys.F9))
            demoWindow = !demoWindow;

        if (CursorState == CursorState.Grabbed)
        {
            var input = KeyboardState;

            float cameraSpeed = 2.5f;

            if (input.IsKeyDown(Keys.LeftControl))
                cameraSpeed *= 2.5f;

            var front2D = _camera.Front;
            front2D.Y = 0;
            front2D.Normalize();
            if (input.IsKeyDown(Keys.W))
                _camera.Position += front2D * cameraSpeed * (float)e.Time; // Forward
            if (input.IsKeyDown(Keys.S))
                _camera.Position -= front2D * cameraSpeed * (float)e.Time; // Backwards

            if (input.IsKeyDown(Keys.A))
                _camera.Position -= _camera.Right * cameraSpeed * (float)e.Time; // Left
            if (input.IsKeyDown(Keys.D))
                _camera.Position += _camera.Right * cameraSpeed * (float)e.Time; // Right

            var upDelta = new Vector3(0, cameraSpeed * (float)e.Time, 0);
            if (input.IsKeyDown(Keys.Space))
                _camera.Position += upDelta; // Up
            if (input.IsKeyDown(Keys.LeftShift))
                _camera.Position -= upDelta; // Down
        }

        hue += (float)e.Time * 32.0f;
        hue %= 360.0f;
        _color = ColorUtils.HSLToRGB(new Vector3(hue / 360.0f, 1, 0.5f));
        if (_camera.Position.Y < 0.1f)
        {
            var pos = _camera.Position;
            _camera.Position = new Vector3(pos.X, 0.1f, pos.Z);
        }
    }

    private void UpdateState(FrameEventArgs e)
    {
        const double weight = 0.05;

        lastFps = lastFps * (1 - weight) + (1 / this.UpdateTime) * weight;
        lastFrametime = lastFrametime * (1 - weight) + this.UpdateTime * weight;

        accel.Update();

        frametimeHistory.Push((float)this.UpdateTime * 1000);
    }

    private void UpdateGui(FrameEventArgs e)
    {
        _controller.Update(this, (float)e.Time);
        if (demoWindow)
            ImGui.ShowDemoWindow(ref demoWindow); // Apparently the state is not changing
        MainWindow();
        accel.SerialWindow();
        DebugRenderer.Ins.DebugWindow();
    }

    private void MainWindow()
    {
        if (ImGui.Begin("Main"))
        {
            //ImGui.AlignTextToFramePadding();
            ImGui.Text($"Frametime: {lastFrametime * 1000:n2}ms");
            ImGui.SameLine(200);
            ImGui.Text($"Fps: {lastFps:n1}");
            ImGui.SameLine(300);
            ImGui.Checkbox("Vsync", ref vsync);
            this.VSync = vsync ? VSyncMode.On : VSyncMode.Off;

            ImGui.ColorEdit3("Triangle color", ref _color.InterchangeRef());
            var pos = _camera.Position; ImGui.DragFloat3("Pos", ref pos.InterchangeRef()); _camera.Position = pos;
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
                _camera.Position = new Vector3(0, 2, 2);
            ImGuiRotation("Rotation");
            //var yaw = _camera.Yaw; ImGui.DragFloat("Yaw", ref yaw, 0.5f); _camera.Yaw = yaw;
            //ImGui.SameLine();
            ImGui.PushID(1);
            //if (ImGui.Button("Reset"))
            //(_camera.Yaw, _camera.Pitch) = VectorUtils.ToYawPitch(_camera.Position, Vector3.Zero);
            ImGui.PopID();
            //var pitch = _camera.Pitch; ImGui.DragFloat("Pitch", ref pitch, 0.5f); _camera.Pitch = pitch;
            ImGui.ColorEdit3("Ambient color", ref ambientColor.InterchangeRef());
            ImGui.DragFloat("Ambient strength", ref ambientStrength, 0.005f, 0, 1);
            if (ImGui.Button("Clear"))
            {
                meshMain.Vertices.Clear();
                meshCubes.Clear();
            }
            if (ImGui.Button("Spawn random"))
            {
                foreach (var _ in Enumerable.Range(0, 5))
                {
                    var triangle = ShapeUtils.EquilateralTriangle(1);
                    var transform = Matrix4.CreateFromAxisAngle(Vector3.UnitX, Random.Shared.NextSingle() * float.Pi * 2);
                    transform *= Matrix4.CreateTranslation(
                        (Random.Shared.NextSingle() - 0.5f) * 2 * 2.5f,
                        (Random.Shared.NextSingle() - 0.5f) * 2 * 2.5f,
                        (Random.Shared.NextSingle() - 0.5f) * 2 * 2.5f
                    );
                    triangle = ShapeUtils.Transform(triangle, transform);
                    meshMain.Vertices.AddRange(triangle.Select((vert, i) =>
                    {
                        vert.Color = new Vector4(
                            -500.0f + (i * 0.1f) % 1,
                            1, 1, 1
                        );
                        return vert;
                    }));
                    meshMain.Texture = textureContainer;
                }
            }
            if (ImGui.Button("Spawn cube"))
            {
                //var (v, i) = ShapeUtils.CubeWithTexture(1);
                //meshCubes.Vertices.AddRange(ShapeUtils.Transform(v, Matrix4.CreateTranslation(new Vector3(0, 0.5f, 0))));
                //meshCubes.Indexes.AddRange(i);
                //meshCubes.Texture = textureCea7d4517decdf202278d1cdbab9a1f5c088401ed86df5110dc37976c19e3116;
            }
            {
                System.Numerics.Vector2 size = new(-1, 50);
                ImGui.PlotLines("frametime", ref frametimeHistory.Ref, frametimeHistory.Length,
                    0, null, 0, 1000.0f / 30,
                    size, frametimeHistory.ElementSize);
            }
            ImGui.End();
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        GL.ClearColor(new Color4(0, 32, 48, 255));
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        Matrix4 view = _camera.GetViewMatrix();
        Matrix4 proj = _camera.GetProjectionMatrix();

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.Zero);

        uniformView.Value = view;
        uniformProjection.Value = proj;
        uniformAmbientColor.Value = ambientColor;
        uniformAmbientStrength.Value = ambientStrength;
        uniformTime.Value = (float)gameTimer.Elapsed.TotalSeconds;
        meshGround.Draw();
        meshMain.Draw();
        meshCubes.Draw();
        accel.Draw();
        DebugRenderer.Ins.Draw();

        _controller.Render();
        ImGuiController.CheckGLError("End of frame");

        SwapBuffers();
    }

    private void ImGuiRotation(string label)
    {
        ImGui.PushID(label);
        Vector3 rot = _camera.Rotation;
        bool changed = false;
        ImGui.PushItemWidth(ImGui.CalcItemWidth() / 3);
        changed |= ImGui.DragFloat("##x", ref rot.X, 1.0f, -90 + Camera.Epsilon, 90 - Camera.Epsilon, "%.1f");
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        changed |= ImGui.DragFloat("##y", ref rot.Y, 1.0f, -180, 180, "%.1f");
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        changed |= ImGui.DragFloat("##z", ref rot.Z, 1.0f, -180, 180, "%.1f");
        if (changed)
            _camera.Rotation = rot;

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.Text(label);


        //float deg = q.W * 180;
        ImGui.AlignTextToFramePadding();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.Button("Reset"))
        {
            _camera.Rotation = new Vector3(0, -90, 0);
        }
        ImGui.PopID();
        ImGui.PopItemWidth();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            CursorState = CursorState == CursorState.Normal ? CursorState.Grabbed : CursorState.Normal;
            _lastMousePos = new Vector2(Cursor.X, Cursor.Y);
        }
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (CursorState == CursorState.Grabbed)
        {
            const float sensitivity = 0.2f;

            _camera.Yaw += e.DeltaX * sensitivity;
            _camera.Pitch -= e.DeltaY * sensitivity; // Reversed since y-coordinates range from bottom to top
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (ImGui.GetIO().WantTextInput)
            _controller.PressChar((char)e.Unicode);
        else
        {
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (ImGui.GetIO().WantCaptureMouse)
            _controller.MouseScroll(e.Offset);
        else
        {
            DebugRenderer.Ins.OnMouseWheel(e);
        }
    }

    private void changeWindowTitle()
    {
        Title += ": OpenGL Version: " + GL.GetString(StringName.Version);
    }

    private void initImGuiController()
    {
        // Get the FrameBuffer size and compute the scale factor for ImGuiController
        Vector2i fb = FramebufferSize;
        int scaleFactorX = fb.X / ClientSize.X;
        int scaleFactorY = fb.Y / ClientSize.Y;

        // Instanciate the ImGuiController with the right Scale Factor
        _controller = new ImGuiController(ClientSize.X, ClientSize.Y, scaleFactorX, scaleFactorY);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (!_disposed)
        {
            foreach (IDisposable? disposable in new IDisposable?[]
            {
                _controller,
                accel,
                DebugRenderer.Ins,
                meshManager,
            })
            {
                disposable?.Dispose();
            }

            _disposed = true;
        }
    }
}
