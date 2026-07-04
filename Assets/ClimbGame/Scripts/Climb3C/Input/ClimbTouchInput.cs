using System.Collections.Generic;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Input
{
    public enum ClimbPointerPhase { Began, Held, Ended }

    /// <summary>一个被采样的触点/指针。</summary>
    public struct ClimbPointer
    {
        public int Id;
        public Vector2 ScreenPos;
        public ClimbPointerPhase Phase;
    }

    /// <summary>
    /// 屏幕左右输入区与触点采样。支持多点触屏；编辑器/PC 用鼠标回退（id = -1）。
    /// 只负责"哪里有触点、在左区还是右区"，不知道当前攀爬手是谁。
    /// </summary>
    public sealed class ClimbTouchInput : MonoBehaviour
    {
        [SerializeField] private ClimbTuningConfig tuning;

        private readonly List<ClimbPointer> _pointers = new List<ClimbPointer>();
        private int _sampledFrame = -1;

        public IReadOnlyList<ClimbPointer> Pointers
        {
            get { EnsureSampled(); return _pointers; }
        }

        public void SetTuning(ClimbTuningConfig config) => tuning = config;

        /// <summary>
        /// 按帧惰性采样：无论本组件与控制器的 Update 谁先执行，查询时都会先采样当前帧输入。
        /// 这样鼠标/触摸的按下（仅一帧的 Began）不会因执行顺序被错过。
        /// </summary>
        private void EnsureSampled()
        {
            if (_sampledFrame == Time.frameCount) return;
            _sampledFrame = Time.frameCount;
            Sample();
        }

        private void Sample()
        {
            _pointers.Clear();
            if (tuning == null) return;

            if (UnityEngine.Input.touchSupported && UnityEngine.Input.touchCount > 0)
            {
                for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    Touch t = UnityEngine.Input.GetTouch(i);
                    _pointers.Add(new ClimbPointer
                    {
                        Id = t.fingerId,
                        ScreenPos = t.position,
                        Phase = PhaseFromTouch(t.phase)
                    });
                }
            }
            else
            {
                // 鼠标回退（编辑器/PC）
                bool down = UnityEngine.Input.GetMouseButton(0);
                bool began = UnityEngine.Input.GetMouseButtonDown(0);
                bool ended = UnityEngine.Input.GetMouseButtonUp(0);
                if (down || began || ended)
                {
                    ClimbPointerPhase phase = began ? ClimbPointerPhase.Began
                        : ended ? ClimbPointerPhase.Ended : ClimbPointerPhase.Held;
                    _pointers.Add(new ClimbPointer
                    {
                        Id = -1,
                        ScreenPos = UnityEngine.Input.mousePosition,
                        Phase = phase
                    });
                }
            }
        }

        private static ClimbPointerPhase PhaseFromTouch(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return ClimbPointerPhase.Began;
                case TouchPhase.Ended:
                case TouchPhase.Canceled: return ClimbPointerPhase.Ended;
                default: return ClimbPointerPhase.Held;
            }
        }

        /// <summary>屏幕点落在哪个输入区（考虑上下裁剪与左右外边距）；不在任何区返回 None。</summary>
        public ClimbHand ZoneOf(Vector2 screenPos)
        {
            if (tuning == null) return ClimbHand.None;
            float w = Screen.width;
            float h = Screen.height;
            if (w <= 0f || h <= 0f) return ClimbHand.None;

            float nx = screenPos.x / w;
            float ny = screenPos.y / h;

            if (ny < tuning.zoneBottomInset || ny > 1f - tuning.zoneTopInset) return ClimbHand.None;
            if (nx < tuning.zoneHorizontalInset || nx > 1f - tuning.zoneHorizontalInset) return ClimbHand.None;

            return ClimbHandExtensions.SideFromScreenX(nx, tuning.zoneSplit);
        }

        /// <summary>查找某侧输入区内、id 匹配的触点；找到返回 true。</summary>
        public bool TryGetPointerById(int id, out ClimbPointer pointer)
        {
            EnsureSampled();
            for (int i = 0; i < _pointers.Count; i++)
            {
                if (_pointers[i].Id == id)
                {
                    pointer = _pointers[i];
                    return true;
                }
            }
            pointer = default;
            return false;
        }

        /// <summary>查找某侧输入区内刚按下（Began）的触点，用于开始一次攀爬或决定首手。</summary>
        public bool TryGetNewPress(ClimbHand side, out ClimbPointer pointer)
        {
            EnsureSampled();
            for (int i = 0; i < _pointers.Count; i++)
            {
                ClimbPointer p = _pointers[i];
                if (p.Phase != ClimbPointerPhase.Began) continue;
                if (ZoneOf(p.ScreenPos) != side) continue;
                pointer = p;
                return true;
            }
            pointer = default;
            return false;
        }

        /// <summary>查找任意位置刚按下（Began）的触点，不分左右区（用于"就近选手"起攀）。</summary>
        public bool TryGetAnyNewPress(out ClimbPointer pointer)
        {
            EnsureSampled();
            for (int i = 0; i < _pointers.Count; i++)
            {
                if (_pointers[i].Phase != ClimbPointerPhase.Began) continue;
                pointer = _pointers[i];
                return true;
            }
            pointer = default;
            return false;
        }

        /// <summary>查找任意一侧刚按下的触点（用于起攀首手判定）。</summary>
        public bool TryGetAnyNewPress(out ClimbPointer pointer, out ClimbHand side)
        {
            EnsureSampled();
            for (int i = 0; i < _pointers.Count; i++)
            {
                ClimbPointer p = _pointers[i];
                if (p.Phase != ClimbPointerPhase.Began) continue;
                ClimbHand s = ZoneOf(p.ScreenPos);
                if (s == ClimbHand.None) continue;
                pointer = p;
                side = s;
                return true;
            }
            pointer = default;
            side = ClimbHand.None;
            return false;
        }
    }
}
