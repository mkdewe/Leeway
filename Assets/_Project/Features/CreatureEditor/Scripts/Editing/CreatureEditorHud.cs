using System;
using Leeway.Creature.Domain;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The editor panel: the apply and revert buttons, the server status, and a live readout of the
    /// stats derived from the genome.
    /// </summary>
    /// <remarks>
    /// <para>The stat readout is not decoration — it makes the pure rules layer visible and lets you
    /// notice that an added vertebra did not translate into HP, before anyone goes looking in the
    /// code.</para>
    ///
    /// <para>The HUD is also responsible for getting all this machinery <b>off the screen</b> when the
    /// player switches to their creature — and for leaving them a visible way back. Which panels to
    /// switch off is named by the scene, not the code: the controller announces the mode, and whoever
    /// assembled the scene knows what counts as "sculpting UI" in it.</para>
    /// </remarks>
    public class CreatureEditorHud : MonoBehaviour
    {
        [Header("Control")]
        [SerializeField] private CreatureEditorController _controller;

        [Header("Widgets")]
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _revertButton;

        [Tooltip("Symmetry toggle for the selected part. Disabled when nothing is selected or the part has no mirrored version.")]
        [SerializeField] private Button _mirrorButton;
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _selectionText;

        [Header("Modes")]
        [Tooltip("Sculpting panels. They switch off when the player moves to their creature in the game.")]
        [SerializeField] private GameObject[] _sculptPanels;

        [Tooltip("The button back to sculpting. Left empty: the HUD clones it from the apply button.")]
        [SerializeField] private Button _returnButton;

        [Tooltip("The label on the return button. The key in brackets has to match the binding in CreatureEditorInput.")]
        [SerializeField] private string _returnLabel = "Back to editor (ESC)";

        private CreatureEditorSession _session;
        private IDisposable _sessionSubscription;
        private IDisposable _modeSubscription;
        private IDisposable _selectionSubscription;
        private IDisposable _partSelectionSubscription;
        private IDisposable _commitSubscription;

        private void Awake()
        {
            if (_applyButton != null) _applyButton.onClick.AddListener(() => _controller?.Apply());
            if (_revertButton != null) _revertButton.onClick.AddListener(() => _controller?.Revert());
            if (_mirrorButton != null) _mirrorButton.onClick.AddListener(() => _controller?.ToggleSelectedPartMirror());

            SetMirrorState(-1, mirrored: false, canMirror: false);
            EnsureReturnButton();
        }

        /// <summary>
        /// Builds the return button by cloning the apply button.
        /// </summary>
        /// <remarks>
        /// <para>A clone rather than a new object: the apply button already carries the background,
        /// font and text sizes set in the scene, so the return button looks like the rest of the panel
        /// without wiring anything up in the inspector.</para>
        ///
        /// <para>It lands on the <b>canvas</b> rather than inside the stats panel, because that whole
        /// panel switches off along with sculpting — a return button inside it would disappear exactly
        /// when it is needed.</para>
        /// </remarks>
        private void EnsureReturnButton()
        {
            if (_returnButton != null || _applyButton == null) return;

            _returnButton = Instantiate(_applyButton, transform);
            _returnButton.name = "ReturnToEditorButton";

            // The clone carries the template's wiring — we clear it before giving it its own.
            _returnButton.onClick = new Button.ButtonClickedEvent();
            _returnButton.onClick.AddListener(() => _controller?.EnterSculptMode());
            _returnButton.interactable = true;

            var rect = (RectTransform)_returnButton.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(300f, 56f);

            var label = _returnButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = _returnLabel;

            _returnButton.gameObject.SetActive(false);
        }

        private void Start()
        {
            // We read the initial state ourselves rather than waiting for the first announcement: the
            // Start() order between scene objects is undefined, so the controller could announce the
            // mode before anyone is listening.
            ApplyMode(_controller != null ? _controller.Mode : CreatureEditorMode.Sculpt);

            if (!GlobalMessagePipe.IsInitialized) return;

            _sessionSubscription = GlobalMessagePipe.GetSubscriber<CreatureEditorSessionChangedMessage>().Subscribe(OnSessionChanged);
            _modeSubscription = GlobalMessagePipe.GetSubscriber<CreatureEditorModeChangedMessage>().Subscribe(OnModeChanged);
            _selectionSubscription = GlobalMessagePipe.GetSubscriber<CreatureEditorSelectionChangedMessage>().Subscribe(OnSelectionChanged);
            _partSelectionSubscription = GlobalMessagePipe.GetSubscriber<CreatureEditorPartSelectionChangedMessage>().Subscribe(OnPartSelectionChanged);
            _commitSubscription = GlobalMessagePipe.GetSubscriber<GenomeCommitResultMessage>().Subscribe(OnCommitResult);
        }

        private void OnModeChanged(CreatureEditorModeChangedMessage message) => ApplyMode(message.Mode);

        /// <summary>Shows the sculpting machinery only while sculpting, and the way back only outside it.</summary>
        private void ApplyMode(CreatureEditorMode mode)
        {
            bool sculpting = mode == CreatureEditorMode.Sculpt;

            if (_sculptPanels != null)
            {
                foreach (GameObject panel in _sculptPanels)
                    if (panel != null) panel.SetActive(sculpting);
            }

            if (_returnButton != null) _returnButton.gameObject.SetActive(!sculpting);
        }

        private void OnDestroy()
        {
            _sessionSubscription?.Dispose();
            _modeSubscription?.Dispose();
            _selectionSubscription?.Dispose();
            _partSelectionSubscription?.Dispose();
            _commitSubscription?.Dispose();
            Unbind();
        }

        private void OnSessionChanged(CreatureEditorSessionChangedMessage message)
        {
            Unbind();
            _session = message.Session;
            if (_session == null) return;

            _session.WorkingGenomeChanged += OnWorkingGenomeChanged;
            _session.EditRejected += OnEditRejected;
            OnWorkingGenomeChanged(_session.Working);
        }

        private void Unbind()
        {
            if (_session == null) return;

            _session.WorkingGenomeChanged -= OnWorkingGenomeChanged;
            _session.EditRejected -= OnEditRejected;
            _session = null;
        }

        private void OnWorkingGenomeChanged(CreatureGenome genome)
        {
            if (_statsText == null) return;

            if (genome == null)
            {
                _statsText.text = "No creature.";
                return;
            }

            CreatureStats stats = GenomeStatRules.Derive(genome, _session.Rules, CreatureStatTuning.Default);
            BudgetReport budget = CreatureBudget.Evaluate(genome, _session.Rules);

            _statsText.text =
                $"Vertebrae: {genome.VertebraCount}   Parts: {genome.PartCount}\n" +
                $"HP: {stats.MaxHp:F0}   Mass: {stats.Mass:F1} kg\n" +
                $"Speed: {stats.MoveSpeed:F2}   Damage: {stats.AttackDamage:F0}\n" +
                $"Currency: {budget.Remaining:N0} (spent {budget.Spent:N0})";

            // The apply button is the only way out of sculpting and into the playground, so the only
            // thing gating it is whether the genome is valid — never whether the player has changed
            // anything yet. A condition on unsaved changes trapped in the editor everyone who simply
            // wanted to move the creature they were given.
            if (_applyButton != null)
                _applyButton.interactable = _session.ValidateForCommit().IsValid;

            if (_revertButton != null)
                _revertButton.interactable = _session.HasUnappliedChanges;
        }

        private void OnSelectionChanged(CreatureEditorSelectionChangedMessage message)
        {
            if (_selectionText == null) return;

            _selectionText.text = message.VertebraIndex < 0
                ? "Selection: none"
                : $"Selection: vertebra {message.VertebraIndex}";
        }

        private void OnPartSelectionChanged(CreatureEditorPartSelectionChangedMessage message)
            => SetMirrorState(message.PartIndex, message.Mirrored, message.CanMirror);

        private void SetMirrorState(int partIndex, bool mirrored, bool canMirror)
        {
            if (_mirrorButton == null) return;

            _mirrorButton.interactable = partIndex >= 0 && canMirror;

            var label = _mirrorButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;

            if (partIndex < 0) label.text = "Symmetry: —";
            else if (!canMirror) label.text = "Symmetry: no pair";
            else label.text = mirrored ? "Symmetry: pair (M)" : "Symmetry: single (M)";
        }

        private void OnEditRejected(GenomeError error) => SetStatus($"Cannot: {Describe(error)}", warning: true);

        private void OnCommitResult(GenomeCommitResultMessage message)
        {
            if (message.Accepted) SetStatus("Applied — the creature has been rebuilt for everyone.", warning: false);
            else SetStatus($"Server rejected it: {Describe(message.Error)}", warning: true);
        }

        private void SetStatus(string text, bool warning)
        {
            if (_statusText == null) return;

            _statusText.text = text;
            _statusText.color = warning ? new Color(1f, 0.6f, 0.3f) : new Color(0.6f, 1f, 0.6f);
        }

        /// <summary>Plain wording for the player — the error codes are for the logs, not for them.</summary>
        private static string Describe(GenomeError error) => error switch
        {
            GenomeError.None => "everything is fine",
            GenomeError.TooFewVertebrae => "too few vertebrae",
            GenomeError.TooManyVertebrae => "the vertebra limit has been reached",
            GenomeError.RadiusOutOfRange => "that vertebra is outside the allowed thickness",
            GenomeError.SegmentTooLong => "the vertebrae are too far apart",
            GenomeError.TooManyParts => "too many parts",
            GenomeError.InvalidAttachmentSite => "this part does not fit that vertebra",
            GenomeError.MirrorNotSupported => "this part has no mirrored version",
            GenomeError.InsufficientFunds => "not enough currency",
            GenomeError.RateLimited => "too fast — wait a moment",
            GenomeError.NotInEditorPad => "step onto the editor pad to apply",
            GenomeError.PayloadTooLarge => "the creature is too complicated",
            _ => error.ToString(),
        };
    }
}
