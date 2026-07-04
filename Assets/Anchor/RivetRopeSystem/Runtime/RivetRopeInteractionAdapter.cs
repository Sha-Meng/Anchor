using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeInteractionAdapter : MonoBehaviour
    {
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private string playerId = "lead";
        [SerializeField] private Transform placePoint;
        [SerializeField] private Transform collectPlayerTransform;
        [SerializeField] private string targetCollectRivetId;
        [SerializeField] private bool playerStable = true;
        [SerializeField] private bool playerInteractive = true;
        [SerializeField] private bool placeSurfaceValid = true;

        public RivetOperationResult PlaceAtPoint()
        {
            if (driver == null)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);
            }

            return driver.PlaceRivet(new RivetPlaceRequest
            {
                PlayerId = playerId,
                Position = placePoint != null ? placePoint.position : transform.position,
                IsValidSurface = placeSurfaceValid,
                IsPlayerInteractive = playerInteractive
            });
        }

        public RivetOperationResult CollectTarget()
        {
            if (driver == null)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);
            }

            return driver.CollectRivet(new RivetCollectRequest
            {
                PlayerId = playerId,
                RivetId = targetCollectRivetId,
                PlayerPosition = collectPlayerTransform != null ? collectPlayerTransform.position : transform.position,
                IsPlayerStable = playerStable,
                IsPlayerInteractive = playerInteractive
            });
        }

        public RescuePullResult RescuePullClick()
        {
            return driver != null
                ? driver.ApplyRescueClick(playerStable, playerInteractive)
                : new RescuePullResult { Success = false, FailureReason = RivetRopeFailureReason.PlayerNotInteractive };
        }

        public bool CanCollectTarget(out RivetRopeFailureReason failureReason)
        {
            if (driver == null)
            {
                failureReason = RivetRopeFailureReason.PlayerNotInteractive;
                return false;
            }

            return driver.CanCollectRivet(new RivetCollectRequest
            {
                PlayerId = playerId,
                RivetId = targetCollectRivetId,
                PlayerPosition = collectPlayerTransform != null ? collectPlayerTransform.position : transform.position,
                IsPlayerStable = playerStable,
                IsPlayerInteractive = playerInteractive
            }, out failureReason);
        }
    }
}
