using ClimbGame.Art;
using ClimbGame.Character;
using ClimbGame.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClimbGame.Boot
{
    /// <summary>
    /// Composition root for the climbing demo. Drop this on a single empty GameObject
    /// (or use the "Tools/ClimbGame/Create &amp; Open Demo Scene" menu) and press Play.
    ///
    /// It is the one place allowed to know about every subsystem: it builds the camera,
    /// backdrop, player and on-screen joystick, then wires them together. Everything it
    /// assembles stays independently testable and reusable.
    /// </summary>
    [AddComponentMenu("ClimbGame/Climb Game Bootstrap")]
    public sealed class ClimbGameBootstrap : MonoBehaviour
    {
        [Header("Climb area (world units)")]
        [SerializeField] private Vector2 areaSize = new Vector2(7f, 8f);

        [Header("Character")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private int pixelsPerUnit = 32;
        [SerializeField] private int climbFrameCount = 8;

        [Header("Scene setup")]
        [SerializeField] private bool createCamera = true;
        [SerializeField] private bool createWall = true;
        [SerializeField] private bool createJoystick = true;
        [SerializeField] private Color backgroundColor = new Color(0.11f, 0.13f, 0.18f, 1f);
        [SerializeField] private Color joystickAccent = new Color(0.35f, 0.75f, 1f, 1f);

        private void Awake()
        {
            Rect climbArea = new Rect(-areaSize.x * 0.5f, -areaSize.y * 0.5f, areaSize.x, areaSize.y);

            if (createCamera) EnsureCamera();
            if (createWall) BuildWall();

            ClimberController controller = BuildPlayer(climbArea);
            CompositeClimbInput input = BuildInputSources(controller.gameObject);

            controller.SetInput(input);
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = areaSize.y * 0.5f + 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildWall()
        {
            var go = new GameObject("ClimbWall");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = WallTextureFactory.Create(96, 128, pixelsPerUnit);
            renderer.sortingOrder = -10;

            // Scale the wall sprite to cover the whole climb area with a small margin.
            Vector2 spriteWorld = renderer.sprite.bounds.size;
            float sx = (areaSize.x + 2f) / spriteWorld.x;
            float sy = (areaSize.y + 2f) / spriteWorld.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
        }

        private ClimberController BuildPlayer(Rect climbArea)
        {
            var go = new GameObject("Climber");

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            var animator = go.AddComponent<ClimberAnimator>();
            ClimberSpriteFactory.ClimberSprites sprites =
                ClimberSpriteFactory.Create(climbFrameCount, pixelsPerUnit);
            animator.SetSprites(sprites.ClimbFrames, sprites.Idle);

            var controller = go.AddComponent<ClimberController>();
            controller.SetClimbArea(climbArea);
            controller.SetMoveSpeed(moveSpeed);

            animator.Bind(controller);
            return controller;
        }

        private CompositeClimbInput BuildInputSources(GameObject playerGo)
        {
            var composite = playerGo.AddComponent<CompositeClimbInput>();

            // Keyboard (WASD / arrows) is always available for Editor debugging.
            composite.AddSource(playerGo.AddComponent<KeyboardClimbInput>());

            if (createJoystick)
            {
                VirtualJoystick joystick = BuildJoystickUI();
                if (joystick != null) composite.AddSource(joystick);
            }

            return composite;
        }

        private VirtualJoystick BuildJoystickUI()
        {
            EnsureEventSystem();

            // Canvas.
            var canvasGo = new GameObject("ClimbCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            const float bgSize = 220f;
            const float handleSize = 96f;
            float travelRadius = (bgSize - handleSize) * 0.5f;

            // Joystick background, anchored bottom-left.
            var bgGo = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.SetParent(canvasGo.transform, false);
            bgRect.anchorMin = bgRect.anchorMax = Vector2.zero;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(200f, 200f);
            bgRect.sizeDelta = new Vector2(bgSize, bgSize);
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.sprite = MakeRingSprite(128, new Color32(255, 255, 255, 40), new Color32(255, 255, 255, 170));
            bgImage.raycastTarget = true;

            // Handle.
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.SetParent(bgRect, false);
            handleRect.sizeDelta = new Vector2(handleSize, handleSize);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.sprite = MakeDiscSprite(64, (Color32)joystickAccent, new Color32(20, 24, 32, 255));
            handleImage.raycastTarget = false;

            var joystick = bgGo.GetComponent<VirtualJoystick>();
            joystick.Configure(bgRect, handleRect, travelRadius);
            return joystick;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.name = "EventSystem";
        }

        // --- UI sprite helpers (procedural, so the demo ships no image assets) ---

        private static Sprite MakeDiscSprite(int size, Color32 fill, Color32 outline)
        {
            var buffer = TextureUtil.NewCanvas(size, size);
            float c = size * 0.5f;
            TextureUtil.DrawDisc(buffer, size, size, c, c, c - 1f, outline);
            TextureUtil.DrawDisc(buffer, size, size, c, c, c - 4f, fill);
            return TextureUtil.CreateSprite(buffer, size, size, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeRingSprite(int size, Color32 fill, Color32 ring)
        {
            var buffer = TextureUtil.NewCanvas(size, size);
            float c = size * 0.5f;
            TextureUtil.DrawDisc(buffer, size, size, c, c, c - 1f, ring);
            TextureUtil.DrawDisc(buffer, size, size, c, c, c - 5f, fill);
            return TextureUtil.CreateSprite(buffer, size, size, size, new Vector2(0.5f, 0.5f));
        }
    }
}
