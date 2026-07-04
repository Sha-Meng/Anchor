using System.Collections.Generic;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RivetRopeLineVisual : MonoBehaviour
    {
        [SerializeField] private RivetRopeConfig config;
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private bool preferConfigSettings = true;
        [SerializeField] private RivetRopeVisualSettings fallbackSettings = RivetRopeVisualSettings.CreateDefault();

        private readonly List<Vector3> _renderPoints = new List<Vector3>();
        private Vector3[] _simulatedPoints;
        private Vector3[] _previousPoints;
        private Vector3[] _velocities;
        private Vector3[] _positionBuffer;
        private bool _hasRuntimeOverride;
        private RivetRopeVisualSettings _runtimeSettings;

        public int RenderPointCount => _renderPoints.Count;
        public float LastRenderedLength { get; private set; }
        public RivetRopeVisualMode CurrentMode => ResolveSettings().VisualMode;

        public void SetRuntimeVisualSettings(RivetRopeVisualSettings settings)
        {
            _runtimeSettings = settings.Sanitized();
            _hasRuntimeOverride = true;
        }

        public void ClearRuntimeVisualSettings()
        {
            _hasRuntimeOverride = false;
        }

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
        }

        private void LateUpdate()
        {
            if (driver == null || lineRenderer == null)
            {
                return;
            }

            var path = driver.LastPath;
            var points = path.Points;
            if (points == null || points.Length < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            var settings = ResolveSettings();
            BuildRenderPoints(path, settings, _renderPoints);
            if (settings.VisualMode == RivetRopeVisualMode.VerletSegments)
            {
                ApplyVerletDynamics(_renderPoints, settings);
            }
            else
            {
                ApplyProceduralDynamics(_renderPoints, settings);
            }

            LastRenderedLength = CalculateLength(_renderPoints);
            lineRenderer.positionCount = _renderPoints.Count;
            EnsurePositionBuffer(_renderPoints.Count);
            for (int i = 0; i < _renderPoints.Count; i++)
            {
                _positionBuffer[i] = _renderPoints[i];
            }

            lineRenderer.SetPositions(_positionBuffer);
            ApplyLineSettings(path, settings);
        }

        private RivetRopeVisualSettings ResolveSettings()
        {
            if (_hasRuntimeOverride)
            {
                return _runtimeSettings.Sanitized();
            }

            if (preferConfigSettings && config != null)
            {
                return config.VisualSettings;
            }

            return fallbackSettings.Sanitized();
        }

        private void ApplyLineSettings(RopePathResult path, RivetRopeVisualSettings settings)
        {
            if (settings.RopeMaterial != null && lineRenderer.sharedMaterial != settings.RopeMaterial)
            {
                lineRenderer.sharedMaterial = settings.RopeMaterial;
            }
            else if (lineRenderer.sharedMaterial == null)
            {
                lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.sortingOrder = settings.SortingOrder;
            lineRenderer.widthMultiplier = settings.Width * (path.IsTaut ? settings.TautWidthMultiplier : 1f);

            var color = path.IsTaut ? settings.TautColor : settings.SlackColor;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            var material = lineRenderer.material;
            if (material != null)
            {
                material.renderQueue = settings.RenderQueue;
                if (material.HasProperty("_MainTex") && settings.TextureTilesPerMeter > 0f)
                {
                    material.mainTextureScale = new Vector2(Mathf.Max(1f, LastRenderedLength * settings.TextureTilesPerMeter), 1f);
                }
            }
        }

        private void BuildRenderPoints(RopePathResult path, RivetRopeVisualSettings settings, List<Vector3> result)
        {
            var source = path.Points;
            result.Clear();
            var sag = path.IsTaut ? 0f : Mathf.Min(settings.MaxSag, path.RemainingSlack * settings.SlackSagPerMeter);
            var subdivisions = Mathf.Max(1, settings.SegmentSubdivisions);

            result.Add(source[0]);
            for (int i = 1; i < source.Length; i++)
            {
                var from = source[i - 1];
                var to = source[i];
                var segmentSag = source.Length > 2 && i > 1 ? sag * 0.85f : sag;
                for (int step = 1; step <= subdivisions; step++)
                {
                    var t = step / (float)subdivisions;
                    var point = Vector3.Lerp(from, to, t);
                    if (segmentSag > 0f)
                    {
                        var direction = (to - from).normalized;
                        var sagDirection = new Vector3(-direction.y, direction.x, 0f);
                        if (sagDirection.sqrMagnitude < 0.0001f)
                        {
                            sagDirection = Vector3.down;
                        }

                        var arc = Mathf.Sin(t * Mathf.PI) * segmentSag;
                        point += sagDirection.normalized * (arc * 0.55f);
                        point.y -= arc * 0.35f;
                    }

                    result.Add(point);
                }
            }
        }

        private void ApplyProceduralDynamics(List<Vector3> targets, RivetRopeVisualSettings settings)
        {
            if (!settings.EnableDynamics || targets.Count == 0)
            {
                return;
            }

            if (_simulatedPoints == null || _simulatedPoints.Length != targets.Count)
            {
                _simulatedPoints = targets.ToArray();
                _velocities = new Vector3[targets.Count];
                return;
            }

            var deltaTime = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            var time = Time.time * settings.SwaySpeed;
            _simulatedPoints[0] = targets[0];
            _simulatedPoints[targets.Count - 1] = targets[targets.Count - 1];
            _velocities[0] = Vector3.zero;
            _velocities[targets.Count - 1] = Vector3.zero;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (i > 0 && i < targets.Count - 1 && settings.SwayAmplitude > 0f)
                {
                    target.x += Mathf.Sin(time + i * 0.73f) * settings.SwayAmplitude;
                }

                if (i == 0 || i == targets.Count - 1)
                {
                    targets[i] = _simulatedPoints[i];
                    continue;
                }

                var toTarget = (target - _simulatedPoints[i]) * Mathf.Max(0f, settings.TargetFollow);
                _velocities[i] += (Vector3.down * settings.PhysicsGravity + toTarget) * deltaTime;
                _velocities[i] *= Mathf.Clamp01(settings.PhysicsDamping);
                _simulatedPoints[i] += _velocities[i] * deltaTime;
            }

            var iterations = Mathf.Max(1, settings.ConstraintIterations);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                _simulatedPoints[0] = targets[0];
                _simulatedPoints[targets.Count - 1] = targets[targets.Count - 1];
                for (int i = 1; i < targets.Count; i++)
                {
                    var expectedLength = Vector3.Distance(targets[i - 1], targets[i]);
                    if (expectedLength <= 0.0001f)
                    {
                        continue;
                    }

                    var from = _simulatedPoints[i - 1];
                    var to = _simulatedPoints[i];
                    var delta = to - from;
                    var currentLength = delta.magnitude;
                    if (currentLength <= 0.0001f)
                    {
                        continue;
                    }

                    var correction = delta * ((currentLength - expectedLength) / currentLength);
                    if (i == 1)
                    {
                        _simulatedPoints[i] -= correction;
                    }
                    else if (i == targets.Count - 1)
                    {
                        _simulatedPoints[i - 1] += correction;
                    }
                    else
                    {
                        _simulatedPoints[i - 1] += correction * 0.5f;
                        _simulatedPoints[i] -= correction * 0.5f;
                    }
                }
            }

            var smoothTime = Mathf.Max(0.01f, settings.SpringSmoothTime);
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i] = Vector3.Lerp(targets[i], _simulatedPoints[i], 1f - Mathf.Exp(-deltaTime / smoothTime));
            }
        }

        private void ApplyVerletDynamics(List<Vector3> targets, RivetRopeVisualSettings settings)
        {
            if (!settings.EnableDynamics || targets.Count == 0)
            {
                return;
            }

            if (_simulatedPoints == null || _simulatedPoints.Length != targets.Count || _previousPoints == null)
            {
                _simulatedPoints = targets.ToArray();
                _previousPoints = targets.ToArray();
                _velocities = new Vector3[targets.Count];
                return;
            }

            var deltaTime = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            var deltaTimeSquared = deltaTime * deltaTime;
            for (int i = 0; i < targets.Count; i++)
            {
                if (IsPinnedRenderPoint(i, targets.Count, settings.SegmentSubdivisions))
                {
                    _simulatedPoints[i] = targets[i];
                    _previousPoints[i] = targets[i];
                    continue;
                }

                var current = _simulatedPoints[i];
                var velocity = (current - _previousPoints[i]) * settings.PhysicsDamping;
                var follow = (targets[i] - current) * settings.TargetFollow * deltaTimeSquared;
                _previousPoints[i] = current;
                _simulatedPoints[i] = current + velocity + Vector3.down * settings.PhysicsGravity * deltaTimeSquared + follow;
            }

            var iterations = Mathf.Max(1, settings.ConstraintIterations);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (IsPinnedRenderPoint(i, targets.Count, settings.SegmentSubdivisions))
                    {
                        _simulatedPoints[i] = targets[i];
                    }
                }

                for (int i = 1; i < targets.Count; i++)
                {
                    var expectedLength = Vector3.Distance(targets[i - 1], targets[i]);
                    if (expectedLength <= 0.0001f)
                    {
                        continue;
                    }

                    var from = _simulatedPoints[i - 1];
                    var to = _simulatedPoints[i];
                    var delta = to - from;
                    var currentLength = delta.magnitude;
                    if (currentLength <= 0.0001f)
                    {
                        continue;
                    }

                    var correction = delta * ((currentLength - expectedLength) / currentLength);
                    var fromPinned = IsPinnedRenderPoint(i - 1, targets.Count, settings.SegmentSubdivisions);
                    var toPinned = IsPinnedRenderPoint(i, targets.Count, settings.SegmentSubdivisions);
                    if (fromPinned && !toPinned)
                    {
                        _simulatedPoints[i] -= correction;
                    }
                    else if (!fromPinned && toPinned)
                    {
                        _simulatedPoints[i - 1] += correction;
                    }
                    else if (!fromPinned && !toPinned)
                    {
                        _simulatedPoints[i - 1] += correction * 0.5f;
                        _simulatedPoints[i] -= correction * 0.5f;
                    }
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i] = _simulatedPoints[i];
            }
        }

        private static bool IsPinnedRenderPoint(int index, int count, int subdivisions)
        {
            if (index == 0 || index == count - 1)
            {
                return true;
            }

            return Mathf.Max(1, subdivisions) > 0 && index % Mathf.Max(1, subdivisions) == 0;
        }

        private static float CalculateLength(List<Vector3> points)
        {
            var length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        private void EnsurePositionBuffer(int count)
        {
            if (_positionBuffer == null || _positionBuffer.Length != count)
            {
                _positionBuffer = new Vector3[count];
            }
        }
    }
}
