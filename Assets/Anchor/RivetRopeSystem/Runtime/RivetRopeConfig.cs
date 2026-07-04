using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    [CreateAssetMenu(menuName = "Anchor/Rivet Rope Config", fileName = "RivetRopeConfig")]
    public sealed class RivetRopeConfig : ScriptableObject
    {
        [SerializeField] private RivetRopeSettings settings = RivetRopeSettings.CreateDefault();

        public RivetRopeSettings Settings => settings.Sanitized();

        private void OnValidate()
        {
            settings = settings.Sanitized();
        }
    }
}
