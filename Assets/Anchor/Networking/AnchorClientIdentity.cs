using UnityEngine;

namespace Anchor.Networking
{
    public static class AnchorClientIdentity
    {
        private const string ClientIdKey = "Anchor.Networking.ClientId";

        public static string GetOrCreateClientId()
        {
            var value = PlayerPrefs.GetString(ClientIdKey, string.Empty);
            if (!string.IsNullOrEmpty(value)) return value;

            value = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ClientIdKey, value);
            PlayerPrefs.Save();
            return value;
        }
    }
}
