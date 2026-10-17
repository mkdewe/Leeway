using System;
using System.Collections.Generic;
using Leeway.MainMenu.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views
{
    /// <summary>The host form. Option lists are filled by the presenter so the view holds no game rules.</summary>
    public sealed class CreateLobbyScreenView : ScreenView
    {
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TMP_Dropdown _modeDropdown;
        [SerializeField] private TMP_Dropdown _visibilityDropdown;
        [SerializeField] private Slider _maxPlayersSlider;
        [SerializeField] private TextMeshProUGUI _maxPlayersLabel;
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _backButton;

        public event Action CreateClicked;
        public event Action BackClicked;

        public string LobbyName => _nameInput.text;
        public int ModeIndex => _modeDropdown.value;
        public int VisibilityIndex => _visibilityDropdown.value;
        public int MaxPlayers => Mathf.RoundToInt(_maxPlayersSlider.value);

        protected override void Awake()
        {
            base.Awake();
            _createButton.onClick.AddListener(() => CreateClicked?.Invoke());
            _backButton.onClick.AddListener(() => BackClicked?.Invoke());
            _nameInput.onSubmit.AddListener(_ => CreateClicked?.Invoke());
            _maxPlayersSlider.wholeNumbers = true;
            _maxPlayersSlider.onValueChanged.AddListener(v => _maxPlayersLabel.text = Mathf.RoundToInt(v).ToString());
        }

        public void SetOptions(IReadOnlyList<string> modes, IReadOnlyList<string> visibilities, int minPlayers, int maxPlayers)
        {
            Fill(_modeDropdown, modes);
            Fill(_visibilityDropdown, visibilities);
            _maxPlayersSlider.minValue = minPlayers;
            _maxPlayersSlider.maxValue = maxPlayers;
        }

        public void SetValues(string lobbyName, int modeIndex, int visibilityIndex, int maxPlayers)
        {
            _nameInput.text = lobbyName;
            _modeDropdown.SetValueWithoutNotify(modeIndex);
            _visibilityDropdown.SetValueWithoutNotify(visibilityIndex);
            _maxPlayersSlider.value = maxPlayers;
            _maxPlayersLabel.text = maxPlayers.ToString();
        }

        protected override void OnShown() => _nameInput.Select();

        private static void Fill(TMP_Dropdown dropdown, IReadOnlyList<string> options)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(0);
        }
    }
}
