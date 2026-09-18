using UnityEngine;

namespace Leeway.Creature
{
    /// <summary>
    /// The sole owner of the creature's visual colour (SRP — game logic never touches the renderer).
    /// It shifts the colour towards red as HP drops, giving immediate combat feedback without
    /// needing a full HUD.
    /// </summary>
    [RequireComponent(typeof(CreatureEntity))]
    public class CreatureVisuals : MonoBehaviour
    {
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Color _healthyColor = Color.white;
        [SerializeField] private Color _lowHpColor = Color.red;

        private CreatureEntity _creature;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _creature = GetComponent<CreatureEntity>();
            _block = new MaterialPropertyBlock();
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (_creature == null) return;
            _creature.HpChanged += OnHpChanged;
            OnHpChanged(_creature.Hp);
        }

        private void OnDisable()
        {
            if (_creature != null) _creature.HpChanged -= OnHpChanged;
        }

        private void OnHpChanged(float hp)
        {
            float ratio = _creature.Config != null && _creature.Config.BaseHp > 0f
                ? Mathf.Clamp01(hp / _creature.Config.BaseHp)
                : 1f;
            Color color = Color.Lerp(_lowHpColor, _healthyColor, ratio);

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
