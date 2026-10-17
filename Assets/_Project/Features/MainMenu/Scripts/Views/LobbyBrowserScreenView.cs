using System;
using System.Collections.Generic;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views.Widgets;
using Leeway.Network.Session;
using Leeway.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views
{
    /// <summary>Public lobby list with filters. Rows are pooled prefabs; the presenter decides what goes in them.</summary>
    public sealed class LobbyBrowserScreenView : ScreenView
    {
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private TMP_Dropdown _modeDropdown;
        [SerializeField] private Toggle _freeSlotsToggle;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private RectTransform _listContent;
        [SerializeField] private LobbyListItemView _itemPrefab;
        [SerializeField] private TextMeshProUGUI _statusLabel;

        private readonly List<LobbyListItemView> _items = new();

        public event Action RefreshClicked;
        public event Action BackClicked;
        public event Action<LobbyId> JoinClicked;

        /// <summary>Raised when the player edits a filter — the presenter decides whether to re-query.</summary>
        public event Action FiltersChanged;

        public string SearchText => _searchInput.text;
        public int ModeIndex => _modeDropdown.value;
        public bool OnlyWithFreeSlots => _freeSlotsToggle.isOn;

        protected override void Awake()
        {
            base.Awake();
            _refreshButton.onClick.AddListener(() => RefreshClicked?.Invoke());
            _backButton.onClick.AddListener(() => BackClicked?.Invoke());
            _searchInput.onSubmit.AddListener(_ => RefreshClicked?.Invoke());
            _modeDropdown.onValueChanged.AddListener(_ => FiltersChanged?.Invoke());
            _freeSlotsToggle.onValueChanged.AddListener(_ => FiltersChanged?.Invoke());
        }

        public event Action Shown;
        protected override void OnShown() => Shown?.Invoke();

        public void SetModeOptions(IReadOnlyList<string> options)
        {
            _modeDropdown.ClearOptions();
            _modeDropdown.AddOptions(new List<string>(options));
            _modeDropdown.SetValueWithoutNotify(0);
        }

        public void SetSearching(bool searching)
        {
            _refreshButton.interactable = !searching;
            if (searching) SetStatus("Searching…");
        }

        public void SetStatus(string text)
        {
            _statusLabel.text = text;
            _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void ShowLobbies(IReadOnlyList<LobbyListing> lobbies, Func<GameMode, string> modeName)
        {
            while (_items.Count < lobbies.Count)
            {
                var item = Instantiate(_itemPrefab, _listContent);
                item.JoinClicked += id => JoinClicked?.Invoke(id);
                _items.Add(item);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                bool used = i < lobbies.Count;
                _items[i].gameObject.SetActive(used);
                if (used) _items[i].Bind(lobbies[i], modeName(lobbies[i].Mode));
            }

            SetStatus(lobbies.Count == 0 ? "No lobbies found. Create one, or refresh in a moment." : string.Empty);
        }
    }
}
