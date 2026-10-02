using UnityEngine;

namespace PineAppleGames.Shaders
{
    [DisallowMultipleComponent]
    public sealed class PortalFXController : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private bool FX_rotate;
        [SerializeField] private Vector3 FX_rotationAxis = Vector3.up;
        [SerializeField] private float FX_rotationSpeed = 90f;
        [SerializeField] private Space FX_rotationSpace = Space.Self;

        [Header("Visibility")]
        [SerializeField] private bool FX_disappearOnStart;
        [SerializeField] private bool FX_disappear;
        [SerializeField] private float FX_fadeSpeed = 6f;
        [SerializeField] private bool FX_destroyAfterDisappear = true;
        [SerializeField] private GameObject FX_destroyGameObject;

        private Renderer[] FX_renderers;
        private MaterialPropertyBlock FX_propertyBlock;
        private float FX_visibility;
        private bool FX_destroyQueued;
        private static readonly int FX_opacityId = Shader.PropertyToID("_Opacity");

        public bool Disappear
        {
            get => FX_disappear;
            set => FX_disappear = value;
        }

        private void Awake()
        {
            FX_renderers = GetComponentsInChildren<Renderer>(true);
            FX_propertyBlock = new MaterialPropertyBlock();
            FX_visibility = FX_disappearOnStart ? 0f : 1f;
            ApplyVisibility();
        }

        private void OnEnable()
        {
            if (FX_propertyBlock == null)
                return;

            if (!FX_disappearOnStart)
                FX_visibility = 1f;

            ApplyVisibility();
        }

        private void Update()
        {
            if (FX_rotate && FX_rotationAxis.sqrMagnitude > 0.0001f)
                transform.Rotate(FX_rotationAxis.normalized, FX_rotationSpeed * Time.deltaTime, FX_rotationSpace);

            float targetVisibility = FX_disappear ? 0f : 1f;
            FX_visibility = Mathf.MoveTowards(FX_visibility, targetVisibility, FX_fadeSpeed * Time.deltaTime);
            ApplyVisibility();

            if (FX_disappear && FX_visibility <= 0f && FX_destroyAfterDisappear)
                DestroyAfterDisappear();
        }

        public void Show()
        {
            FX_disappear = false;
            FX_visibility = 1f;
            ApplyVisibility();
        }

        public void DisappearNow()
        {
            FX_disappear = true;
        }

        private void DestroyAfterDisappear()
        {
            if (FX_destroyQueued)
                return;

            FX_destroyQueued = true;
            GameObject destroyTarget = FX_destroyGameObject != null
                ? FX_destroyGameObject
                : gameObject;
            destroyTarget.transform.SetParent(null, true);
            Destroy(destroyTarget);
        }

        private void ApplyVisibility()
        {
            if (FX_renderers == null)
                return;

            foreach (Renderer FX_renderer in FX_renderers)
            {
                if (FX_renderer == null)
                    continue;

                FX_renderer.GetPropertyBlock(FX_propertyBlock);
                FX_propertyBlock.SetFloat(FX_opacityId, FX_visibility);
                FX_renderer.SetPropertyBlock(FX_propertyBlock);
            }
        }
    }
}
