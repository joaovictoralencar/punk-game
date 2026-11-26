using Unity.Cinemachine;
using UnityEngine;

namespace PunkGame.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class WorldToScreenFollower : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _rectTransform;

        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private Vector3 _worldOffset = new Vector3(0f, 2f, 0f);

        private CanvasGroup _canvasGroup;
        private bool _isVisible = true;
        private bool _isInFrontOfCamera;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();

            if (_canvas == null)
            {
                Debug.LogError($"[{nameof(WorldToScreenFollower)}] A parent Canvas is required.", this);
            }

            if (_rectTransform == null || _rectTransform.parent is not RectTransform)
            {
                Debug.LogError($"[{nameof(WorldToScreenFollower)}] The UI element must be parented under a RectTransform.", this);
            }
        }

        private void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
            UpdateVisibility();
        }

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _isInFrontOfCamera = target != null;
            UpdateVisibility();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            UpdateVisibility();
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            if (brain == null || brain.OutputCamera == null)
            {
                _isInFrontOfCamera = false;
                UpdateVisibility();
                return;
            }

            if (_target == null)
            {
                _isInFrontOfCamera = false;
                UpdateVisibility();
                return;
            }

            if (_canvas == null || _rectTransform == null) return;

            Vector3 screenPosition = brain.OutputCamera.WorldToScreenPoint(_target.position + _worldOffset);
            _isInFrontOfCamera = screenPosition.z > 0f;
            UpdateVisibility();
            if (!_isInFrontOfCamera) return;

            RectTransform parentRect = _rectTransform.parent as RectTransform;
            if (parentRect == null) return;

            Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera != null ? _canvas.worldCamera : brain.OutputCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    canvasCamera,
                    out Vector2 localPosition))
            {
                return;
            }

            Vector3 position = _rectTransform.localPosition;
            position.x = localPosition.x;
            position.y = localPosition.y;
            _rectTransform.localPosition = position;
        }

        private void CacheReferences()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void UpdateVisibility()
        {
            if (_canvasGroup == null) return;

            bool visible = _isVisible && _isInFrontOfCamera;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
