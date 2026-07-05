using System;
using Anchor.ForceSystem;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Gameplay;
using UnityEngine;

namespace ClimbGame.Climb3C.State
{
    /// <summary>
    /// 单个攀爬者的运行时数据（与视觉/物理无关）。作为联机同步的最小快照单元：
    /// 需要同步的字段用普通字段（可序列化），仅本地使用的瞬态字段标记 [NonSerialized]。
    /// </summary>
    [Serializable]
    public sealed class ClimberRuntimeState
    {
        [Header("身份")]
        public int PlayerId;
        public bool IsLocal;

        [Header("攀爬状态（需同步）")]
        public ClimbState State = ClimbState.WaitingForPress;
        public ClimbHand CurrentHand = ClimbHand.None;

        [Header("姿态（需同步）")]
        public Vector3 TorsoCenter;
        public Vector3 LeftAnchor;
        public Vector3 RightAnchor;
        public Vector3 AttackHandCurrent;

        [Header("抓点引用（按 RivetField 索引，供同步；-1 表示手在默认位）")]
        public int LeftRivetId = -1;
        public int RightRivetId = -1;

        [Header("耐力（需同步）")]
        public float Stamina;
        public float MaxStamina = 100f;

        [Header("生命（需同步）")]
        public float Health = 100f;
        public float MaxHealth = 100f;
        public bool IsFailed;
        public float LastDamage;
        public string LastDamageReason = string.Empty;

        // --- 仅本地使用的瞬态数据，不参与联机同步 ---
        [NonSerialized] public int TrackedFinger;
        [NonSerialized] public float FallStartY;
        [NonSerialized] public float FallTimer;

        // 本次伸手开始时手的真实世界位（用于相对位移映射，按下瞬间手不跳变）
        [NonSerialized] public Vector3 ReachStartHand;
        [NonSerialized] public RivetPoint LeftRivet;
        [NonSerialized] public RivetPoint RightRivet;

        /// <summary>SystemValidation 力学判定（坠落）的记忆状态，供 ForceEvaluator 逐帧演算。</summary>
        [NonSerialized] public ForceEvaluationMemory ForceMemory;

        public float StaminaRatio => MaxStamina > 0f ? Mathf.Clamp01(Stamina / MaxStamina) : 0f;
        public float HealthRatio => MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;
    }
}
