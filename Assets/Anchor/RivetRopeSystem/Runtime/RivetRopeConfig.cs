using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public enum RivetRopeVisualMode
    {
        ProceduralLine = 0,
        VerletSegments = 1
    }

    [System.Serializable]
    public struct RivetRopeVisualSettings
    {
        public RivetRopeVisualMode VisualMode;
        public Material RopeMaterial;
        [Min(0.001f)] public float Width;
        [Min(0f)] public float TextureTilesPerMeter;
        [Min(0f)] public float SlackSagPerMeter;
        [Min(0f)] public float MaxSag;
        [Min(1)] public int SegmentSubdivisions;
        public bool EnableDynamics;
        [Min(0.001f)] public float SpringSmoothTime;
        [Min(0f)] public float PhysicsGravity;
        [Range(0f, 1f)] public float PhysicsDamping;
        [Min(0f)] public float TargetFollow;
        [Min(1)] public int ConstraintIterations;
        [Min(0f)] public float SwayAmplitude;
        [Min(0f)] public float SwaySpeed;
        public Color SlackColor;
        public Color TautColor;
        [Min(0.1f)] public float TautWidthMultiplier;
        public int SortingOrder;
        [Range(1000, 5000)] public int RenderQueue;

        public static RivetRopeVisualSettings CreateDefault()
        {
            return new RivetRopeVisualSettings
            {
                VisualMode = RivetRopeVisualMode.ProceduralLine,
                RopeMaterial = null,
                Width = 0.055f,
                TextureTilesPerMeter = 1.35f,
                SlackSagPerMeter = 0.09f,
                MaxSag = 1.1f,
                SegmentSubdivisions = 12,
                EnableDynamics = true,
                SpringSmoothTime = 0.16f,
                PhysicsGravity = 7.5f,
                PhysicsDamping = 0.94f,
                TargetFollow = 6.5f,
                ConstraintIterations = 6,
                SwayAmplitude = 0.075f,
                SwaySpeed = 1.8f,
                SlackColor = new Color(0.86f, 0.78f, 0.58f),
                TautColor = new Color(1f, 0.52f, 0.34f),
                TautWidthMultiplier = 0.86f,
                SortingOrder = -20,
                RenderQueue = 2000
            };
        }

        public RivetRopeVisualSettings Sanitized()
        {
            return new RivetRopeVisualSettings
            {
                VisualMode = VisualMode,
                RopeMaterial = RopeMaterial,
                Width = Mathf.Max(0.001f, Width),
                TextureTilesPerMeter = Mathf.Max(0f, TextureTilesPerMeter),
                SlackSagPerMeter = Mathf.Max(0f, SlackSagPerMeter),
                MaxSag = Mathf.Max(0f, MaxSag),
                SegmentSubdivisions = Mathf.Max(1, SegmentSubdivisions),
                EnableDynamics = EnableDynamics,
                SpringSmoothTime = Mathf.Max(0.001f, SpringSmoothTime),
                PhysicsGravity = Mathf.Max(0f, PhysicsGravity),
                PhysicsDamping = Mathf.Clamp01(PhysicsDamping),
                TargetFollow = Mathf.Max(0f, TargetFollow),
                ConstraintIterations = Mathf.Max(1, ConstraintIterations),
                SwayAmplitude = Mathf.Max(0f, SwayAmplitude),
                SwaySpeed = Mathf.Max(0f, SwaySpeed),
                SlackColor = SlackColor,
                TautColor = TautColor,
                TautWidthMultiplier = Mathf.Max(0.1f, TautWidthMultiplier),
                SortingOrder = SortingOrder,
                RenderQueue = Mathf.Clamp(RenderQueue <= 0 ? 2000 : RenderQueue, 1000, 5000)
            };
        }
    }

    [CreateAssetMenu(menuName = "Anchor/Rivet Rope Config", fileName = "RivetRopeConfig")]
    public sealed class RivetRopeConfig : ScriptableObject
    {
        [SerializeField] private RivetRopeSettings settings = RivetRopeSettings.CreateDefault();
        [SerializeField] private RivetRopeVisualSettings visualSettings = RivetRopeVisualSettings.CreateDefault();

        public RivetRopeSettings Settings => settings.Sanitized();
        public RivetRopeVisualSettings VisualSettings => visualSettings.Sanitized();

        public void ConfigureRuntime(RivetRopeSettings runtimeSettings)
        {
            settings = runtimeSettings.Sanitized();
        }

        public void ConfigureRuntimeVisuals(RivetRopeVisualSettings runtimeVisualSettings)
        {
            visualSettings = runtimeVisualSettings.Sanitized();
        }

        private void OnValidate()
        {
            settings = settings.Sanitized();
            visualSettings = visualSettings.Sanitized();
        }
    }
}
