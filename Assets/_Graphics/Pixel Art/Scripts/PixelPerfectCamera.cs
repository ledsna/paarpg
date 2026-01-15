using UnityEngine;
using UnityEngine.UI;

namespace PixelPerfectCamera
{
    public class PixelPerfectCamera : MonoBehaviour
    {
        public static PixelPerfectCamera instance;
        
        [SerializeField] public Camera mainCamera;
        [SerializeField] private RawImage orthographicTexture;

        private float pixelW, pixelH;
        private Rect orthographicTextureRect;

        private void Setup()
        {
            pixelW = 1f / mainCamera.scaledPixelWidth;
            pixelH = 1f / mainCamera.scaledPixelHeight;

            orthographicTextureRect = new Rect(0, 0, 1f - pixelW, 1f - pixelH);
        }

        public void ApplyPixelPerfect()
        {
            var pixelsPerUnit = mainCamera.scaledPixelHeight / mainCamera.orthographicSize * 0.5f;

            var pixelDiffInPixelSpace = GetPixelDiffInPixelSpace(pixelsPerUnit);
            
            mainCamera.transform.localPosition = new Vector3(
                pixelDiffInPixelSpace.x / pixelsPerUnit,
                pixelDiffInPixelSpace.y / pixelsPerUnit,
                mainCamera.transform.localPosition.z
            );

            orthographicTextureRect.x = (0.5f + -pixelDiffInPixelSpace.x) * pixelW;
            orthographicTextureRect.y = (0.5f + -pixelDiffInPixelSpace.y) * pixelH;
            orthographicTexture.uvRect = orthographicTextureRect;
        }


        private Vector2 GetPixelDiffInPixelSpace(float ppu)
        {
            var snapSpacePosition = transform.InverseTransformVector(transform.position);

            var pixelSpacePosition = new Vector2(
                snapSpacePosition.x,
                snapSpacePosition.y) * ppu;

            // Making snap to grid 
            var snappedPixelSpacePosition = new Vector2(
                Mathf.Round(snapSpacePosition.x * ppu),
                Mathf.Round(snapSpacePosition.y * ppu)
            );

            return snappedPixelSpacePosition - pixelSpacePosition;
        }

        private void Start() => Setup();

        private void Awake()
        {
            if (instance != null)
                Destroy(gameObject);
            instance = this;
        }

        public void LateUpdate()
        {
            if (mainCamera.orthographic)
                ApplyPixelPerfect();
        }
    }
}