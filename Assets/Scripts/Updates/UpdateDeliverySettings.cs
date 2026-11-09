using UnityEngine;

namespace Unite.Updates
{
    [CreateAssetMenu(menuName = "Unite/Update Delivery Settings")]
    public sealed class UpdateDeliverySettings : ScriptableObject
    {
        [Tooltip("Public HTTPS origin. Leave empty for local development. No credentials belong here.")]
        public string baseUrl = "";
        [Tooltip("Change when the player can no longer read the previous content/schema.")]
        public string compatibilityLine = "1";
        public string addressablesVersion = "1";
        public string patchVersion = "1";
        public int requestTimeoutSeconds = 30;
        public int retryCount = 2;
        public bool useRemoteInEditor;

        public static UpdateDeliverySettings Load() => Resources.Load<UpdateDeliverySettings>("UpdateDeliverySettings");
        public string Root => baseUrl.TrimEnd('/') + "/" + compatibilityLine;
        public string ManifestUrl(string platform) => Root + "/players/" + Application.version + "/" + platform + "/latest.json";
    }
}
