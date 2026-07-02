using UnityEngine;
using UnityEngine.UI;
using Wapawapa.Abilities;

namespace Wapawapa.UI
{
    [RequireComponent(typeof(PlayerDamageReceiver))]
    public sealed class PlayerWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.55f, 0f);
        [SerializeField] private Vector2 size = new Vector2(140f, 16f);

        private PlayerDamageReceiver receiver;
        private Canvas canvas;
        private Image fillImage;
        private RectTransform fillRect;
        private Camera targetCamera;
        private readonly Color fillColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
        private const float FillLeft = 0.04f;
        private const float FillRight = 0.96f;

        private void Awake()
        {
            receiver = GetComponent<PlayerDamageReceiver>();
            if (followTarget == null)
            {
                var head = transform.Find("Head");
                followTarget = head != null ? head : transform;
            }

            CreateCanvas();
        }

        private void OnEnable()
        {
            if (receiver != null)
            {
                receiver.HealthChanged += OnHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (receiver != null)
            {
                receiver.HealthChanged -= OnHealthChanged;
            }
        }

        private void LateUpdate()
        {
            if (canvas == null || fillImage == null)
            {
                return;
            }

            canvas.transform.position = followTarget.position + worldOffset;
            targetCamera = ResolveCamera();
            if (targetCamera != null)
            {
                canvas.worldCamera = targetCamera;
                canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - targetCamera.transform.position, Vector3.up);
            }

            UpdateFill();
        }

        private void OnHealthChanged(PlayerDamageReceiver _)
        {
            UpdateFill();
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject("World HP Bar");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * 0.01f;

            var background = new GameObject("Background").AddComponent<Image>();
            background.transform.SetParent(canvas.transform, false);
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.82f);
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            fillImage = new GameObject("Fill").AddComponent<Image>();
            fillImage.transform.SetParent(canvas.transform, false);
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Simple;
            fillRect = fillImage.rectTransform;
            fillRect.anchorMin = new Vector2(FillLeft, 0.18f);
            fillRect.anchorMax = new Vector2(FillRight, 0.82f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            UpdateFill();
        }

        private void UpdateFill()
        {
            if (receiver == null || fillImage == null || fillRect == null)
            {
                return;
            }

            var ratio = receiver.MaxHealth > 0f ? Mathf.Clamp01(receiver.Health / receiver.MaxHealth) : 0f;
            fillRect.anchorMax = new Vector2(Mathf.Lerp(FillLeft, FillRight, ratio), fillRect.anchorMax.y);
            fillImage.color = fillColor;
        }

        private static Camera ResolveCamera()
        {
            var main = Camera.main;
            if (main != null && main.enabled)
            {
                return main;
            }

            var cameras = Camera.allCameras;
            foreach (var camera in cameras)
            {
                if (camera != null && camera.enabled)
                {
                    return camera;
                }
            }

            return null;
        }
    }
}
