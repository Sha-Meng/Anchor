using System.Collections.Generic;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeRivetVisual : MonoBehaviour
    {
        [SerializeField] private RivetRopeConfig config;
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float headRadius = 0.13f;
        [SerializeField] private float shaftRadius = 0.055f;
        [SerializeField] private float shaftLength = 0.28f;
        [SerializeField] private float ropeRingRadius = 0.2f;
        [SerializeField] private float ropeRingWidth = 0.018f;
        [SerializeField] private float tautScale = 1.12f;
        [SerializeField] private Color slackColor = new Color(0.95f, 0.76f, 0.32f);
        [SerializeField] private Color tautColor = new Color(1f, 0.42f, 0.22f);
        [SerializeField] private Color shaftColor = new Color(0.28f, 0.28f, 0.3f);

        private readonly Dictionary<string, RivetView> _viewsByRivetId = new Dictionary<string, RivetView>();
        private Material _rivetMaterial;
        private Material _shaftMaterial;
        private Material _ringMaterial;
        private int _lastRevision = -1;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (visualRoot == null)
            {
                var root = new GameObject("Rivet Rope Rivet Visuals");
                root.transform.SetParent(transform, false);
                visualRoot = root.transform;
            }

            _rivetMaterial = new Material(Shader.Find("Standard"));
            _shaftMaterial = new Material(Shader.Find("Standard"));
            _ringMaterial = new Material(Shader.Find("Sprites/Default"));
            _shaftMaterial.color = shaftColor;
        }

        private void LateUpdate()
        {
            if (driver == null)
            {
                ClearViews();
                return;
            }

            if (_lastRevision != driver.Model.RopeRevision)
            {
                SyncViews();
                _lastRevision = driver.Model.RopeRevision;
            }

            UpdateViews();
        }

        private void OnDestroy()
        {
            ClearViews();
            DestroyMaterial(_rivetMaterial);
            DestroyMaterial(_shaftMaterial);
            DestroyMaterial(_ringMaterial);
        }

        private void SyncViews()
        {
            var activeIds = new HashSet<string>();
            var rivets = driver.Model.PlacedRivets;
            for (int i = 0; i < rivets.Count; i++)
            {
                var rivetId = rivets[i].RivetId;
                activeIds.Add(rivetId);
                if (!_viewsByRivetId.ContainsKey(rivetId))
                {
                    _viewsByRivetId.Add(rivetId, CreateView(rivetId));
                }
            }

            var remove = new List<string>();
            foreach (var kvp in _viewsByRivetId)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    DestroyView(kvp.Value);
                    remove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _viewsByRivetId.Remove(remove[i]);
            }
        }

        private RivetView CreateView(string rivetId)
        {
            var root = new GameObject("Rivet Visual " + rivetId);
            root.transform.SetParent(visualRoot != null ? visualRoot : transform, false);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            RemoveCollider(shaft);
            var shaftRenderer = shaft.GetComponent<Renderer>();
            if (shaftRenderer != null)
            {
                shaftRenderer.sharedMaterial = _shaftMaterial;
            }

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            RemoveCollider(head);
            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.sharedMaterial = _rivetMaterial;
            }

            var ring = new GameObject("Rope Link Ring");
            ring.transform.SetParent(root.transform, false);
            var ringRenderer = ring.AddComponent<LineRenderer>();
            ringRenderer.useWorldSpace = false;
            ringRenderer.loop = true;
            ringRenderer.positionCount = 24;
            ringRenderer.numCapVertices = 2;
            ringRenderer.numCornerVertices = 2;
            ringRenderer.sharedMaterial = _ringMaterial;

            return new RivetView
            {
                Root = root.transform,
                Shaft = shaft.transform,
                Head = head.transform,
                Ring = ringRenderer
            };
        }

        private void UpdateViews()
        {
            var settings = config != null ? config.VisualSettings : RivetRopeVisualSettings.CreateDefault();
            var isTaut = driver.LastPath.IsTaut;
            var color = isTaut ? ResolveTautColor(settings) : ResolveSlackColor(settings);
            var scale = isTaut ? tautScale : 1f;
            var surfaceNormal = ResolveSurfaceNormal();

            if (_rivetMaterial != null)
            {
                _rivetMaterial.color = color;
            }

            if (_ringMaterial != null)
            {
                _ringMaterial.color = color;
            }

            var rivets = driver.Model.PlacedRivets;
            for (int i = 0; i < rivets.Count; i++)
            {
                if (!_viewsByRivetId.TryGetValue(rivets[i].RivetId, out var view))
                {
                    continue;
                }

                UpdateView(view, rivets[i].Position, surfaceNormal, color, scale);
            }
        }

        private void UpdateView(RivetView view, Vector3 position, Vector3 surfaceNormal, Color ropeColor, float scale)
        {
            if (view.Root == null)
            {
                return;
            }

            view.Root.position = position;
            view.Root.rotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);
            view.Root.localScale = Vector3.one * scale;

            if (view.Head != null)
            {
                view.Head.localPosition = Vector3.zero;
                view.Head.localRotation = Quaternion.identity;
                view.Head.localScale = Vector3.one * Mathf.Max(0.01f, headRadius);
            }

            if (view.Shaft != null)
            {
                view.Shaft.localPosition = new Vector3(0f, 0f, shaftLength * 0.5f);
                view.Shaft.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                view.Shaft.localScale = new Vector3(
                    Mathf.Max(0.01f, shaftRadius),
                    Mathf.Max(0.01f, shaftLength) * 0.5f,
                    Mathf.Max(0.01f, shaftRadius));
            }

            if (view.Ring != null)
            {
                view.Ring.startColor = ropeColor;
                view.Ring.endColor = ropeColor;
                view.Ring.widthMultiplier = Mathf.Max(0.001f, ropeRingWidth);
                WriteRingPoints(view.Ring, Mathf.Max(headRadius, ropeRingRadius));
            }
        }

        private Color ResolveSlackColor(RivetRopeVisualSettings settings)
        {
            return settings.SlackColor.a > 0f ? settings.SlackColor : slackColor;
        }

        private Color ResolveTautColor(RivetRopeVisualSettings settings)
        {
            return settings.TautColor.a > 0f ? settings.TautColor : tautColor;
        }

        private Vector3 ResolveSurfaceNormal()
        {
            if (targetCamera != null)
            {
                return -targetCamera.transform.forward.normalized;
            }

            return Vector3.back;
        }

        private static void WriteRingPoints(LineRenderer ring, float radius)
        {
            var count = Mathf.Max(8, ring.positionCount);
            for (int i = 0; i < count; i++)
            {
                var radians = i / (float)count * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, -0.012f));
            }
        }

        private void ClearViews()
        {
            foreach (var view in _viewsByRivetId.Values)
            {
                DestroyView(view);
            }

            _viewsByRivetId.Clear();
        }

        private static void DestroyView(RivetView view)
        {
            if (view.Root != null)
            {
                Destroy(view.Root.gameObject);
            }
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private struct RivetView
        {
            public Transform Root;
            public Transform Shaft;
            public Transform Head;
            public LineRenderer Ring;
        }
    }
}
