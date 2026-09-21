using System;
using Leeway.CreatureEditor;
using MessagePipe;
using TMPro;
using UnityEngine;

namespace Leeway.Combat
{
    /// <summary>
    /// The part of a fight the player can actually read: their own hit points, and what just hit them.
    /// </summary>
    /// <remarks>
    /// <para>Without this the combat is invisible. Damage is a server-side number on a SyncVar, and a
    /// player who cannot see it falling has no way to tell a fight they are winning from one they are
    /// losing — or that they are in one at all.</para>
    ///
    /// <para>It listens to messages rather than holding a reference to the creature: the local body
    /// arrives, is replaced on a rebuild and goes away on disconnect, and the bus already carries all
    /// three. The hit line is fed by the blow's own message instead of by watching hit points, so the
    /// number shown is the blow that was struck, not the difference between two sync updates.</para>
    /// </remarks>
    public class CombatHud : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private TextMeshProUGUI _hitText;

        [Header("Appearance")]
        [SerializeField] private Color _healthy = new(0.85f, 0.92f, 0.85f, 1f);
        [SerializeField] private Color _hurt = new(0.95f, 0.55f, 0.35f, 1f);

        [Tooltip("Below this fraction of full health the readout turns.")]
        [SerializeField, Range(0f, 1f)] private float _hurtBelow = 0.4f;

        [Tooltip("How long a hit line stays up.")]
        [SerializeField, Min(0.2f)] private float _hitSeconds = 1.6f;

        private IDisposable _subscriptions;
        private CreatureBody _body;
        private float _hitUntil;

        private void OnEnable()
        {
            if (!GlobalMessagePipe.IsInitialized)
            {
                Debug.LogWarning("The message bus is not up — the combat HUD will stay silent.", this);
                return;
            }

            var bag = DisposableBag.CreateBuilder();

            GlobalMessagePipe.GetSubscriber<LocalCreatureBodyChangedMessage>()
                .Subscribe(OnLocalBodyChanged).AddTo(bag);

            GlobalMessagePipe.GetSubscriber<CreatureStruckMessage>()
                .Subscribe(OnStruck).AddTo(bag);

            GlobalMessagePipe.GetSubscriber<CreatureDiedMessage>()
                .Subscribe(OnDied).AddTo(bag);

            _subscriptions = bag.Build();

            Clear();
        }

        private void OnDisable()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }

        private void OnLocalBodyChanged(LocalCreatureBodyChangedMessage message)
        {
            _body = message.Body;
            Clear();
        }

        private void Update()
        {
            if (_healthText != null) _healthText.text = HealthLine();

            if (_hitText != null && _hitText.text.Length > 0 && Time.time >= _hitUntil)
                _hitText.text = string.Empty;
        }

        private string HealthLine()
        {
            if (_body == null) return string.Empty;

            float max = Mathf.Max(1f, _body.MaxHp);
            float fraction = Mathf.Clamp01(_body.Hp / max);

            _healthText.color = fraction <= _hurtBelow ? _hurt : _healthy;

            return _body.IsAlive
                ? $"HP {_body.Hp:F0} / {max:F0}"
                : "Martwy";
        }

        /// <summary>
        /// Shows the blow — but only the ones this player has a stake in. A distant scuffle between two
        /// animals is not news, and reporting it would bury the line that matters.
        /// </summary>
        private void OnStruck(CreatureStruckMessage message)
        {
            if (_hitText == null || _body == null || message.Damage <= 0f) return;

            bool taken = message.Target == _body;
            bool dealt = message.Attacker == _body;
            if (!taken && !dealt) return;

            _hitText.color = taken ? _hurt : _healthy;
            _hitText.text = taken
                ? $"Trafiony! −{message.Damage:F0}" + (message.KnockedDown ? "  (przewrócony)" : string.Empty)
                : $"Trafienie −{message.Damage:F0}" + (message.KnockedDown ? "  (przewrócony)" : string.Empty);

            _hitUntil = Time.time + _hitSeconds;
        }

        private void OnDied(CreatureDiedMessage message)
        {
            if (_hitText == null || _body == null) return;

            if (message.Body == _body)
            {
                _hitText.color = _hurt;
                _hitText.text = "Zginąłeś";
            }
            else if (message.Killer == _body)
            {
                _hitText.color = _healthy;
                _hitText.text = "Przeciwnik pokonany";
            }
            else return;

            _hitUntil = Time.time + _hitSeconds * 2f;
        }

        private void Clear()
        {
            if (_hitText != null) _hitText.text = string.Empty;
            if (_healthText != null) _healthText.text = string.Empty;
        }
    }
}
