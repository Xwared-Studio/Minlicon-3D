// Unminal Engine - Copyright (C) 2026 Dov1ntc
// Licensed under GNU AGPLv3 with No-Misattribution Addendum
// See LICENSE file for details.
namespace Unminal.Main;

using Unminal.Utils.Diagnostics;
using Unminal.Core.Scripting.Script;
using Unminal.Core.PlayerCamera;
using Unminal.Render.Texture;
using Unminal.Render.Light;
using Unminal.Render.Billboards;
using Unminal.UI.TextRender.TextRenderer;
using Unminal.Utils.Colors;

[SupportedOSPlatform("windows")]
public class Main : GameWindow {
    private readonly Script _userGame;
    private float mouseWhellDelta;
    Matrix4 _model, _view, _projection;
    float _initialFov = MathHelper.PiOver4;
    private Text? _textRenderer;
    private Camera? _activeCameraRef; 
    private Console? gameConsole;
    private LightManager? _lightManager;
    private ILightingPipeline? _lightingPipeline;
    private Dictionary<string, string> _debugTexts = new();
    private float _smoothFps = 60f;
    private Texture2D? _loadingLogoTexture;
    private Texture2D? _loadingTextTexture;
    private float _loadingAlpha = 1f;
    private bool _isLoadingFade;
    private const float LoadingFadeDuration = 1.0f;
    private int _frameTriangles;
    private int _frameDrawCalls;
    private int _drawCallsAccumulator;
    private int _drawCallsPerSecond;
    private double _drawCallsElapsed;
    private const int GpuQueryCount = 4;
    private readonly int[] _gpuTimeQueries = new int[GpuQueryCount];
    private readonly int[] _gpuPrimitiveQueries = new int[GpuQueryCount];
    private int _gpuQueryFrame;
    private bool _gpuTimerQueriesSupported;
    private bool _gpuPrimitiveQueriesSupported;
    private double _gpuTimeMilliseconds = double.NaN;
    private long _gpuPrimitives = -1;
#if DEBUG
private PerformanceMonitor? _perfMonitor;
#endif

    private Vector3 _isoTarget = new Vector3(0, 0, -40);
    private float IsoDistance = 30f;
    private const float IsoAngleX = 35f;
    private float IsoAngleY = 45f;
    private float _cameraMoveSpeed = 20f;
    private float _cameraRotateSpeed = 100f;
    private float _mouseSensitivity = 0.5f;

    // === Режим вида сверху ===
    private bool _topViewMode = false;
    private const float TopViewAngleX = 89.9f;
    private const float TopViewDistance = 60f;

    public Main(Script userGame) : base(
            new GameWindowSettings() { UpdateFrequency = 100000 }, 
            new NativeWindowSettings(){ 
                Location =  new Vector2i(Engine.ConfigManager.GetConfig<int>("LocationX"), Engine.ConfigManager.GetConfig<int>("LocationY")),
                ClientSize = new Vector2i(Engine.ConfigManager.GetConfig<int>("Width"), Engine.ConfigManager.GetConfig<int>("Height")),
                Title = Engine.ConfigManager.GetConfig<string>("Title"),
                APIVersion = new Version(3, 3)
            }
        )
    {
        _userGame = userGame;
        this.TextInput += HandleConsoleTextInput;
        Engine.IsDebug = Engine.ConfigManager.GetConfig<bool>("Debug");
    }

    protected override void OnLoad() {
        base.OnLoad(); 
        gameConsole = new Console();
        Engine.CurrentWindow = this;

        CursorState = CursorState.Normal;

        if (Engine.ConfigManager != null) {
            bool vsync = Engine.ConfigManager.GetConfig<bool>("VSync");
            this.VSync = vsync ? VSyncMode.On : VSyncMode.Off;
        }

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend); 
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        InitializeGpuQueries();

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _loadingLogoTexture = Texture2D.GetOrCreateFileTexture(GetPath.GetCorrectPath("/Assets/textures/EngineLogo.png", true));
        _loadingTextTexture = Texture2D.GetOrCreateFileTexture(GetPath.GetCorrectPath("/Assets/textures/loading.png", true));

        DrawLoadingScreen(1f);
        Context.SwapBuffers();

        _textRenderer = new Text(
            GetPath.GetCorrectPath(Engine.Paths.Fonts.VCR_OSD_MONO, true),
            256,
            GetPath.GetCorrectPath(Engine.Paths.Shaders.textV, true),
            GetPath.GetCorrectPath(Engine.Paths.Shaders.textF, true)
        );

        Billboard.Initialize(
            GetPath.GetCorrectPath("Assets/shaders/billboard.vert", true), 
            GetPath.GetCorrectPath("Assets/shaders/billboard.frag", true)
        );
        
        _model = Matrix4.Identity;
        _projection = Matrix4.CreatePerspectiveFieldOfView(_initialFov, Size.X / (float)Size.Y, 0.1f, 1000.0f);

        _lightManager = new LightManager();

        string lightType = Engine.ConfigManager?.GetConfig<string>("LightType") ?? "Forward-Rendering-With-UBO";

        if (lightType == "Forward-Rendering-With-UBO") {
            _lightingPipeline = new ForwardUBOPipeline(_lightManager);
        } else {
            _lightingPipeline = new ForwardUBOPipeline(_lightManager);
        }

#if DEBUG
_perfMonitor = new PerformanceMonitor();
#endif

        _lightingPipeline.Initialize();

        Engine.LightManager = _lightManager;
        Engine.LightingPipeline = _lightingPipeline;

        _userGame.Load(_projection);

        _activeCameraRef = _userGame.ActiveCamera;

        if (_activeCameraRef == null) {
            _activeCameraRef = new Camera(new Vector3(0, 5, 10), -90.0f, 0.0f);
            _userGame.ActiveCamera = _activeCameraRef;
        }

        _view = _activeCameraRef.GetViewMatrix();
        _loadingAlpha = 1f;
        _isLoadingFade = true;
        
        if (Engine.ConfigManager == null) return;
        Engine.CanF3 = Engine.ConfigManager.GetConfig<bool>("Canf3");
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        
        if (MouseState.IsButtonDown(MouseButton.Right) && gameConsole != null && !gameConsole.IsOpen)
        {
            IsoAngleY -= e.DeltaX * _mouseSensitivity;
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs e) {
        base.OnUpdateFrame(e);
        KeyboardState input = KeyboardState;
        MouseState mouse = MouseState;

        if (!IsFocused) return;

        if (input.IsKeyReleased(Keys.Escape)) {
            Engine.GlobalWindowState.InPause = !Engine.GlobalWindowState.InPause;
            CursorState = CursorState.Normal;
        }

        if (gameConsole == null) throw new Exception("[#red][ERROR]: Console Is Null"); 

        Engine.Player.CameraObj = _activeCameraRef;
        if (Engine.Player.CameraObj == null) throw new Exception("[#red][ERROR]: Something went wrong, camera is null: see file main.cs (line ~102)");

        if (input.IsKeyReleased(Keys.F11)) {
            Engine.GlobalWindowState.IsFullScreen = !Engine.GlobalWindowState.IsFullScreen;
            WindowState = Engine.GlobalWindowState.IsFullScreen ? WindowState.Fullscreen : WindowState.Normal;
        }

        gameConsole.ProcessInput(input);
        Engine.GlobalWindowState.InConsole = gameConsole.IsOpen;
        if (gameConsole != null && gameConsole.IsOpen) {
            CursorState = CursorState.Normal;
        } else {
            if (input.IsKeyReleased(Keys.F3)) { 
                if (Engine.CanF3) {
                    Engine.GlobalWindowState.InDebugMenu = !Engine.GlobalWindowState.InDebugMenu;
                } else {
                    Log.Create(Log.LogType.WARNING, "No permission to use the debug menu");
                }
            }

            CursorState = CursorState.Normal;
        }

        if (!Engine.GlobalWindowState.InConsole)
        {
            float delta = (float)e.Time * _cameraMoveSpeed;
            if (input.IsKeyDown(Keys.W)) _isoTarget.Z -= delta;
            if (input.IsKeyDown(Keys.S)) _isoTarget.Z += delta;
            if (input.IsKeyDown(Keys.A)) _isoTarget.X -= delta;
            if (input.IsKeyDown(Keys.D)) _isoTarget.X += delta;

            float rotDelta = (float)e.Time * _cameraRotateSpeed;
            if (input.IsKeyDown(Keys.Q)) IsoAngleY -= rotDelta;
            if (input.IsKeyDown(Keys.E)) IsoAngleY += rotDelta;

            if (_isoTarget.X < -20f) _isoTarget.X = -20f;
            if (_isoTarget.X > 20f) _isoTarget.X = 20f;
            if (_isoTarget.Z < -60f) _isoTarget.Z = -60f;
            if (_isoTarget.Z > -20f) _isoTarget.Z = -20f;
        }

        float radX = MathHelper.DegreesToRadians(_topViewMode ? TopViewAngleX : IsoAngleX);
        float radY = MathHelper.DegreesToRadians(IsoAngleY);
        float dist = _topViewMode ? TopViewDistance : IsoDistance;

        Vector3 offset = new Vector3(
            dist * (float)Math.Cos(radX) * (float)Math.Sin(radY),
            dist * (float)Math.Sin(radX),
            dist * (float)Math.Cos(radX) * (float)Math.Cos(radY)
        );
        Vector3 cameraPos = _isoTarget + offset;
        _view = Matrix4.LookAt(cameraPos, _isoTarget, Vector3.UnitY);
        _projection = Matrix4.CreatePerspectiveFieldOfView(_initialFov, Size.X / (float)Size.Y, 0.1f, 1000.0f);

        if (_activeCameraRef != null) {
            _activeCameraRef.Position = cameraPos;
        }

        Engine.Projection = _projection;
        Engine.View = _view;
        Engine.CurrentKeyboard = input;
        Engine.CurrentMouse = mouse;
        Engine.CurrentCursorState = CursorState;
        Engine.WindowSize = new Vector2i(Size.X, Size.Y);

        // === Переключение вида сверху по ЛКМ (обрабатываем здесь, чтобы Script знал режим) ===
        bool isMousePressed = mouse.IsButtonDown(MouseButton.Left);
        if (isMousePressed && !_prevMousePressed) {
            _topViewMode = !_topViewMode;
            Console.WriteLine($"[DEBUG] TopViewMode = {_topViewMode}");
        }
        _prevMousePressed = isMousePressed;

        if (gameConsole == null || !gameConsole.IsOpen) {
            _userGame.Update();
            if (_userGame.ActiveCamera != null) {
                _activeCameraRef = _userGame.ActiveCamera;
            }
            _model = Matrix4.Identity;
        }

        mouseWhellDelta = 0f;
    }

    private bool _prevMousePressed = false;

    private void HandleConsoleTextInput(TextInputEventArgs e) {
        if (gameConsole != null && gameConsole.IsOpen) {
            string text = e.AsString; 
            
            if (!string.IsNullOrEmpty(text) && text[0] >= 32) {
                gameConsole.AppendToCommand(text);
            }
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e) {  
        base.OnRenderFrame(e);
        _frameTriangles = 0;
        _frameDrawCalls = 0;
        Engine.WindowSize = new Vector2i(Size.X, Size.Y);
        Engine.DeltaTime = (float)e.Time;
        Engine.TotalTime += e.Time;

        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_isLoadingFade) {
            _loadingAlpha -= (float)e.Time / LoadingFadeDuration;
            if (_loadingAlpha <= 0f) {
                _loadingAlpha = 0f;
                _isLoadingFade = false;
                _loadingLogoTexture = null;
                _loadingTextTexture = null;
            }

            if (_loadingAlpha > 0f)
                DrawLoadingScreen(_loadingAlpha);
            Context.SwapBuffers();
            return;
        }

        _lightingPipeline?.BeginFrame();
        ReadGpuQueryResults();
        BeginGpuQueries();
        _userGame.Draw();
        EndGpuQueries();

        _drawCallsElapsed += e.Time;
        _drawCallsAccumulator += _frameDrawCalls;

        if (_drawCallsElapsed >= 1.0)
        {
            _drawCallsPerSecond = (int)(_drawCallsAccumulator / _drawCallsElapsed);

            _drawCallsAccumulator = 0;
            _drawCallsElapsed = 0;
        }

        if (gameConsole != null && gameConsole.IsOpen) {
            gameConsole.DrawConsole(Size.X, Size.Y);
        } 

        Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, Size.X, Size.Y, 0, -1, 1);
        Engine.Ortho = ortho;
        float currentFps = 1.0f / Engine.DeltaTime;
        _smoothFps = _smoothFps + (currentFps - _smoothFps) * 0.05f;
        
        if (_textRenderer != null && Engine.GlobalWindowState.InDebugMenu && _activeCameraRef != null) {
            float TS = 20f;
            _debugTexts["name"] = $"Unminal V0.2.4-prerelease-2";
            _debugTexts["fps"] = $"LERP FPS: {_smoothFps:F0}";
            _debugTexts["pos&fov"] = string.Format(CultureInfo.InvariantCulture,
                "Pos: {0:F1} {1:F1} {2:F1} | FOV: {3}", 
                _activeCameraRef.Position.X, _activeCameraRef.Position.Y, _activeCameraRef.Position.Z, 
                MathHelper.RadiansToDegrees(_activeCameraRef.FOV));
            _debugTexts["direction"] = string.Format(CultureInfo.InvariantCulture, 
                "Dir: {0:F1} {1:F1} {2:F1}", 
                _activeCameraRef.Front.X, _activeCameraRef.Front.Y, _activeCameraRef.Front.Z);
            _debugTexts["pc-Resources"] = "PC resources: Telemetry Disabled";
            string gpuPrimitives = _gpuPrimitives >= 0 ? _gpuPrimitives.ToString(CultureInfo.InvariantCulture) : "N/A";
            string gpuTime = !double.IsNaN(_gpuTimeMilliseconds)
                ? $"{_gpuTimeMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms"
                : "N/A";
            _debugTexts["triangles"] =
                $"Triangles: {_frameTriangles} | GPU Primitives: {gpuPrimitives}";
            _debugTexts["draw-calls"] =
                $"Draw Calls: {_drawCallsPerSecond}/sec | GPU Time: {gpuTime}";
#if DEBUG
            if (_perfMonitor != null)
                _debugTexts["pc-Resources"] = $"{_perfMonitor.GetMemory()} | {_perfMonitor.GetCPU()} | {_perfMonitor.GetGPU()}";
#endif
            _textRenderer.DrawString(_debugTexts["name"], 10, 5, TS, new Vector4(Colors.White, 1f), 2f);
            _textRenderer.DrawString(_debugTexts["fps"], 10, 30, TS, new Vector4(Colors.White, 1f), 1f); 
            _textRenderer.DrawString(_debugTexts["pos&fov"], 10, 55, TS, new Vector4(Colors.White, 1f), 1f);
            _textRenderer.DrawString(_debugTexts["direction"], 10, 80, TS, new Vector4(Colors.White, 1f), 1f); 
            _textRenderer.DrawString(_debugTexts["pc-Resources"], 10, 105, TS, new Vector4(Colors.White, 1f), 1f);
            _textRenderer.DrawString(_debugTexts["triangles"], 10, 130, TS, new Vector4(Colors.White, 1f), 1f);
            _textRenderer.DrawString(_debugTexts["draw-calls"], 10, 155, TS, new Vector4(Colors.White, 1f), 1f);
        }
        Context.SwapBuffers();
    }

    private void DrawLoadingScreen(float alpha) {
        if (_loadingLogoTexture == null || _loadingTextTexture == null) return;

        const float spacing = 24f;
        float groupHeight = _loadingLogoTexture.Height + spacing + _loadingTextTexture.Height;
        float logoX = (Size.X - _loadingLogoTexture.Width) * 0.5f;
        float logoY = (Size.Y - groupHeight) * 0.5f;
        float textX = (Size.X - _loadingTextTexture.Width) * 0.5f;
        float textY = logoY + _loadingLogoTexture.Height + spacing;

        _loadingLogoTexture.Draw2D(logoX, logoY, Size.X, Size.Y, alpha);
        _loadingTextTexture.Draw2D(textX, textY, Size.X, Size.Y, alpha);
    }

    protected override void OnResize(ResizeEventArgs e) {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _projection = Matrix4.CreatePerspectiveFieldOfView(_initialFov, Size.X / (float)Size.Y, 0.1f, 1000.0f);
    }

    protected override void OnUnload() {
        DisposeGpuQueries();
        _textRenderer?.Dispose();
        
        _lightingPipeline?.Dispose();
        _lightManager?.Dispose();
        
        base.OnUnload();
        _userGame.Unload();
        int currentX = this.Location.X;
        int currentY = this.Location.Y;
        Engine.ConfigManager?.SetConfig(newLocationX: $"{currentX}");
        Engine.ConfigManager?.SetConfig(newLocationY: $"{currentY}");
        Engine.ConfigManager?.SetConfig(newDebug: $"{Engine.IsDebug}");
        Engine.ConfigManager?.SetConfig(newWidth: $"{Engine.WindowSize.X}");
        Engine.ConfigManager?.SetConfig(newHeight: $"{Engine.WindowSize.Y}");
#if DEBUG
_perfMonitor!.Dispose();
#endif
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e) {
        mouseWhellDelta += e.OffsetY;
        base.OnMouseWheel(e);
        
        if (gameConsole != null && !gameConsole.IsOpen) {
            IsoDistance -= e.OffsetY * 2f;
            
            if (IsoDistance < 5f) IsoDistance = 5f;
            if (IsoDistance > 100f) IsoDistance = 100f;
        } 
    }

    public float GetMouseWhellDelta() => mouseWhellDelta;

    public void RegisterDraw(int indexCount)
    {
        _frameTriangles += indexCount / 3;
        _frameDrawCalls++;
    }

    public void RegisterMeshLoaded(int indexCount) { }
    public void RegisterMeshUnloaded(int indexCount) { }

    private void InitializeGpuQueries()
    {
        string version = GL.GetString(StringName.Version) ?? string.Empty;
        bool core33 = Version.TryParse(version.Split(' ')[0], out Version? glVersion)
            && glVersion >= new Version(3, 3);
        string extensions = GL.GetString(StringName.Extensions) ?? string.Empty;

        _gpuTimerQueriesSupported = core33 || extensions.Contains("GL_ARB_timer_query", StringComparison.Ordinal);
        _gpuPrimitiveQueriesSupported = core33 || extensions.Contains("GL_ARB_transform_feedback2", StringComparison.Ordinal);

        if (_gpuTimerQueriesSupported)
            GL.GenQueries(GpuQueryCount, _gpuTimeQueries);
        if (_gpuPrimitiveQueriesSupported)
            GL.GenQueries(GpuQueryCount, _gpuPrimitiveQueries);
    }

    private void BeginGpuQueries()
    {
        int queryIndex = _gpuQueryFrame % GpuQueryCount;
        if (_gpuTimerQueriesSupported)
            GL.BeginQuery(QueryTarget.TimeElapsed, _gpuTimeQueries[queryIndex]);
        if (_gpuPrimitiveQueriesSupported)
            GL.BeginQuery(QueryTarget.PrimitivesGenerated, _gpuPrimitiveQueries[queryIndex]);
    }

    private void EndGpuQueries()
    {
        if (_gpuPrimitiveQueriesSupported)
            GL.EndQuery(QueryTarget.PrimitivesGenerated);
        if (_gpuTimerQueriesSupported)
            GL.EndQuery(QueryTarget.TimeElapsed);
        _gpuQueryFrame++;
    }

    private void ReadGpuQueryResults()
    {
        if (_gpuQueryFrame < GpuQueryCount) return;
        int queryIndex = (_gpuQueryFrame - GpuQueryCount) % GpuQueryCount;

        if (_gpuTimerQueriesSupported && IsQueryReady(_gpuTimeQueries[queryIndex])) {
            GL.GetQueryObject(_gpuTimeQueries[queryIndex], GetQueryObjectParam.QueryResult, out long nanoseconds);
            _gpuTimeMilliseconds = nanoseconds / 1_000_000.0;
        }

        if (_gpuPrimitiveQueriesSupported && IsQueryReady(_gpuPrimitiveQueries[queryIndex])) {
            GL.GetQueryObject(_gpuPrimitiveQueries[queryIndex], GetQueryObjectParam.QueryResult, out long primitives);
            _gpuPrimitives = primitives;
        }
    }

    private static bool IsQueryReady(int query)
    {
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResultAvailable, out int available);
        return available != 0;
    }

    private void DisposeGpuQueries()
    {
        if (_gpuTimerQueriesSupported)
            GL.DeleteQueries(GpuQueryCount, _gpuTimeQueries);
        if (_gpuPrimitiveQueriesSupported)
            GL.DeleteQueries(GpuQueryCount, _gpuPrimitiveQueries);
    }
}

public static class TypeExtensions {
    public static object? GetDefaultValue(this Type type) {
        if (type == typeof(string)) return string.Empty;
        if (type.IsValueType) return Activator.CreateInstance(type);
        if (type == typeof(bool)) return false;
        return null;
    }
}