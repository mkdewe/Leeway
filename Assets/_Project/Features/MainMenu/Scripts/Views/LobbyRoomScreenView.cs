using System;
using System.Collections.Generic;
using System.Text;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views.Widgets;
using Leeway.Network.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views
{
    /// <summary>The waiting room: members and their ready state, chat, invite, and Start for the host.</summary>
    public sealed class LobbyRoomScreenView : ScreenView
    {
        private const int MaxChatLines = 60;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _detailsLabel;
        [SerializeField] private TextMeshProUGUI _codeLabel;
        [SerializeField] private Button _copyCodeButton;

        [Header("Members")]
        [SerializeField] private RectTransform _membersContent;
        [SerializeField] private LobbyMemberItemView _memberPrefab;
        [SerializeField] private TextMeshProUGUI _membersCountLabel;

        [Header("Chat")]
        [SerializeField] private GameObject _chatPanel;
        [SerializeField] private TextMeshProUGUI _chatLog;
        [SerializeField] private ScrollRect _chatScroll;
        [SerializeField] private TMP_InputField _chatInput;
        [SerializeField] private Button _sendButton;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private TextMeshProUGUI _readyButtonLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private TextMeshProUGUI _startHintLabel;
        [SerializeField] private Button _inviteButton;
        [SerializeField] private Button _leaveButton;

        private readonly List<LobbyMemberItemView> _members = new();
        private readonly Queue<string> _chatLines = new();
        private readonly StringBuilder _chatBuilder = new();

        public event Action CopyCodeClicked;
        public event Action ReadyClicked;
        public event Action StartClicked;
        public event Action InviteClicked;
        public event Action LeaveClicked;
        public event Action<string> ChatSubmitted;

        protected override void Awake()
        {
            base.Awake();
            _copyCodeButton.onClick.AddListener(() => CopyCodeClicked?.Invoke());
            _readyButton.onClick.AddListener(() => ReadyClicked?.Invoke());
            _startButton.onClick.AddListener(() => StartClicked?.Invoke());
            _inviteButton.onClick.AddListener(() => InviteClicked?.Invoke());
            _leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());
            _sendButton.onClick.AddListener(SubmitChat);
            _chatInput.onSubmit.AddListener(_ => SubmitChat());
        }

        public void SetHeader(string title, string details, string code)
        {
            _titleLabel.text = title;
            _detailsLabel.text = details;
            _codeLabel.text = code;
        }

        public void SetMembers(IReadOnlyList<LobbyMemberSnapshot> members, int maxMembers)
        {
            while (_members.Count < members.Count) _members.Add(Instantiate(_memberPrefab, _membersContent));

            for (int i = 0; i < _members.Count; i++)
            {
                bool used = i < members.Count;
                _members[i].gameObject.SetActive(used);
                if (used) _members[i].Bind(members[i]);
            }

            _membersCountLabel.text = $"Players {members.Count}/{maxMembers}";
        }

        /// <summary>The host sees Start, guests see Ready. <paramref name="startHint"/> explains a disabled Start.</summary>
        public void SetActions(bool isHost, bool localReady, bool canStart, string startHint, bool canInvite)
        {
            _startButton.gameObject.SetActive(isHost);
            _startButton.interactable = canStart;
            _startHintLabel.gameObject.SetActive(isHost && !string.IsNullOrEmpty(startHint));
            _startHintLabel.text = startHint;

            _readyButton.gameObject.SetActive(!isHost);
            _readyButtonLabel.text = localReady ? "Not ready" : "Ready";

            _inviteButton.gameObject.SetActive(canInvite);
        }

        public void SetChatAvailable(bool available) => _chatPanel.SetActive(available);

        public void AppendChat(string line)
        {
            _chatLines.Enqueue(line);
            while (_chatLines.Count > MaxChatLines) _chatLines.Dequeue();

            _chatBuilder.Clear();
            foreach (var l in _chatLines)
            {
                if (_chatBuilder.Length > 0) _chatBuilder.Append('\n');
                _chatBuilder.Append(l);
            }
            _chatLog.text = _chatBuilder.ToString();

            // Stick to the newest line, but only after layout has caught up with the new text.
            Canvas.ForceUpdateCanvases();
            _chatScroll.verticalNormalizedPosition = 0f;
        }

        public void ClearChat()
        {
            _chatLines.Clear();
            _chatLog.text = string.Empty;
        }

        private void SubmitChat()
        {
            var text = _chatInput.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            _chatInput.text = string.Empty;
            ChatSubmitted?.Invoke(text);
            _chatInput.ActivateInputField();
        }
    }
}
