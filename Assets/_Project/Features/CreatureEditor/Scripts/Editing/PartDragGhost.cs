using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A translucent preview of the part being dragged out of the palette.
    /// </summary>
    /// <remarks>
    /// Without it the drag is blind — the player only finds out where the part will land after
    /// releasing the button. The ghost also shows whether dropping will work at all: over the body it
    /// sits on the skin in its final pose, off the body it hangs by the cursor in the refusal colour.
    /// </remarks>
    public class PartDragGhost : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CreaturePartCatalog _catalog;

        [Tooltip("How far in front of the camera the ghost hangs when the cursor is not over the body.")]
        [SerializeField] private float _freeDistance = 3f;

        [SerializeField] private Color _validColor = new(0.45f, 1f, 0.55f, 0.65f);
        [SerializeField] private Color _invalidColor = new(1f, 0.4f, 0.35f, 0.5f);

        private readonly List<Renderer> _renderers = new();

        private GameObject _instance;
        private MaterialPropertyBlock _block;

        public bool IsVisible => _instance != null;

        public void Show(int partId)
        {
            Hide();

            if (_catalog == null || !_catalog.TryGetPart(partId, out CreaturePartDefinition definition)) return;
            if (definition == null || definition.Prefab == null) return;

            _instance = Instantiate(definition.Prefab, transform);
            _instance.name = $"Ghost_{definition.PartKey}";

            _instance.GetComponentsInChildren(true, _renderers);
            SetOverBody(false);
        }

        public void Hide()
        {
            if (_instance != null) Destroy(_instance);

            _instance = null;
            _renderers.Clear();
        }

        public void Place(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_instance == null) return;
            _instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        /// <summary>Hangs the ghost by the cursor when there is no body underneath it.</summary>
        public void FollowPointer(UnityEngine.Camera camera, Vector2 pointer)
        {
            if (_instance == null || camera == null) return;

            Ray ray = camera.ScreenPointToRay(pointer);
            _instance.transform.SetPositionAndRotation(ray.GetPoint(_freeDistance), camera.transform.rotation);
        }

        public void SetOverBody(bool overBody)
        {
            if (_renderers.Count == 0) return;

            _block ??= new MaterialPropertyBlock();
            Color color = overBody ? _validColor : _invalidColor;

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void OnDestroy() => Hide();
    }
}
