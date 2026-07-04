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

        public string HostLeadAnchorName
        {
            get
            {
                return spawnAnchors != null && !string.IsNullOrEmpty(spawnAnchors.hostLeadAnchorName)
                    ? spawnAnchors.hostLeadAnchorName
                    : "ScatterAnchor_003";
            }
        }

        public string GuestSecondAnchorName
        {
            get
            {
                return spawnAnchors != null && !string.IsNullOrEmpty(spawnAnchors.guestSecondAnchorName)
                    ? spawnAnchors.guestSecondAnchorName
                    : HostLeadAnchorName;
            }
        }

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
        public string hostLeadAnchorName;
        public string guestSecondAnchorName;

        public static AnchorSpawnAnchorConfig CreateDefault()
        {
            return new AnchorSpawnAnchorConfig
            {
                hostLeadAnchorName = "ScatterAnchor_003",
                guestSecondAnchorName = "ScatterAnchor_003"
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
