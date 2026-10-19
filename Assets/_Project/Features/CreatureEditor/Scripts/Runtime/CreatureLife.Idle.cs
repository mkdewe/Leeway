using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The things a creature does when nothing is asked of it - and the small movements of the body under
    /// everything it does: the spine that sways, the bob of each footfall, the squash of a landing.
    /// </summary>
    /// <remarks>
    /// <para>None of this is a canned clip. Every activity is a handful of curves over a few seconds -
    /// how far the jaw is open, how heavy the lids are, where the head points, how the body is posed - and
    /// the same curves play on a creature with any build, because they act on the shared parts of every
    /// body: the spine bones, the head, the eyes and the mouth.</para>
    ///
    /// <para>The spine motion is written into the <b>animated</b> bones, which the PuppetMaster body then
    /// follows, so the physical creature really does sway. Nothing here is replicated: each machine plays
    /// its own copy, and the server's body only needs the poses for its blows.</para>
    /// </remarks>
    public sealed partial class CreatureLife
    {
        private enum Activity { None, Sniff, Shake, Yawn, Stretch, Curious, Wiggle }

        [Header("Idle life")]
        [Tooltip("Seconds of standing still between one idle activity and the next.")]
        [SerializeField] private Vector2 _activityInterval = new(3.5f, 8f);

        [Tooltip("How much the spine sways while walking, in degrees at the tail.")]
        [SerializeField, Range(0f, 15f)] private float _walkSwayDegrees = 6f;

        [Tooltip("How much the spine sways at rest.")]
        [SerializeField, Range(0f, 6f)] private float _idleSwayDegrees = 1.4f;

        [Tooltip("How much each footfall bounces the body (metres).")]
        [SerializeField, Range(0f, 0.08f)] private float _footfallBob = 0.06f;

        // What the current activity asks for. Reset every frame and filled in by the activity.
        private float _actJaw;
        private float _actBreath = 1f;
        private float _swayBoost;
        private Vector3 _actPose;
        private Vector3? _actLook;

        private Activity _activity;
        private float _activityTime;
        private float _activityLength;
        private float _nextActivityAt;
        private float _activitySide = 1f;

        private Quaternion[] _boneRest;
        private float _swayPhase;

        // Springs: the bounce of footfalls and the squash and stretch of take-off and landing.
        private float _bob, _bobVelocity;
        private float _squash, _squashVelocity;
        private bool[] _legWasStepping;
        private bool _wasAirborne;
        private float _lastVerticalSpeed;
        private Vector3 _lastPosition;
        private Vector3 _poseOut;
        private Vector3 _offsetOut;
        private float _speed;

        private void UpdateActivities(float dt, bool alive, bool down)
        {
            // What it asks for is zero unless an activity says otherwise.
            _actJaw = 0f;
            _actBreath = 1f;
            _swayBoost = 0f;
            _actLook = null;
            Vector3 pose = Vector3.zero;
            float lid = 0f;
            float pupil = 1f;

            bool calm = alive && !down && !_locomotion.IsMoving && !_locomotion.IsAirborne
                && _attackJaw <= -1f && !Attention.HasValue;

            if (_activity != Activity.None && (!calm || _activityTime >= _activityLength)) EndActivity();

            if (_activity == Activity.None && calm && Time.time >= _nextActivityAt) StartActivity();

            if (_activity != Activity.None)
            {
                _activityTime += dt;
                float t = _activityTime;
                float u = Mathf.Clamp01(t / _activityLength);
                float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.7f);

                switch (_activity)
                {
                    case Activity.Sniff:
                        // Nose to the ground a little ahead, the mouth working, the pupils wide.
                        _actLook = transform.position + transform.forward * 0.9f + _body.Up * 0.05f;
                        _actJaw = 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 19f)) * envelope;
                        pupil = 1f + 0.18f * envelope;
                        pose.x = 7f * envelope;
                        break;

                    case Activity.Shake:
                        // A quick shudder along the whole body, loosest at the tail.
                        pose.z = 11f * Mathf.Sin(t * Mathf.PI * 2f * 9f) * envelope;
                        pose.y = 6f * Mathf.Sin(t * Mathf.PI * 2f * 7.3f) * envelope;
                        _swayBoost = 5f * envelope;
                        lid = 0.4f * envelope;
                        break;

                    case Activity.Yawn:
                        // Jaw wide, eyes screwed shut, a big breath in, head lifted.
                        float gape = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 3f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.7f) * 3.3f)));
                        _actJaw = gape;
                        lid = 0.9f * gape;
                        _actBreath = 1f + 3f * gape;
                        pose.x = -6f * gape;
                        _actLook = transform.position + transform.forward * 4f + _body.Up * 2.2f;
                        break;

                    case Activity.Stretch:
                        // Front down, hindquarters up, a long exhale.
                        pose.x = 13f * envelope;
                        _actJaw = 0.35f * envelope;
                        lid = 0.35f * envelope;
                        _actBreath = 1f + 1.5f * envelope;
                        _actLook = transform.position + transform.forward * 1.2f + -_body.Up * 0.1f;
                        break;

                    case Activity.Curious:
                        // The head cocked at something that is not there.
                        _actLook = transform.position + transform.forward * 3f + transform.right * (_activitySide * 2.6f) + _body.Up * 0.9f;
                        pupil = 1f + 0.3f * envelope;
                        pose.z = 6f * _activitySide * envelope;
                        break;

                    case Activity.Wiggle:
                        // Pleased with itself: the tail goes, the body bobs.
                        _swayBoost = 7f * envelope;
                        pose.y = 5f * Mathf.Sin(t * Mathf.PI * 2f * 3f) * envelope;
                        _actJaw = 0.12f * envelope;
                        pupil = 1f + 0.12f * envelope;
                        if (Mathf.Repeat(t, 0.33f) < dt) _bobVelocity -= 0.6f * envelope;
                        break;
                }
            }

            // Hurt creatures look it: heavy lids, and slow, wide eyes.
            if (alive && _body.MaxHp > 0f)
            {
                float hurt = Mathf.Clamp01(1f - _body.Hp / _body.MaxHp);
                lid = Mathf.Max(lid, Mathf.Clamp01((hurt - 0.4f) * 0.9f));
            }

            _lidDroop = Mathf.Lerp(_lidDroop, lid, 1f - Mathf.Exp(-9f * dt));
            _pupilDilation = Mathf.Lerp(_pupilDilation, pupil, 1f - Mathf.Exp(-6f * dt));
            _poseOut = Vector3.Lerp(_poseOut, pose, 1f - Mathf.Exp(-14f * dt));
        }

        private void StartActivity()
        {
            // Weighted: the small things are common, the big ones rare.
            float roll = Random.value;
            _activity = roll < 0.28f ? Activity.Sniff
                : roll < 0.45f ? Activity.Curious
                : roll < 0.62f ? Activity.Wiggle
                : roll < 0.78f ? Activity.Stretch
                : roll < 0.9f ? Activity.Yawn
                : Activity.Shake;

            _activityLength = _activity switch
            {
                Activity.Sniff => Random.Range(1.4f, 2.2f),
                Activity.Curious => Random.Range(1.6f, 2.6f),
                Activity.Wiggle => Random.Range(1.2f, 1.8f),
                Activity.Stretch => Random.Range(1.8f, 2.6f),
                Activity.Yawn => Random.Range(1.8f, 2.4f),
                _ => Random.Range(0.8f, 1.1f),
            };

            _activityTime = 0f;
            _activitySide = Random.value < 0.5f ? -1f : 1f;
        }

        private void EndActivity()
        {
            _activity = Activity.None;
            _nextActivityAt = Time.time + Random.Range(_activityInterval.x, _activityInterval.y);
        }

        /// <summary>The body's secondary motion: sway along the spine, footfall bob, squash and stretch.</summary>
        private void UpdateBodyMotion(float dt, bool alive, bool down)
        {
            if (dt <= 0f) return;

            // The springs below are stepped once per frame: a long frame must not blow them up.
            float rawDt = dt;
            dt = Mathf.Min(dt, 1f / 30f);

            Vector3 position = transform.position;
            Vector3 velocity = (position - _lastPosition) / rawDt;
            _lastPosition = position;
            _speed = Mathf.Lerp(_speed, _body.Surface.Tangent(velocity).magnitude, 1f - Mathf.Exp(-8f * dt));

            if (down || !alive || _locomotion == null)
            {
                _offsetOut = Vector3.zero;
                return;
            }

            // Footfalls: each landing knocks the body down a touch and it springs back.
            var legs = _locomotion.Legs;
            if (_legWasStepping == null || _legWasStepping.Length != legs.Count) _legWasStepping = new bool[legs.Count];

            for (int i = 0; i < legs.Count; i++)
            {
                bool stepping = legs[i].IsStepping;
                if (_legWasStepping[i] && !stepping) _bobVelocity -= 1.3f * Mathf.Clamp01(_speed / 3f);
                _legWasStepping[i] = stepping;
            }

            // Take-off stretches the body, landing squashes it; the harder the landing the deeper.
            bool airborne = _locomotion.IsAirborne;
            if (!_wasAirborne && airborne) _squashVelocity += 0.9f;
            if (_wasAirborne && !airborne) _squashVelocity -= Mathf.Clamp(Mathf.Abs(_lastVerticalSpeed) * 0.35f, 0.3f, 2.2f);
            _wasAirborne = airborne;
            _lastVerticalSpeed = Vector3.Dot(velocity, _body.Up);

            _bobVelocity += (-220f * _bob - 16f * _bobVelocity) * dt;
            _bob += _bobVelocity * dt;
            _squashVelocity += (-170f * _squash - 11f * _squashVelocity) * dt;
            _squash = Mathf.Clamp(_squash + _squashVelocity * dt, -0.35f, 0.3f);
            if (float.IsNaN(_squash) || float.IsNaN(_bob)) { _squash = 0f; _squashVelocity = 0f; _bob = 0f; _bobVelocity = 0f; }

            _offsetOut = new Vector3(0f, _bob * _footfallBob * 16f, 0f);

            ApplySpineSway(dt);
        }

        /// <summary>
        /// A wave along the spine, growing towards the tail. Walking sets its size and speed; the tail
        /// goes on moving a little even when the creature is standing.
        /// </summary>
        private void ApplySpineSway(float dt)
        {
            Transform[] bones = _body.Built?.Bones;
            if (bones == null || _boneRest == null || _boneRest.Length != bones.Length || bones.Length < 2) return;

            float walk = Mathf.Clamp01(_speed / 3f);
            float amplitude = _idleSwayDegrees + _walkSwayDegrees * walk + _swayBoost;
            float frequency = 0.45f + _speed * 0.55f + _swayBoost * 0.45f;
            _swayPhase += dt * frequency * Mathf.PI * 2f;

            int count = bones.Length;
            for (int i = 1; i < count; i++)
            {
                if (bones[i] == null) continue;

                float along = i / (float)(count - 1);
                float lag = _swayPhase - i * 0.85f;
                float yaw = amplitude * Mathf.Sin(lag) * (0.3f + 0.7f * along);
                float roll = amplitude * 0.35f * Mathf.Sin(lag + 1.1f) * along;

                bones[i].localRotation = _boneRest[i] * Quaternion.Euler(0f, yaw, roll);
            }
        }
    }
}
