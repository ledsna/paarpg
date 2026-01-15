using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Clouds
{
    public class CloudsSettings : MonoBehaviour
    {
        private Light lightComponent;
        private UniversalAdditionalLightData lightData;

        [SerializeField] private CustomRenderTexture renderTexture;

        [SerializeField] private float cookieSteps = -1;
        [SerializeField] private Texture2D noise;
        [SerializeField] private Texture2D details;
        [SerializeField] private Vector2 cookieSize = new(30, 30);
        [SerializeField] private Vector2 noiseSpeed = new(0.2f, 0.2f);
        [SerializeField] private Vector2 detailsSpeed = new(0.33f, 0.5f);

        private bool initialized = false;

        [ExecuteAlways]
        void Start()
        {
            if (!initialized)
                Initialize();
        }

        private void Initialize()
        {
            lightComponent = GetComponent<Light>();
            lightData = GetComponent<UniversalAdditionalLightData>();
            lightComponent.cookie = renderTexture;
            lightData.lightCookieSize = cookieSize;
            initialized = true;
            UpdateCookie();
        }

        private void OnValidate()
        {
            UpdateCookie();
        }

        void UpdateCookie()
        {
            if (!initialized)
                return;
            renderTexture.material.SetTexture("_Noise", noise);
            renderTexture.material.SetTexture("_Details", details);
            renderTexture.material.SetFloat("_CookieSteps", cookieSteps);
            renderTexture.material.SetVector("_NoiseSpeed", noiseSpeed);
            renderTexture.material.SetVector("_DetailsSpeed", detailsSpeed);
            lightData.lightCookieSize = cookieSize;
        }
    }
}