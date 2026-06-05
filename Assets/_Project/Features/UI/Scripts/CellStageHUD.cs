using System;
using Leeway.Creature;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.UI
{
    public class CellStageHUD : MonoBehaviour
    {
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _evolutionSlider;
        [SerializeField] private TextMeshProUGUI _sizeText;
        [SerializeField] private TextMeshProUGUI _statusText;

        private CellEntity _localCell;
        private IDisposable _subscription;

        private void Start()
        {
            if (GlobalMessagePipe.IsInitialized)
                _subscription = GlobalMessagePipe.GetSubscriber<LocalCellChangedMessage>().Subscribe(msg => SetLocalCell(msg.Cell));
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            Unbind();
        }

        private void SetLocalCell(CellEntity cell)
        {
            Unbind();
            _localCell = cell;
            if (_localCell == null) return;

            _localCell.HpChanged += UpdateHp;
            _localCell.SizeChanged += UpdateSize;

            UpdateHp(_localCell.Hp);
            UpdateSize(_localCell.Size);
        }

        private void Unbind()
        {
            if (_localCell == null) return;
            _localCell.HpChanged -= UpdateHp;
            _localCell.SizeChanged -= UpdateSize;
            _localCell = null;
        }

        private void UpdateHp(float hp)
        {
            if (_hpSlider == null) return;
            _hpSlider.maxValue = _localCell != null ? _localCell.Config.BaseHp : 100f;
            _hpSlider.value = hp;
        }

        private void UpdateSize(float size)
        {
            if (_evolutionSlider != null)
            {
                _evolutionSlider.maxValue = _localCell != null ? _localCell.Config.MaxSize : 8f;
                _evolutionSlider.value = size;
            }
            if (_sizeText != null)
                _sizeText.text = $"Rozmiar: {size:F2}";
        }

        private void Update()
        {
            if (_statusText == null || _localCell == null) return;
            _statusText.text = _localCell.IsAlive ? "Zyjesz!" : "Umarles...";
        }
    }
}
