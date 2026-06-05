using UnityEngine;

namespace Leeway.Creature
{
    /// <summary>
    /// Jedyny właściciel skali wizualnej komórki (SRP — logika gry nie dotyka
    /// <c>transform</c>). Skalę bazową bierze z rozmiaru <see cref="CellEntity"/>,
    /// a na wierzch nakłada delikatny „wobble/pulse", żeby komórka żyła.
    /// </summary>
    [RequireComponent(typeof(CellEntity))]
    public class CellVisuals : MonoBehaviour
    {
        [SerializeField] private float _wobbleSpeed = 1.8f;
        [SerializeField] private float _wobbleAmount = 0.04f;
        [SerializeField] private float _pulseSpeed = 0.8f;
        [SerializeField] private float _pulseAmount = 0.03f;

        private CellEntity _cell;
        private float _baseScale = 1f;
        private float _wobbleOffset;
        private float _pulseOffset;

        private void Awake()
        {
            _cell = GetComponent<CellEntity>();
            _wobbleOffset = Random.Range(0f, Mathf.PI * 2f);
            _pulseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            if (_cell == null) return;
            _cell.SizeChanged += OnSizeChanged;
            OnSizeChanged(_cell.Size);
        }

        private void OnDisable()
        {
            if (_cell != null) _cell.SizeChanged -= OnSizeChanged;
        }

        private void OnSizeChanged(float size) => _baseScale = size;

        private void Update()
        {
            float wobbleX = 1f + Mathf.Sin(Time.time * _wobbleSpeed + _wobbleOffset) * _wobbleAmount;
            float wobbleY = 1f + Mathf.Cos(Time.time * _wobbleSpeed * 0.7f + _wobbleOffset) * _wobbleAmount;
            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed + _pulseOffset) * _pulseAmount;

            transform.localScale = new Vector3(_baseScale * wobbleX * pulse, _baseScale * wobbleY * pulse, 1f);
        }
    }
}
