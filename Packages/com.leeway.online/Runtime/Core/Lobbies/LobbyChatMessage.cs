using System;

namespace Leeway.Online
{
    public readonly struct LobbyChatMessage
    {
        public readonly LobbyMember Sender;
        public readonly string Text;
        public readonly DateTime ReceivedAtUtc;

        public LobbyChatMessage(LobbyMember sender, string text, DateTime receivedAtUtc)
        {
            Sender = sender;
            Text = text ?? string.Empty;
            ReceivedAtUtc = receivedAtUtc;
        }
    }
}
