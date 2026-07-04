using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeDebugPanel : MonoBehaviour, IRivetRopeDamageSink
    {
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private bool showPanel = true;
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 420f, 260f);
        [SerializeField] private Transform samplePlacePoint;
        [SerializeField] private Transform sampleCollectPlayerPoint;

        private RopeFallResolution _lastResolution;

        public void OnRivetRopeFallResolved(RopeFallResolution resolution)
        {
            _lastResolution = resolution;
        }

        private void OnGUI()
        {
            if (!showPanel || driver == null)
            {
                return;
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Rivet Rope Debug");
            GUILayout.Label($"Lead: {driver.Model.LeadPlayerId} inv={driver.Model.GetInventory(driver.Model.LeadPlayerId)}");
            GUILayout.Label($"Second: {driver.Model.SecondPlayerId} inv={driver.Model.GetInventory(driver.Model.SecondPlayerId)}");
            GUILayout.Label($"Placed: {driver.Model.PlacedRivets.Count} / Revision: {driver.Model.RopeRevision}");
            GUILayout.Label($"Rope: {driver.LastPath.TensionState} used={driver.LastPath.UsedLength:0.00} slack={driver.LastPath.RemainingSlack:0.00} constraint={driver.LastPath.ConstraintDistance:0.00}");
            GUILayout.Label($"Rescue: active={driver.Model.RescueState.IsActive} pull={driver.Model.RescueState.PullAmount:0.00} clicks={driver.Model.RescueState.EffectiveClickCount}");
            GUILayout.Label($"Fall: protected={_lastResolution.IsProtected} damage={_lastResolution.SuggestedDamage:0.0} reason={_lastResolution.Reason}");

            GUILayout.Space(6f);
            if (GUILayout.Button("Lead 插锚"))
            {
                driver.DebugPlaceLeadRivet(samplePlacePoint != null ? samplePlacePoint.position : Vector3.up * 5f);
            }

            if (GUILayout.Button("Second 回收第一个铆钉"))
            {
                if (driver.Model.PlacedRivets.Count > 0)
                {
                    var rivet = driver.Model.PlacedRivets[0];
                    driver.DebugCollectSecondRivet(rivet.RivetId, rivet.Position, true);
                }
            }

            if (GUILayout.Button("触发上方坠落"))
            {
                driver.StartRescueWindow();
                driver.DebugResolveLeadFall();
            }

            if (GUILayout.Button("下方玩家收绳"))
            {
                driver.ApplyRescueClick(true, true);
                driver.DebugResolveLeadFall();
            }

            if (GUILayout.Button("运行完整冒烟验收"))
            {
                driver.DebugRunSmokeSequence();
            }

            GUILayout.EndArea();
        }
    }
}
