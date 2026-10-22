using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    // Camera-wide presentation. Disabling restores the original camera output.
    public sealed class WorldPictureStyle : MonoBehaviour
    {
        public Camera worldCamera;
        public Material pictureMaterial;
        [Range(240,1080)] public int pictureHeight = 540;
        RenderTexture picture, previousTarget;
        GameObject output;
        RawImage image;
        int screenWidth, screenHeight;

        void OnEnable()
        {
            if (!worldCamera || !pictureMaterial) return;
            previousTarget = worldCamera.targetTexture;
            output = new GameObject("World picture output", typeof(RectTransform), typeof(Canvas));
            output.transform.SetParent(transform, false);
            var canvas = output.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = worldCamera.targetDisplay;
            canvas.sortingOrder = -100;
            // The world camera renders into a texture. Keep a real display camera
            // behind the overlay so Unity presents Display 1 without its no-camera warning.
            var display = new GameObject("World picture display", typeof(Camera));
            display.transform.SetParent(output.transform, false);
            var displayCamera = display.GetComponent<Camera>();
            displayCamera.targetDisplay = worldCamera.targetDisplay;
            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = Color.black;
            displayCamera.depth = worldCamera.depth + 1;
            displayCamera.allowHDR = false;
            displayCamera.allowMSAA = false;
            displayCamera.useOcclusionCulling = false;
            var panel = new GameObject("Styled world", typeof(RectTransform), typeof(RawImage));
            panel.transform.SetParent(output.transform, false);
            image = panel.GetComponent<RawImage>();
            image.material = pictureMaterial;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Resize();
        }

        void Update()
        {
            if (image && (screenWidth != Screen.width || screenHeight != Screen.height ||
                picture.height != Mathf.Min(pictureHeight, Screen.height))) Resize();
        }

        void Resize()
        {
            screenWidth = Mathf.Max(1, Screen.width); screenHeight = Mathf.Max(1, Screen.height);
            int h = Mathf.Clamp(pictureHeight, 1, screenHeight);
            int w = Mathf.Max(1, Mathf.RoundToInt((float)screenWidth / screenHeight * h));
            worldCamera.targetTexture = previousTarget;
            if (picture) { picture.Release(); Destroy(picture); }
            picture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "V4 world picture", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
            };
            picture.Create();
            worldCamera.targetTexture = picture;
            image.texture = picture;
        }

        void OnDisable()
        {
            if (worldCamera) worldCamera.targetTexture = previousTarget;
            if (picture) { picture.Release(); Destroy(picture); }
            if (output) Destroy(output);
        }
    }
}
