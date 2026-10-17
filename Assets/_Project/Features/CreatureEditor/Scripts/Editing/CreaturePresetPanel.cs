using System.Collections.Generic;
using Leeway.Creature.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The saved creatures: a list to load from and a field to save into.
    /// </summary>
    /// <remarks>
    /// <para>The store has been able to read and write presets for a while; what was missing was any
    /// way to reach it from the game. A preset the player cannot load is a file, not a feature.</para>
    ///
    /// <para><b>The paint travels with the creature.</b> A preset carries the skin as a PNG alongside
    /// the genome, so loading one brings back the markings that were painted on it, not just its
    /// shape.</para>
    ///
    /// <para><b>Built-in creatures sit in the same list</b>, above the saved ones. They come from code
    /// rather than from disk, so a fresh installation has something to look at — and loading one drops
    /// it straight into the editor where it can be taken apart.</para>
    /// </remarks>
    public class CreaturePresetPanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreatureEditorSession _session;
        [SerializeField] private CreatureBodyPreview _preview;

        [Header("Widgets")]
        [Tooltip("Inactive template for one row in the list — cloned once per preset.")]
        [SerializeField] private Button _rowTemplate;

        [SerializeField] private Transform _rowContainer;
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Appearance")]
        [SerializeField] private Color _savedColor = new(0.78f, 0.84f, 0.90f, 1f);
        [SerializeField] private Color _builtInColor = new(0.96f, 0.85f, 0.55f, 1f);

        private readonly List<GameObject> _rows = new();

        private void Awake()
        {
            if (_saveButton != null) _saveButton.onClick.AddListener(Save);
            if (_refreshButton != null) _refreshButton.onClick.AddListener(Refresh);
        }

        private void OnEnable() => Refresh();

        /// <summary>Rebuilds the list from what is on disk, plus the creatures that ship with the game.</summary>
        public void Refresh()
        {
            foreach (GameObject row in _rows)
                if (row != null) Destroy(row);

            _rows.Clear();

            if (_rowTemplate == null || _rowContainer == null) return;

            _rowTemplate.gameObject.SetActive(false);

            AddRow("Humanoid (wbudowany)", _builtInColor, LoadHumanoid);

            foreach (string name in CreaturePresetStore.List())
            {
                string captured = name;
                AddRow(name, _savedColor, () => Load(captured));
            }
        }

        private void AddRow(string label, Color color, System.Action onClick)
        {
            Button row = Instantiate(_rowTemplate, _rowContainer);
            row.gameObject.SetActive(true);
            row.name = "Preset_" + label;

            var text = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = label;
                text.color = color;
            }

            // The clone carries the template's listeners — they go before ours.
            row.onClick = new Button.ButtonClickedEvent();
            row.onClick.AddListener(() => onClick());

            _rows.Add(row.gameObject);
        }

        /// <summary>Puts a creature from disk into the editor, paint and all.</summary>
        public void Load(string name)
        {
            if (_session == null) return;

            if (!CreaturePresetStore.TryLoadDocument(name, out GenomeDocument document, out string error))
            {
                SetStatus($"Nie udało się wczytać \"{name}\": {error}", warning: true);
                return;
            }

            CreatureGenome genome = document.ToGenome();

            GenomeValidationResult validation = GenomeValidator.Validate(genome, _session.Rules);
            if (!validation.IsValid)
            {
                // A preset made with a catalog that had parts this one does not is not a crash — it is
                // a creature this installation cannot build, and saying so is more use than a silent
                // half-creature.
                SetStatus($"\"{name}\" nie pasuje do katalogu części: {validation.Error}", warning: true);
                return;
            }

            _session.Load(genome);
            ApplySkin(CreaturePresetStore.SkinOf(document));

            if (_nameField != null) _nameField.text = name;
            SetStatus($"Wczytano \"{name}\".", warning: false);
        }

        private void LoadHumanoid()
        {
            if (_session == null || _preview == null) return;

            _session.Load(ShowcaseGenomes.Humanoid(_preview.Rules));

            if (_nameField != null) _nameField.text = "Humanoid";
            SetStatus("Wczytano wbudowanego humanoida.", warning: false);
        }

        /// <summary>Writes the creature currently being sculpted to disk, under the name in the field.</summary>
        public void Save()
        {
            if (_session?.Working == null) return;

            string name = _nameField != null ? _nameField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) name = "Stworek";

            byte[] skin = _preview != null && _preview.SkinCanvas != null ? _preview.SkinCanvas.ToPng() : null;

            if (!CreaturePresetStore.TrySave(name, _session.Working, KeyOf, skin, out string error))
            {
                SetStatus($"Nie udało się zapisać: {error}", warning: true);
                return;
            }

            SetStatus($"Zapisano \"{name}\".", warning: false);
            Refresh();
        }

        /// <summary>
        /// The catalog key for a part id, so the file names its parts instead of hashing them.
        /// </summary>
        /// <remarks>
        /// A preset with keys survives a catalog whose ids were rebuilt, and a human can read what the
        /// creature is made of without running the game.
        /// </remarks>
        private string KeyOf(int partId)
        {
            CreaturePartCatalog catalog = _preview != null ? _preview.Catalog : null;
            if (catalog == null) return null;

            return catalog.TryGetPart(partId, out CreaturePartDefinition definition) && definition != null
                ? definition.PartKey
                : null;
        }

        private void ApplySkin(byte[] png)
        {
            if (png == null || png.Length == 0) return;

            CreatureSkinCanvas canvas = _preview != null ? _preview.SkinCanvas : null;
            if (canvas == null) return;

            // The canvas is rebuilt along with the body, so the paint goes on after the rebuild has
            // finished rather than before it.
            canvas.LoadPng(png);
        }

        private void SetStatus(string message, bool warning)
        {
            if (_statusText == null)
            {
                if (warning) Debug.LogWarning(message, this);
                return;
            }

            _statusText.text = message;
            _statusText.color = warning ? new Color(0.95f, 0.6f, 0.4f) : new Color(0.85f, 0.9f, 0.85f);
        }
    }
}
