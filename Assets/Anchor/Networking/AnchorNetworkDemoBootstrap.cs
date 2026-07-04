using UnityEngine;

namespace Anchor.Networking
{
    public class AnchorNetworkDemoBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            AnchorNetworkDemoController.EnsureDemo();
        }
    }
}
