using System;
using Leeway.MainMenu.Navigation;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Leeway.MainMenu.Views
{
    /// <summary>Two ways in without the browser: a lobby code (friends) or an address (LAN, dedicated servers).</summary>
    public sealed class JoinScreenView : ScreenView
    {
        [Header("Lobby code")]
        [SerializeField] private GameObject _codeSection;
        [SerializeField] private TMP_InputField _codeInput;
        [SerializeField] private Button _pasteCodeButton;
        [SerializeField] private Button _joinCodeButton;

        [Header("Direct connection")]
        [SerializeField] private TMP_InputField _addressInput;
        [SerializeField] private TMP_InputField _portInput;
        [SerializeField] private Button _connectButton;

        [SerializeField] private Button _backButton;

        public event Action JoinByCodeClicked;
        public event Action PasteCodeClicked;
        public event Action ConnectClicked;
        public event Action BackClicked;

        public string Code
        {
            get => _codeInput.text;
            set => _codeInput.text = value;
        }

        public string Address => _addressInput.text;
        public string Port => _portInput.text;

        protected override void Awake()
        {
            base.Awake();
            _joinCodeButton.onClick.AddListener(() => JoinByCodeClicked?.Invoke());
            _pasteCodeButton.onClick.AddListener(() => PasteCodeClicked?.Invoke());
            _connectButton.onClick.AddListener(() => ConnectClicked?.Invoke());
            _backButton.onClick.AddListener(() => BackClicked?.Invoke());
            _codeInput.onSubmit.AddListener(_ => JoinByCodeClicked?.Invoke());
            _addressInput.onSubmit.AddListener(_ => ConnectClicked?.Invoke());
            _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }

        public void SetCodeSectionVisible(bool visible) => _codeSection.SetActive(visible);

        public void SetDefaults(string address, ushort port)
        {
            if (string.IsNullOrEmpty(_addressInput.text)) _addressInput.text = address;
            if (string.IsNullOrEmpty(_portInput.text)) _portInput.text = port.ToString();
        }
    }
}
