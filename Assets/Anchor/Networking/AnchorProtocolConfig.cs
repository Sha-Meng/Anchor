using System;
using System.IO;
using UnityEngine;

namespace Anchor.Networking
{
    [Serializable]
    public class AnchorProtocolConfig
    {
        public string version;
        public AnchorTransportConfig transport;
        public AnchorEnvelopeConfig envelope;
        public AnchorSpawnAnchorConfig spawnAnchors;
        public AnchorMessageConfig[] messages;
        public string[] notes;

        public string DefaultEndpoint
        {
            get
            {
                if (transport != null && !string.IsNullOrEmpty(transport.defaultEndpoint))
                {
                    return transport.defaultEndpoint;
                }

                return "ws://43.156.16.10:8080/ws";
            }
        }

        public float DemoStateSendInterval
        {
            get
            {
                if (messages == null) return 0.1f;

                foreach (var message in messages)
                {
                    if (message != null && message.type == "game.state" && message.sendRateHz > 0)
                    {
                        return 1f / message.sendRateHz;
                    }
                }

                return 0.1f;
            }
        }

        public AnchorSpawnSlotConfig HostSpawn
        {
            get
            {
                return spawnAnchors != null
                    ? spawnAnchors.GetHostSpawn()
                    : AnchorSpawnSlotConfig.CreateHostDefault();
            }
        }

        public AnchorSpawnSlotConfig GuestSpawn
        {
            get
            {
                return spawnAnchors != null
                    ? spawnAnchors.GetGuestSpawn()
                    : AnchorSpawnSlotConfig.CreateGuestDefault();
            }
        }

        public string HostLeadAnchorName => HostSpawn.LeftHandAnchorName;

        public string GuestSecondAnchorName => GuestSpawn.LeftHandAnchorName;

        public static AnchorProtocolConfig Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "coop-network-protocol.config.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonUtility.FromJson<AnchorProtocolConfig>(json);
                if (config != null) return config;
            }

            return new AnchorProtocolConfig
            {
                version = "fallback",
                transport = new AnchorTransportConfig
                {
                    defaultEndpoint = "ws://43.156.16.10:8080/ws",
                    fallbackEndpoint = "ws://43.156.16.10:8080/ws"
                },
                spawnAnchors = AnchorSpawnAnchorConfig.CreateDefault(),
                messages = Array.Empty<AnchorMessageConfig>()
            };
        }
    }

    [Serializable]
    public class AnchorTransportConfig
    {
        public string defaultEndpoint;
        public string fallbackEndpoint;
    }

    [Serializable]
    public class AnchorEnvelopeConfig
    {
        public string[] fields;
    }

    [Serializable]
    public class AnchorSpawnAnchorConfig
    {
        public AnchorSpawnSlotConfig host;
        public AnchorSpawnSlotConfig guest;
        public string hostLeadAnchorName;
        public string guestSecondAnchorName;

        public AnchorSpawnSlotConfig GetHostSpawn()
        {
            return AnchorSpawnSlotConfig.CreateHostDefault(host, hostLeadAnchorName);
        }

        public AnchorSpawnSlotConfig GetGuestSpawn()
        {
            return AnchorSpawnSlotConfig.CreateGuestDefault(guest, guestSecondAnchorName);
        }

        public static AnchorSpawnAnchorConfig CreateDefault()
        {
            return new AnchorSpawnAnchorConfig
            {
                host = AnchorSpawnSlotConfig.CreateHostDefault(),
                guest = AnchorSpawnSlotConfig.CreateGuestDefault(),
                hostLeadAnchorName = "ScatterAnchor_007",
                guestSecondAnchorName = "ScatterAnchor_001"
            };
        }
    }

    [Serializable]
    public class AnchorSpawnSlotConfig
    {
        public string slot;
        public string startPointName;
        public string leftHandAnchorName;
        public string rightHandAnchorName;

        public string StartPointName => startPointName;
        public string LeftHandAnchorName => leftHandAnchorName;
        public string RightHandAnchorName => rightHandAnchorName;

        public static AnchorSpawnSlotConfig CreateHostDefault(AnchorSpawnSlotConfig source = null, string legacyPrimaryAnchorName = null)
        {
            return CreateWithDefaults(
                source,
                "host",
                "HostStartPoint",
                string.IsNullOrEmpty(legacyPrimaryAnchorName) ? "ScatterAnchor_007" : legacyPrimaryAnchorName,
                "ScatterAnchor_008");
        }

        public static AnchorSpawnSlotConfig CreateGuestDefault(AnchorSpawnSlotConfig source = null, string legacyPrimaryAnchorName = null)
        {
            return CreateWithDefaults(
                source,
                "guest",
                "GuestStartPoint",
                string.IsNullOrEmpty(legacyPrimaryAnchorName) ? "ScatterAnchor_001" : legacyPrimaryAnchorName,
                "ScatterAnchor_002");
        }

        private static AnchorSpawnSlotConfig CreateWithDefaults(
            AnchorSpawnSlotConfig source,
            string defaultSlot,
            string defaultStartPointName,
            string defaultLeftHandAnchorName,
            string defaultRightHandAnchorName)
        {
            return new AnchorSpawnSlotConfig
            {
                slot = !string.IsNullOrEmpty(source?.slot) ? source.slot : defaultSlot,
                startPointName = !string.IsNullOrEmpty(source?.startPointName) ? source.startPointName : defaultStartPointName,
                leftHandAnchorName = !string.IsNullOrEmpty(source?.leftHandAnchorName) ? source.leftHandAnchorName : defaultLeftHandAnchorName,
                rightHandAnchorName = !string.IsNullOrEmpty(source?.rightHandAnchorName) ? source.rightHandAnchorName : defaultRightHandAnchorName
            };
        }
    }

    [Serializable]
    public class AnchorMessageConfig
    {
        public string type;
        public string schema;
        public string displayName;
        public string sendMode;
        public float sendRateHz;
        public bool relay;
        public AnchorPayloadField[] payloadFields;
    }

    [Serializable]
    public class AnchorPayloadField
    {
        public string name;
        public string type;
        public bool required;
    }
}
