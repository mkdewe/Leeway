using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Carries a creature's painted skin to the other players.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is not in the genome.</b> Everything else about a creature travels as a few
    /// hundred bytes that every client rebuilds from. A painted skin is a texture — tens of kilobytes
    /// — and cannot be derived from anything. So it travels on its own channel, on the same occasion:
    /// the player commits, and the picture follows the genome.</para>
    ///
    /// <para><b>Sent in chunks.</b> A PNG does not fit in a packet, so it goes out in pieces and is
    /// reassembled on arrival. A creature is shown in its old skin until the last piece lands —
    /// visibly late paint is a far smaller problem than a stalled connection.</para>
    ///
    /// <para><b>The server keeps the bytes</b> so a player who joins later, or who sees the creature
    /// for the first time, gets the skin too. It does not look inside them: the picture is decoration,
    /// and the only thing worth guarding is the size.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public class CreatureSkinNetwork : NetworkBehaviour
    {
        /// <summary>How much of the PNG goes into one message. Comfortably inside a reliable packet.</summary>
        private const int ChunkBytes = 512;

        /// <summary>
        /// The most a skin may weigh.
        /// </summary>
        /// <remarks>
        /// A 512² PNG of a painted creature runs to a few tens of kilobytes. The cap is what stops a
        /// doctored client from sending a megabyte and making every other player pay for it — the same
        /// reasoning as the genome's blob limit, at a scale that suits a picture.
        /// </remarks>
        private const int MaxSkinBytes = 256 * 1024;

        [SerializeField] private CreatureSkinCanvas _canvas;

        /// <summary>The skin as the server knows it — what late joiners are sent.</summary>
        private byte[] _skin;

        private readonly List<byte> _incoming = new();
        private int _expectedChunks;
        private int _receivedChunks;

        private CreatureBody _body;

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            if (_canvas == null) _canvas = GetComponent<CreatureSkinCanvas>();
        }

        private void OnEnable()
        {
            if (_body != null) _body.BodyRebuilt += OnBodyRebuilt;
        }

        private void OnDisable()
        {
            if (_body != null) _body.BodyRebuilt -= OnBodyRebuilt;
        }

        /// <summary>
        /// Puts the last known skin back on after a rebuild.
        /// </summary>
        /// <remarks>
        /// A rebuild makes a new renderer and a new texture, seeded from the genome's base coat. On the
        /// player's own machine the canvas carries its paint across by itself; on everyone else's, the
        /// paint only exists as the bytes that arrived over the network — so they are laid on again
        /// here.
        /// </remarks>
        private void OnBodyRebuilt(Leeway.Creature.Domain.CreatureGenome genome, Leeway.Creature.Domain.CreatureStats stats)
        {
            if (_skin != null) Apply(_skin);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Not for the owner: their skin is the one on their own canvas, and asking for it back
            // would overwrite what they are in the middle of painting.
            if (!IsOwner) CmdRequestSkin();
        }

        /// <summary>
        /// Sends the creature's current skin to everyone. Called by the owner when the genome is committed.
        /// </summary>
        public void Publish()
        {
            if (!IsOwner || _canvas == null) return;

            byte[] png = _canvas.ToBundle();
            if (png == null || png.Length == 0) return;

            if (png.Length > MaxSkinBytes)
            {
                Debug.LogWarning($"The painted skin weighs {png.Length / 1024} kB, over the {MaxSkinBytes / 1024} kB limit — not sending it.", this);
                return;
            }

            int chunks = Mathf.CeilToInt(png.Length / (float)ChunkBytes);

            for (int i = 0; i < chunks; i++)
            {
                int offset = i * ChunkBytes;
                int size = Mathf.Min(ChunkBytes, png.Length - offset);
                var chunk = new byte[size];
                System.Array.Copy(png, offset, chunk, 0, size);

                CmdSkinChunk(i, chunks, chunk);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdSkinChunk(int index, int total, byte[] chunk, Channel channel = Channel.Reliable)
        {
            if (!Accumulate(index, total, chunk)) return;

            _skin = _incoming.ToArray();
            Apply(_skin);

            RpcSkinComplete(_skin);
        }

        /// <summary>
        /// The server hands the finished picture on in one piece.
        /// </summary>
        /// <remarks>
        /// Whole rather than chunk by chunk, because by now the bytes are known-good and under the cap —
        /// and because a client that missed a chunk would otherwise be left holding half a skin with no
        /// way to ask for the rest.
        /// </remarks>
        [ObserversRpc(BufferLast = true)]
        private void RpcSkinComplete(byte[] png, Channel channel = Channel.Reliable)
        {
            if (IsOwner) return;

            _skin = png;
            Apply(png);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdRequestSkin(NetworkConnection sender = null)
        {
            if (_skin == null || sender == null) return;

            TargetSkin(sender, _skin);
        }

        [TargetRpc]
        private void TargetSkin(NetworkConnection connection, byte[] png)
        {
            _skin = png;
            Apply(png);
        }

        /// <summary>
        /// Collects a chunk, and reports whether that was the last one.
        /// </summary>
        /// <remarks>
        /// Chunks are expected in order, because they go out in order on a reliable channel. Anything
        /// else — a stray index, a changed total, a skin that has grown past the cap mid-transfer — ends
        /// the transfer rather than being pieced together: the sender is the only one who can start a
        /// new one, and a half-built picture is worth nothing.
        /// </remarks>
        private bool Accumulate(int index, int total, byte[] chunk)
        {
            if (chunk == null || total <= 0) return false;

            if (index == 0)
            {
                _incoming.Clear();
                _expectedChunks = total;
                _receivedChunks = 0;
            }
            else if (total != _expectedChunks || index != _receivedChunks)
            {
                _incoming.Clear();
                _expectedChunks = 0;
                _receivedChunks = 0;
                return false;
            }

            if (_incoming.Count + chunk.Length > MaxSkinBytes)
            {
                _incoming.Clear();
                _expectedChunks = 0;
                _receivedChunks = 0;
                return false;
            }

            _incoming.AddRange(chunk);
            _receivedChunks++;

            return _receivedChunks == _expectedChunks;
        }

        private void Apply(byte[] png)
        {
            if (_canvas == null || png == null || png.Length == 0) return;

            _canvas.LoadBundle(png);
        }
    }
}
