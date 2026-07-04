using Anchor.RivetRopeSystem;
using UnityEngine;

namespace Anchor.Networking
{
    public sealed class RivetRopeNetworkBridge : MonoBehaviour, IRivetRopeNetworkSink
    {
        [SerializeField] private AnchorRelayClient relayClient;
        [SerializeField] private RivetRopeDebugDriver rivetRopeDriver;
        [SerializeField] private string roomId;
        [SerializeField] private string localPlayerId;
        [SerializeField] private bool syncEnabled;
        [SerializeField] private bool logEvents = true;

        private int _seq;

        public void Configure(string newRoomId, string newLocalPlayerId)
        {
            roomId = newRoomId;
            localPlayerId = newLocalPlayerId;
        }

        public void Configure(
            AnchorRelayClient newRelayClient,
            RivetRopeDebugDriver newRivetRopeDriver,
            string newRoomId,
            string newLocalPlayerId,
            bool newSyncEnabled)
        {
            if (relayClient != null && relayClient != newRelayClient && isActiveAndEnabled)
            {
                relayClient.MessageReceived -= HandleMessage;
            }

            relayClient = newRelayClient;
            rivetRopeDriver = newRivetRopeDriver;
            roomId = newRoomId;
            localPlayerId = newLocalPlayerId;
            syncEnabled = newSyncEnabled;

            if (relayClient != null && isActiveAndEnabled)
            {
                relayClient.MessageReceived -= HandleMessage;
                relayClient.MessageReceived += HandleMessage;
            }
        }

        public void SetSyncEnabled(bool enabled)
        {
            syncEnabled = enabled;
        }

        private void Awake()
        {
            if (relayClient == null)
            {
                relayClient = GetComponent<AnchorRelayClient>();
            }
        }

        private void OnEnable()
        {
            if (relayClient != null)
            {
                relayClient.MessageReceived += HandleMessage;
            }
        }

        private void OnDisable()
        {
            if (relayClient != null)
            {
                relayClient.MessageReceived -= HandleMessage;
            }
        }

        public void SendRivetRopeEvent(RivetRopeSyncEvent syncEvent)
        {
            if (!syncEnabled || relayClient == null || !relayClient.IsConnected || !syncEvent.IsValid)
            {
                return;
            }

            var payload = AnchorJson.BuildClimbEventPayload(
                syncEvent.EventId,
                syncEvent.EventType,
                syncEvent.ActorPlayerId,
                BuildRivetEventData(syncEvent));
            var envelope = AnchorJson.BuildEnvelope(
                "game.event",
                roomId: roomId,
                senderId: localPlayerId,
                seq: ++_seq,
                sentAt: Time.realtimeSinceStartup,
                schema: "climb-event.v1",
                payloadJson: payload);

            relayClient.SendText(envelope);

            if (logEvents)
            {
                Debug.Log($"Sent rivet event: {syncEvent.EventType} {syncEvent.RivetId} rev={syncEvent.RopeRevision}", this);
            }
        }

        private void HandleMessage(string json)
        {
            if (AnchorJson.GetString(json, "type") != "game.event")
            {
                return;
            }

            var senderId = AnchorJson.GetString(json, "senderId");
            if (!string.IsNullOrEmpty(senderId) && senderId == localPlayerId)
            {
                return;
            }

            var payload = AnchorJson.GetPayload(json);
            var eventType = AnchorJson.GetString(payload, "eventType");
            if (eventType != RivetRopeEventTypes.RivetPlace &&
                eventType != RivetRopeEventTypes.RivetCollect &&
                eventType != RivetRopeEventTypes.LeadSwitch)
            {
                return;
            }

            var data = AnchorJson.GetRawProperty(payload, "data") ?? "{}";
            var syncEvent = new RivetRopeSyncEvent
            {
                EventId = AnchorJson.GetString(payload, "eventId"),
                EventType = eventType,
                ActorPlayerId = AnchorJson.GetString(payload, "actorPlayerId"),
                RivetId = AnchorJson.GetString(data, "rivetId"),
                Position = AnchorJson.GetVector3(data, "position", Vector3.zero),
                InventoryAfter = Mathf.RoundToInt(AnchorJson.GetFloat(data, "inventoryAfter", 0f)),
                RopeRevision = Mathf.RoundToInt(AnchorJson.GetFloat(data, "ropeRevision", 0f))
            };

            var result = rivetRopeDriver != null
                ? rivetRopeDriver.ApplyRemoteEvent(syncEvent)
                : RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);

            if (logEvents)
            {
                Debug.Log($"Received rivet event: {eventType} {syncEvent.RivetId} success={result.Success} reason={result.FailureReason}", this);
            }
        }

        private static string BuildRivetEventData(RivetRopeSyncEvent syncEvent)
        {
            var data = "{"
                + AnchorJson.Pair("rivetId", syncEvent.RivetId) + ","
                + "\"inventoryAfter\":" + syncEvent.InventoryAfter + ","
                + "\"ropeRevision\":" + syncEvent.RopeRevision;

            if (syncEvent.EventType == RivetRopeEventTypes.RivetPlace)
            {
                data += ",\"position\":" + AnchorJson.BuildVector3(syncEvent.Position);
            }

            return data + "}";
        }
    }
}
