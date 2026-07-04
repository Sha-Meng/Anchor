using System.Collections.Generic;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RivetRopeLineVisual : MonoBehaviour
    {
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float slackSagPerMeter = 0.04f;
        [SerializeField] private float maxSag = 0.6f;
        [SerializeField] private int segmentSubdivisions = 5;
        [SerializeField] private bool enableDynamics = true;
        [SerializeField] private float springSmoothTime = 0.08f;
        [SerializeField] private float physicsGravity = 5.5f;
        [SerializeField] private float physicsDamping = 0.92f;
        [SerializeField] private float targetFollow = 7f;
        [SerializeField] private int constraintIterations = 4;
        [SerializeField] private float swayAmplitude = 0.04f;
        [SerializeField] private float swaySpeed = 2.2f;
        [SerializeField] private Color slackColor = Color.green;
        [SerializeField] private Color tautColor = Color.red;

        private readonly List<Vector3> _renderPoints = new List<Vector3>();
        private Vector3[] _simulatedPoints;
        private Vector3[] _velocities;

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

            BuildRenderPoints(path, _renderPoints);
            ApplyDynamics(_renderPoints);
            lineRenderer.positionCount = _renderPoints.Count;
            lineRenderer.SetPositions(_renderPoints.ToArray());
            var color = path.IsTaut ? tautColor : slackColor;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        private void BuildRenderPoints(RopePathResult path, List<Vector3> result)
        {
            var source = path.Points;
            result.Clear();
            var sag = path.IsTaut ? 0f : Mathf.Min(maxSag, path.RemainingSlack * slackSagPerMeter);
            var subdivisions = Mathf.Max(1, segmentSubdivisions);

            result.Add(source[0]);
            for (int i = 1; i < source.Length; i++)
            {
                var from = source[i - 1];
                var to = source[i];
                for (int step = 1; step <= subdivisions; step++)
                {
                    var t = step / (float)subdivisions;
                    var point = Vector3.Lerp(from, to, t);
                    if (sag > 0f)
                    {
                        var direction = (to - from).normalized;
                        var sagDirection = new Vector3(-direction.y, direction.x, 0f);
                        if (sagDirection.sqrMagnitude < 0.0001f)
                        {
                            sagDirection = Vector3.down;
                        }

                        var arc = Mathf.Sin(t * Mathf.PI) * sag;
                        point += sagDirection.normalized * (arc * 0.55f);
                        point.y -= arc * 0.35f;
                    }

                    result.Add(point);
                }
            }
        }

        private void ApplyDynamics(List<Vector3> targets)
        {
            if (!enableDynamics || targets.Count == 0)
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
            var time = Time.time * swaySpeed;
            _simulatedPoints[0] = targets[0];
            _simulatedPoints[targets.Count - 1] = targets[targets.Count - 1];
            _velocities[0] = Vector3.zero;
            _velocities[targets.Count - 1] = Vector3.zero;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (i > 0 && i < targets.Count - 1 && swayAmplitude > 0f)
                {
                    target.x += Mathf.Sin(time + i * 0.73f) * swayAmplitude;
                }

                if (i == 0 || i == targets.Count - 1)
                {
                    targets[i] = _simulatedPoints[i];
                    continue;
                }

                var toTarget = (target - _simulatedPoints[i]) * Mathf.Max(0f, targetFollow);
                _velocities[i] += (Vector3.down * physicsGravity + toTarget) * deltaTime;
                _velocities[i] *= Mathf.Clamp01(physicsDamping);
                _simulatedPoints[i] += _velocities[i] * deltaTime;
            }

            var iterations = Mathf.Max(1, constraintIterations);
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

            var smoothTime = Mathf.Max(0.01f, springSmoothTime);
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i] = Vector3.Lerp(targets[i], _simulatedPoints[i], 1f - Mathf.Exp(-deltaTime / smoothTime));
            }
        }
    }
}
