using System.Collections.Generic;

namespace ClimbGame.Climb3C.State
{
    /// <summary>
    /// 游戏运行时上下文：集中保存所有攀爬者的运行时数据（<see cref="ClimberRuntimeState"/>）。
    /// 单机时只有本地一名玩家；联机时每个 PlayerId 对应一份状态，
    /// 网络层可从这里读取本地状态发送、把远端状态写回，逻辑层与化身层据此驱动各自玩家。
    /// </summary>
    public sealed class GameContext
    {
        private readonly Dictionary<int, ClimberRuntimeState> _players = new Dictionary<int, ClimberRuntimeState>();

        /// <summary>本地玩家 id。</summary>
        public int LocalPlayerId { get; private set; }

        public GameContext(int localPlayerId = 0)
        {
            LocalPlayerId = localPlayerId;
        }

        /// <summary>本地玩家运行时状态。</summary>
        public ClimberRuntimeState Local => GetOrCreate(LocalPlayerId, true);

        public IReadOnlyDictionary<int, ClimberRuntimeState> Players => _players;

        /// <summary>获取或创建指定玩家的运行时状态。</summary>
        public ClimberRuntimeState GetOrCreate(int playerId, bool isLocal = false)
        {
            if (!_players.TryGetValue(playerId, out var state))
            {
                state = new ClimberRuntimeState { PlayerId = playerId, IsLocal = isLocal };
                _players[playerId] = state;
            }
            return state;
        }

        public bool TryGet(int playerId, out ClimberRuntimeState state) => _players.TryGetValue(playerId, out state);

        public void Remove(int playerId) => _players.Remove(playerId);
    }
}
