using System;
using System.Collections.Generic;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.Combat
{
    /// <summary>
    /// The row of what the local creature can do right now - its blows, its senses, jumping, sprinting and
    /// wings - each with its key, and a sweep and a number for the time left before it can be used again.
    /// </summary>
    /// <remarks>
    /// <para>The row is built from the body: a slot exists only for an ability the creature has the part for
    /// (see <see cref="BodyAbilityRules.Has"/>), and is rebuilt whenever the body is. Like
    /// <see cref="VitalsHud"/> it makes its own overlay canvas and only reads numbers off the creature.</para>
    /// </remarks>
    public sealed class AbilityBarHud : MonoBehaviour
    {
        [SerializeField] private Color _panel = new(0.07f, 0.1f, 0.12f, 0.62f);
        [SerializeField] private Color _ready = new(0.95f, 0.92f, 0.8f, 1f);
        [SerializeField] private Color _active = new(0.55f, 0.85f, 0.5f, 1f);
        [SerializeField] private Color _blocked = new(0.45f, 0.45f, 0.45f, 1f);

        private enum Kind { Attack, Scan, Forage, Jump, Sprint, Wings }

        private sealed class Slot
        {
            public Kind Kind;
            public CreatureAbility Ability;
            public Image Background;
            public RectTransform Sweep;
            public TextMeshProUGUI Time;
            public float Longest;
        }

        private const float SlotSize = 84f, Gap = 8f;

        private IDisposable _subscription;
        private CreatureBody _body;
        private PlaygroundCreatureController _controller;
        private CreatureCombatant _fighter;

        private RectTransform _row;
        private readonly List<Slot> _slots = new();
        private int _builtMask = -1;

        private void Awake()
        {
            var canvasObject = new GameObject("AbilityCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            _row.SetParent(canvasObject.transform, false);
            _row.anchorMin = _row.anchorMax = new Vector2(0.5f, 0f);
            _row.pivot = new Vector2(0.5f, 0f);
            _row.anchoredPosition = new Vector2(0f, 48f);
        }

        // The message bus may come up after this component's first OnEnable, so Start tries again.
        private void Start() => Subscribe();

        private void OnEnable() => Subscribe();

        private void Subscribe()
        {
            if (_subscription != null || !GlobalMessagePipe.IsInitialized) return;

            _subscription = GlobalMessagePipe.GetSubscriber<LocalCreatureBodyChangedMessage>().Subscribe(message =>
            {
                _body = message.Body;
                _controller = _body != null ? _body.GetComponent<PlaygroundCreatureController>() : null;
                _fighter = _body != null ? _body.GetComponent<CreatureCombatant>() : null;
                _builtMask = -1;
            });
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void Update()
        {
            bool visible = _body != null && _body.IsAlive && _fighter != null;
            _row.gameObject.SetActive(visible);
            if (!visible) return;

            int mask = AbilityMask();
            if (mask != _builtMask) Rebuild(mask);

            foreach (Slot slot in _slots) Refresh(slot);
        }

        // ---------------------------------------------------------------- state

        private bool Has(CreatureAbility ability) => BodyAbilityRules.Has(_body.Genome, _body.Rules, ability);

        private int AbilityMask()
        {
            int mask = 0;
            for (int i = 0; i <= (int)CreatureAbility.Forage; i++)
                if (Has((CreatureAbility)i)) mask |= 1 << i;
            if (_controller != null && _controller.CanFly) mask |= 1 << 8;
            return mask;
        }

        private void Refresh(Slot slot)
        {
            float remaining = 0f;
            float fixedLongest = 0f;
            bool blocked = _body.IsKnockedDown;
            bool blow = slot.Kind == Kind.Attack || slot.Kind == Kind.Scan || slot.Kind == Kind.Forage;
            if (blow && _body.IsInEditorPad) blocked = true;
            bool active = false;

            switch (slot.Kind)
            {
                case Kind.Attack:
                    remaining = _fighter.CooldownRemaining;
                    fixedLongest = _fighter.AttackFor(slot.Ability).Cooldown;
                    active = _fighter.IsWindingUp && _fighter.PendingAbility == slot.Ability;
                    break;
                case Kind.Scan:
                    remaining = _fighter.ScanCooldown;
                    active = _fighter.ScanActive;
                    break;
                case Kind.Forage:
                    remaining = _fighter.ForageCooldown;
                    break;
                case Kind.Jump:
                    blocked |= _controller == null || _controller.IsExhausted || _controller.Stamina < CreatureStaminaRules.JumpCost;
                    break;
                case Kind.Sprint:
                    blocked |= _controller == null || _controller.IsExhausted;
                    active = _controller != null && _controller.IsSprinting;
                    break;
                case Kind.Wings:
                    remaining = _controller.FlapCooldownRemaining;
                    fixedLongest = CreatureFlightRules.FlapCooldown;
                    blocked |= _controller.IsExhausted || _controller.Stamina < CreatureFlightRules.FlapCost;
                    active = _controller.IsGliding;
                    break;
            }

            // A timer whose length is not known up front is measured by the longest it has been seen at.
            slot.Longest = Mathf.Max(fixedLongest, slot.Longest, remaining);
            if (remaining <= 0f) slot.Longest = fixedLongest;

            float fraction = remaining > 0f && slot.Longest > 0f ? Mathf.Clamp01(remaining / slot.Longest) : 0f;
            slot.Sweep.anchorMax = new Vector2(1f, fraction);
            slot.Time.text = remaining >= 0.95f ? remaining.ToString("0") : remaining > 0.05f ? remaining.ToString("0.0") : string.Empty;
            slot.Background.color = blocked ? _blocked : active ? _active : _ready;
        }

        // ---------------------------------------------------------------- building

        private void Rebuild(int mask)
        {
            _builtMask = mask;
            foreach (Slot slot in _slots) Destroy(slot.Background.gameObject);
            _slots.Clear();

            var wanted = new List<(Kind kind, CreatureAbility ability, string key, string name)>();

            // The two blows on the mouse come first, then the numbered ones.
            CreatureAbility primary = BodyAbilityRules.Primary(_body.Genome, _body.Rules);
            wanted.Add((Kind.Attack, primary, "LPM", BodyAbilityRules.Name(primary)));
            CreatureAbility secondary = BodyAbilityRules.Secondary(_body.Genome, _body.Rules);
            if (secondary != primary) wanted.Add((Kind.Attack, secondary, "R", BodyAbilityRules.Name(secondary)));

            AddNumbered(wanted, primary, secondary, CreatureAbility.Bite, "1");
            AddNumbered(wanted, primary, secondary, CreatureAbility.Claw, "2");
            AddNumbered(wanted, primary, secondary, CreatureAbility.Horn, "3");
            AddNumbered(wanted, primary, secondary, CreatureAbility.Kick, "4");

            if (Has(CreatureAbility.Scan)) wanted.Add((Kind.Scan, CreatureAbility.Scan, "Q", "Czujność"));
            if (Has(CreatureAbility.Forage)) wanted.Add((Kind.Forage, CreatureAbility.Forage, "E", "Żerowanie"));

            wanted.Add((Kind.Jump, default, "Spacja", "Skok"));
            wanted.Add((Kind.Sprint, default, "Shift", "Sprint"));
            if (_controller != null && _controller.CanFly) wanted.Add((Kind.Wings, default, "Spacja", "Skrzydła"));

            _row.sizeDelta = new Vector2(wanted.Count * SlotSize + (wanted.Count - 1) * Gap, SlotSize);
            for (int i = 0; i < wanted.Count; i++)
                _slots.Add(MakeSlot(i, wanted[i].kind, wanted[i].ability, wanted[i].key, wanted[i].name));
        }

        // A numbered key is listed only when it does something the mouse buttons do not already do.
        private void AddNumbered(List<(Kind, CreatureAbility, string, string)> list, CreatureAbility primary, CreatureAbility secondary, CreatureAbility ability, string key)
        {
            if (!Has(ability) || ability == primary || ability == secondary) return;
            list.Add((Kind.Attack, ability, key, BodyAbilityRules.Name(ability)));
        }

        private Slot MakeSlot(int index, Kind kind, CreatureAbility ability, string key, string label)
        {
            var root = new GameObject("Slot_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_row, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(SlotSize, SlotSize);
            root.anchoredPosition = new Vector2(index * (SlotSize + Gap), 0f);

            var frame = root.gameObject.AddComponent<Image>();
            frame.color = _panel;
            frame.raycastTarget = false;

            // The face of the slot, tinted by its state.
            var face = Child(root, "Face", 4f);
            var faceImage = face.gameObject.AddComponent<Image>();
            faceImage.color = _ready;
            faceImage.raycastTarget = false;
            var faceShade = face.gameObject.AddComponent<Outline>();
            faceShade.effectColor = new Color(0f, 0f, 0f, 0.25f);

            // The dark sweep that drains as the cooldown runs out.
            var sweep = Child(face, "Sweep", 0f);
            sweep.anchorMin = Vector2.zero;
            sweep.anchorMax = new Vector2(1f, 0f);
            var sweepImage = sweep.gameObject.AddComponent<Image>();
            sweepImage.color = new Color(0.05f, 0.07f, 0.09f, 0.72f);
            sweepImage.raycastTarget = false;

            Text(face, "Key", key, 18f, TextAlignmentOptions.TopLeft, new Vector2(6f, -4f), new Vector2(-4f, -2f));
            Text(face, "Name", label, 13f, TextAlignmentOptions.Bottom, new Vector2(2f, 2f), new Vector2(-2f, 0f));
            TextMeshProUGUI time = Text(face, "Time", string.Empty, 30f, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            time.color = Color.white;

            return new Slot { Kind = kind, Ability = ability, Background = faceImage, Sweep = sweep, Time = time };
        }

        private static RectTransform Child(Transform parent, string name, float inset)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max)
        {
            RectTransform rect = Child(parent, name, 0f);
            rect.offsetMin = min;
            rect.offsetMax = max;

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.05f, 0.07f, 0.08f, 1f);
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }
    }
}
