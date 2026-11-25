namespace Dov1ntc.MyGameIn3d;

using static Dov1ntc.MyGameIn3d.Scene;
using Unminal.Core.Scripting.Script;
using Unminal.Core.Scripting.Utils;
using Unminal.Core.Commands.Structure;
using Unminal.Core.State;
using Unminal.Core.PlayerCamera;
using Unminal.Core.Commands.Manager;
using Unminal.Render.Light;
using Unminal.Render.Objects;
using Unminal.Render.SkyBox;
using Unminal.Render.Billboards;
using Unminal.UI.TextRender.TextRenderer;
using Unminal.UI.ButtonObject;
using Unminal.UI.ButtonEngine;
using Unminal.UI.ButtonRender;
using Unminal.Render.Primitive._2D;
using Unminal.UI.EventBus;
using Unminal.Utils.Colors;

[SupportedOSPlatform("windows")]
[Script]
public class Game : Script {
    private LightData? _cameraLight;
    private List<GameObject> _objects = new List<GameObject>();
    private Skybox? skybox;
    private Text? _textRenderer;

    private List<Button> _voxelButtons = new List<Button>();
    private List<Vector3> _buttonWorldPositions = new List<Vector3>();

    public override void Load(Matrix4 initialProjection) {
        EventBusUi.Subscribe<ButtonPressedEvent>(OnBtnPress);
        EventBusUi.Subscribe<ButtonHeldEvent>(OnBtnHeld);
        EventBusUi.Subscribe<ButtonRelesedEvent>(OnBtnReleased);

        ActiveCamera = new Camera(new Vector3(0, 0, 0), -90.0f, 0.0f);
        _objects = LoadScene(_objects);

        Engine.LightManager?.ClearLights();

        var cameraLight = new LightData(
            ActiveCamera!.Position,
            Colors.White,
            30f
        );

        _cameraLight = cameraLight;
        Engine.LightManager?.AddLight(cameraLight);

        _textRenderer = new Text("Assets/fonts/PFAgoraSlabPro-Bold.ttf", 256);

        skybox = new Skybox(Engine.Paths.BaseSkyBoxAssets);

        // Координаты вокселей (y = -2, как в Scene.cs)
        Vector3[] spots = {
            new Vector3(-5f, -2f, -40f),
            new Vector3(0f, -2f, -40f),
            new Vector3(5f, -2f, -40f)
        };

        for (int i = 0; i < spots.Length; i++) {
            Square sq = new Square(
                new Vector2(0, 0),
                new Vector2(20, 20), // маленький размер
                new Vector4(0, 0, 0, 0), // полностью прозрачный
                0f
            );

            EngineButton eng = new EngineButton((uint)(i + 1), sq);
            RenderButton rend = new RenderButton(
                new Vector4(0, 0, 0, 0),
                new Vector4(0, 0, 0, 0)
            );
            Button btn = new Button(eng, rend);
            btn.name = $"voxel_btn_{i}";

            _voxelButtons.Add(btn);
            _buttonWorldPositions.Add(spots[i]);
        }

        CommandManager.AddCommand("debug", new Command(){Name = "SayHello", Layer = null!, ExecuteMethod = "SayHello", ExecutedLayer = true});
        CommandManager.AddCommand("debug", new Command(){Name = "Server", Layer = null!, ExecutedLayer = false});
        CommandManager.AddCommand("Server", new Command(){Name = "Open", Layer = null!, ExecuteMethod = "ServerOpen", ExecutedLayer = true});
        CommandManager.AddCommand("Server", new Command(){Name = "Conect", Layer = null!, ExecuteMethod = "ServerConect", ExecutedLayer = true});
    }

    public void OnBtnPress(ButtonPressedEvent e) {
        Console.WriteLine($"[DEBUG] Кнопка {e.ButtonId} нажата (воксель)");
    }
    public void OnBtnHeld(ButtonHeldEvent e) { }
    public void OnBtnReleased(ButtonRelesedEvent e) { }

    public override void Update() {
        base.Update();

        if (Engine.Player.CameraObj == null) return;

        btn1?.Update();
        teapot?.Rotate(90f, "x");

        if (Engine.Player.CameraObj != null && _cameraLight != null) {
            _cameraLight.Position = Engine.Player.CameraObj.Position;
        }

        // Обновляем позиции кнопок по проекции вокселей
        for (int i = 0; i < _voxelButtons.Count; i++) {
            Vector3 worldPos = _buttonWorldPositions[i];
            Vector4 clip = (Engine.View * Engine.Projection) * new Vector4(worldPos, 1f);
            if (clip.W <= 0) continue;

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            Vector2 screen = new Vector2(
                (ndcX + 1f) * 0.5f * Engine.WindowSize.X,
                (1f - ndcY) * 0.5f * Engine.WindowSize.Y
            );

            // Если Square.Position доступен для записи:
            _voxelButtons[i].Bounds.Position = screen;

            _voxelButtons[i].Update();
        }
    }

    public override void Draw() {
        skybox!.Draw();

        foreach (var obj in _objects) obj.Draw();

        // Кнопки невидимы, но их Draw можно не вызывать (или вызывать для отладки)
        // foreach (var btn in _voxelButtons) btn.Draw();

        new Billboard()
            .Position(new Vector3(15, 8, -40)).Scale(new Vector2(8.0f, 5.0f));

        if (Console.Instance!.IsOpen) {
            Scene.btn1?.Draw();
            _textRenderer!.DrawString("Hello!", 200, 200, 20, new Vector4(1, 1, 1, 1));
        }
    }

    public override void Unload() {
        base.Unload();

        foreach (var obj in _objects) obj.Dispose();
        foreach (var btn in _voxelButtons) btn.Dispose();
        EventBusUi.Unsubscribe<ButtonPressedEvent>(OnBtnPress);
        EventBusUi.Unsubscribe<ButtonHeldEvent>(OnBtnHeld);
        EventBusUi.Unsubscribe<ButtonRelesedEvent>(OnBtnReleased);
        Billboard.Dispose();
        _textRenderer?.Dispose();
        skybox?.Dispose();
        Scene.btn1?.Dispose();
    }
}