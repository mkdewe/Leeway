using System.Collections.Generic;
using Leeway.Creature.Domain;
using RootMotion.FinalIK;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// What makes a creature look alive when nothing is happening: it breathes, looks around, blinks and
    /// its mouth moves. The same component plays the attack poses - the jaw that snaps, the arms that
    /// swing, the horn that thrusts - so a blow is a movement of the body, not a flash.
    /// </summary>
    /// <remarks>
    /// <para><b>Purely visual, local, and driven by the clock.</b> Nothing here touches the network or the
    /// prediction: every machine animates its own copy of the same body. The attack poses are a pure
    /// function of the attack's synced timing (see <see cref="PoseAttack"/>), so the server's copy, which
    /// decides whether a blow connects, is where the client's copy shows it.</para>
    ///
    /// <para><b>Head and eyes follow RootMotion's LookAtIK.</b> The head bone and the spine behind it are
    /// the chain; the solver turns them toward whatever the creature is interested in. The puppet's
    /// muscles chase the animated bones, so the physical body turns its head too.</para>
    ///
    /// <para>The mouth and the eyes are made of whole-mesh part models, so the jaw is a vertex bend of a
    /// copy of the mouth mesh about a hinge at its rear, and the eyes are painted by their own shader. Nothing is
    /// added to the catalog's models.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public sealed partial class CreatureLife : MonoBehaviour
    {
        [Header("Breathing")]
        [Tooltip("Breaths per second at rest. Heavier creatures breathe slower.")]
        [SerializeField, Min(0.05f)] private float _breathRate = 0.32f;

        [Tooltip("How much the chest swells, as a fraction of its size.")]
        [SerializeField, Range(0f, 0.05f)] private float _breathDepth = 0.014f;

        [Header("Attention")]
        [Tooltip("How far another creature has to be to catch this one's eye (m).")]
        [SerializeField, Min(1f)] private float _noticeDistance = 14f;

        [Tooltip("Seconds between glances when there is nothing to look at.")]
        [SerializeField] private Vector2 _glanceInterval = new(1.8f, 4.5f);

        [Tooltip("The widest the head turns away from the way the body faces (degrees).")]
        [SerializeField, Range(10f, 80f)] private float _headYawLimit = 55f;

        [Header("Eyes")]
        [Tooltip("Seconds between blinks.")]
        [SerializeField] private Vector2 _blinkInterval = new(2.2f, 6f);

        [Tooltip("Pupil radius at rest, as a share of the iris (the painted eye in Leeway/CreatureEye).")]
        [SerializeField, Range(0.2f, 0.8f)] private float _pupilSize = 0.42f;

        [Header("Mouth")]
        [Tooltip("How far the jaw opens at full gape (degrees).")]
        [SerializeField, Range(10f, 70f)] private float _maxJawDegrees = 42f;

        private const float StrikeSeconds = 0.2f;
        private const float RecoverSeconds = 0.35f;

        private CreatureBody _body;
        private CreatureLocomotion _locomotion;
        private BuiltCreatureBody _builtFor;

        private readonly List<EyeRig> _eyes = new();
        private JawRig _jaw;
        private Transform _horn;
        private Vector3 _hornRest;
        private Transform[] _breathBones;
        private LookAtIK _lookIk;
        private Transform _lookTarget;
        private readonly List<LegRef> _arms = new();

        private Vector3 _glancePoint;
        private float _nextGlance;
        private float _nextBlink;
        private float _blink;
        private float _lidDroop;
        private float _pupilDilation = 1f;
        private float _breathPhase;
        private float _attackJaw = -1f;
        private float _headDip;
        private float _lookWeight;
        private CreatureBody _interest;
        private float _nextScan;

        private struct LegRef
        {
            public ProceduralLeg Leg;
            public int Side;
        }

        /// <summary>Where the creature is looking. Combat sets it to the target of a blow.</summary>
        public Vector3? Attention { get; set; }

        /// <summary>A body part's tip in world space - the point that hurts something when it connects.</summary>
        public struct Tip
        {
            public Vector3 Position;
            public float Radius;
        }

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            _locomotion = GetComponent<CreatureLocomotion>();
            _body.BodyRebuilt += OnBodyRebuilt;

            var target = new GameObject("LookTarget");
            target.transform.SetParent(transform, false);
            _lookTarget = target.transform;
        }

        private static readonly List<CreatureBody> Bodies = new();

        private void OnEnable()
        {
            if (_body == null) _body = GetComponent<CreatureBody>();
            if (!Bodies.Contains(_body)) Bodies.Add(_body);
            if (_body != null && _body.Built != null) Rebuild();
        }

        private void OnDisable() => Bodies.Remove(_body);

        private void OnDestroy()
        {
            Bodies.Remove(_body);
            if (_body != null) _body.BodyRebuilt -= OnBodyRebuilt;
            ReleaseRigs();
        }

        private void OnBodyRebuilt(CreatureGenome genome, CreatureStats stats) => Rebuild();

        private void ReleaseRigs()
        {
            foreach (EyeRig eye in _eyes) eye.Dispose();
            _eyes.Clear();
            _jaw?.Dispose();
            _jaw = null;
            _horn = null;
            _breathBones = null;
            _lookIk = null;
            _arms.Clear();
        }

        /// <summary>Finds the mouth, the eyes, the horn and the arms of the freshly built body and rigs them.</summary>
        private void Rebuild()
        {
            ReleaseRigs();

            BuiltCreatureBody built = _body.Built;
            CreatureGenome genome = _body.Genome;
            if (built == null || genome == null) return;

            _builtFor = built;
            PartRuleSet rules = _body.Rules;
            int hornId = StableHash.PartId("weapon.horn");

            foreach (CreaturePartInstance instance in built.PartInstances)
            {
                if (instance.Object == null || instance.GeneIndex < 0 || instance.GeneIndex >= genome.PartCount) continue;

                PartGene gene = genome.GetPart(instance.GeneIndex);
                if (!rules.TryGetRule(gene.PartId, out PartRule rule)) continue;

                if (rule.Category == PartCategory.Sense) _eyes.Add(new EyeRig(instance.Object.transform, genome, _pupilSize));
                else if (rule.Category == PartCategory.Mouth && _jaw == null) _jaw = JawRig.TryCreate(instance.Object.transform);
                else if (gene.PartId == hornId && _horn == null)
                {
                    _horn = instance.Object.transform;
                    _hornRest = _horn.localPosition;
                }
            }

            Transform[] bones = built.Bones;
            if (bones != null && bones.Length > 0)
            {
                // The swell goes in the middle of the body, where the lungs would be, not at the head or tail.
                int first = bones.Length > 2 ? 1 : 0;
                int last = bones.Length > 3 ? bones.Length - 2 : bones.Length - 1;
                var list = new List<Transform>();
                for (int i = first; i <= last; i++) if (bones[i] != null) list.Add(bones[i]);
                _breathBones = list.ToArray();

                _boneRest = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++) _boneRest[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;

                BuildLookIk(built, bones);
            }

            _breathRate = Mathf.Clamp(0.42f - 0.05f * Mathf.Log(Mathf.Max(1f, _body.Stats.Mass), 2f), 0.18f, 0.5f);
            _nextBlink = Time.time + Random.Range(_blinkInterval.x, _blinkInterval.y);
            _nextGlance = 0f;
        }

        private void BuildLookIk(BuiltCreatureBody built, Transform[] bones)
        {
            if (built.Armature == null) return;

            // Spine from the body towards the head; the head bone is the first vertebra.
            var spine = new List<Transform>();
            // Head only: pulling the spine round with it twisted the whole body while it walked.

            _lookIk = built.Armature.gameObject.AddComponent<LookAtIK>();
            bool ok = _lookIk.solver.SetChain(spine.ToArray(), bones[0], null, transform);
            if (!ok)
            {
                Destroy(_lookIk);
                _lookIk = null;
                return;
            }

            // The puppet's own mapping writes these bones; the solver must not reset them to a stored pose first.
            _lookIk.fixTransforms = true;
            _lookIk.solver.target = _lookTarget;
            _lookIk.solver.IKPositionWeight = 0f;
        }

        // --- Attack poses ---------------------------------------------------------------------------

        /// <summary>
        /// Poses the body for a blow. A pure function of time: call it every frame with the attack's
        /// synced timing and every machine shows the same movement.
        /// </summary>
        /// <param name="windup">How far through the wind-up the blow is, 0 to 1. Above 1 it has been struck.</param>
        /// <param name="sinceImpact">Seconds since the moment of impact, negative before it.</param>
        public void PoseAttack(CreatureAbility ability, float windup, float sinceImpact)
        {
            float strike = Mathf.Clamp01(sinceImpact / StrikeSeconds);
            float recover = Mathf.Clamp01((sinceImpact - StrikeSeconds) / RecoverSeconds);
            bool active = sinceImpact < StrikeSeconds + RecoverSeconds;

            _attackJaw = -1f;
            _headDip = 0f;

            if (!active)
            {
                ClearArms();
                SetHorn(0f);
                return;
            }

            // 0 = rest, 1 = fully drawn back, -1 = fully extended - one number all abilities share.
            float draw = sinceImpact < 0f ? Ease(windup) : 1f - Mathf.Min(1f, strike * 1.25f) * 2f;
            draw = Mathf.Lerp(draw, 0f, recover);

            switch (ability)
            {
                case CreatureAbility.Bite:
                    // The jaw opens wide while the creature draws breath, then snaps shut: open 0..1, shut below.
                    _attackJaw = draw > 0f ? draw : Mathf.Max(-0.12f, draw * 0.12f);
                    _headDip = draw > 0f ? -draw * 8f : -draw * 10f;
                    break;

                case CreatureAbility.Claw:
                    PoseArms(draw);
                    _headDip = draw * 4f;
                    break;

                case CreatureAbility.Horn:
                    SetHorn(draw);
                    _headDip = draw > 0f ? draw * 6f : -draw * 12f;
                    break;

                default:
                    _headDip = draw > 0f ? -draw * 5f : -draw * 8f;
                    break;
            }
        }

        /// <summary>Ends any attack pose at once - the creature was knocked down, or died.</summary>
        public void ClearAttackPose()
        {
            _attackJaw = -1f;
            _headDip = 0f;
            ClearArms();
            SetHorn(0f);
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void SetHorn(float draw)
        {
            if (_horn == null) return;

            // Drawn back, then thrust forward past where it started.
            float reach = draw > 0f ? -0.12f * draw : 0.38f * -draw;
            _horn.localPosition = _hornRest + Vector3.forward * reach;
        }

        private void PoseArms(float draw)
        {
            if (_locomotion == null) return;

            if (_arms.Count == 0) CollectArms();

            foreach (LegRef arm in _arms)
            {
                ProceduralLeg leg = arm.Leg;
                Transform hip = leg.Hip;
                if (hip == null) continue;

                Vector3 forward = transform.forward;
                Vector3 side = transform.right * arm.Side;
                float reach = leg.Spec.Reach;

                // Drawn back and up, then swung across the front in an arc.
                Vector3 back = hip.position - forward * reach * 0.35f + Vector3.up * reach * 0.55f + side * reach * 0.45f;
                Vector3 front = hip.position + forward * reach * 0.95f + Vector3.up * reach * 0.1f - side * reach * 0.25f;

                Vector3 target = draw > 0f
                    ? Vector3.Lerp(hip.position + forward * reach * 0.3f - Vector3.up * reach * 0.4f, back, draw)
                    : Vector3.Lerp(front, hip.position + forward * reach * 0.3f - Vector3.up * reach * 0.4f, 1f + draw);

                leg.SetStrike(target, 1f);
            }
        }

        private void CollectArms()
        {
            _arms.Clear();
            CreatureGenome genome = _body.Genome;
            PartRuleSet rules = _body.Rules;
            if (genome == null) return;

            foreach (ProceduralLeg leg in _locomotion.Legs)
            {
                if (leg.GeneIndex < 0 || leg.GeneIndex >= genome.PartCount) continue;
                if (!rules.TryGetRule(genome.GetPart(leg.GeneIndex).PartId, out PartRule rule) || rule.Category != PartCategory.Grasper) continue;

                float lateral = Vector3.Dot(leg.HipPosition - transform.position, transform.right);
                _arms.Add(new LegRef { Leg = leg, Side = lateral >= 0f ? 1 : -1 });
            }
        }

        private void ClearArms()
        {
            foreach (LegRef arm in _arms) arm.Leg.SetStrike(Vector3.zero, 0f);
        }

        /// <summary>
        /// The points of this body that deliver a blow of the given kind, in world space. Empty when
        /// the body has nothing to strike with - the blow is then a plain shove of the whole creature.
        /// </summary>
        public void GetTips(CreatureAbility ability, List<Tip> tips)
        {
            tips.Clear();
            float scale = Mathf.Max(0.3f, transform.lossyScale.x);

            switch (ability)
            {
                case CreatureAbility.Bite:
                    if (_jaw != null) tips.Add(new Tip { Position = _jaw.Mouth(), Radius = 0.2f });
                    break;

                case CreatureAbility.Claw:
                    if (_arms.Count == 0) CollectArms();
                    foreach (LegRef arm in _arms) tips.Add(new Tip { Position = arm.Leg.FootPosition, Radius = 0.22f * scale });
                    break;

                case CreatureAbility.Horn:
                    if (_horn != null) tips.Add(new Tip { Position = _horn.position + _horn.forward * 0.25f, Radius = 0.22f });
                    break;
            }
        }

        // --- Life -----------------------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_builtFor == null || _builtFor != _body.Built)
            {
                if (_body.Built != null) Rebuild();
                return;
            }

            float dt = Time.deltaTime;
            bool alive = _body.IsAlive;
            bool down = _body.IsKnockedDown;

            UpdateActivities(dt, alive, down);
            UpdateBodyMotion(dt, alive, down);
            UpdateBreathing(dt, alive);
            UpdateLook(dt, alive && !down);
            UpdateEyes(dt, alive);
            UpdateJaw(dt, alive);

            if (!alive || down) ClearAttackPose();

            _locomotion?.SetBodyPose(down || !alive ? Vector3.zero : _poseOut, _offsetOut);
        }

        private void UpdateBreathing(float dt, bool alive)
        {
            if (_breathBones == null) return;

            // Faster when it is working hard.
            float effort = 1f;
            if (TryGetComponent(out PlaygroundCreatureController controller) && controller.IsSprinting) effort = 2.2f;
            if (_body.IsKnockedDown) effort = Mathf.Max(effort, 1.6f);

            _breathPhase += dt * _breathRate * effort * Mathf.PI * 2f;
            float swell = alive ? Mathf.Sin(_breathPhase) * _breathDepth * _actBreath : 0f;

            for (int i = 0; i < _breathBones.Length; i++)
            {
                if (_breathBones[i] == null) continue;

                // Lungs sit in the middle: the swell fades towards both ends of the run of bones.
                float u = _breathBones.Length > 1 ? i / (float)(_breathBones.Length - 1) : 0.5f;
                float weight = Mathf.Sin(u * Mathf.PI);
                if (_breathBones.Length == 1) weight = 1f;

                float s = 1f + swell * Mathf.Max(0.35f, weight) + _squash * 0.5f * weight;
                _breathBones[i].localScale = new Vector3(s, s, 1f + swell * 0.3f - _squash * 0.35f * weight);
            }
        }

        private void UpdateLook(float dt, bool enabled)
        {
            if (_lookIk == null) return;

            Vector3 point = ChooseLookPoint(dt, out float strength);
            _lookTarget.position = Vector3.Lerp(_lookTarget.position, point, 1f - Mathf.Exp(-8f * dt));

            // A dip of the head for the attack poses rides on top of the look.
            _lookTarget.position += Vector3.down * (_headDip * 0.05f);

            // Less of it on the move: a head swinging round while the creature walks looks like a twist of the body.
            float moving = _locomotion != null && _locomotion.IsMoving ? 0.35f : 1f;
            _lookWeight = Mathf.MoveTowards(_lookWeight, enabled ? strength * moving : 0f, dt * 2.5f);
            _lookIk.solver.SetLookAtWeight(_lookWeight * 0.6f, 0f, 1f, 0f, 0.6f, 0.6f, 0.3f);
        }

        private Vector3 ChooseLookPoint(float dt, out float strength)
        {
            Vector3 eyes = transform.position + Vector3.up * 0.6f;

            if (Attention.HasValue)
            {
                strength = 1f;
                return Attention.Value;
            }

            if (_actLook.HasValue)
            {
                strength = 1f;
                return _actLook.Value;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.6f + Random.value * 0.4f;
                _interest = NearestOther();
            }

            if (_interest != null && _interest.IsAlive)
            {
                strength = 0.85f;
                return _interest.transform.position + Vector3.up * 0.5f;
            }

            if (Time.time >= _nextGlance)
            {
                _nextGlance = Time.time + Random.Range(_glanceInterval.x, _glanceInterval.y);
                float yaw = Random.Range(-_headYawLimit, _headYawLimit) * 0.7f;
                float pitch = Random.Range(-8f, 12f);
                Vector3 direction = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                _glancePoint = eyes + transform.TransformDirection(direction) * 8f;
            }

            strength = 0.55f;
            return _glancePoint == Vector3.zero ? eyes + transform.forward * 8f : _glancePoint;
        }

        private CreatureBody NearestOther()
        {
            CreatureBody best = null;
            float bestDistance = _noticeDistance * _noticeDistance;

            foreach (CreatureBody other in Bodies)
            {
                if (other == _body || !other.IsAlive) continue;

                Vector3 offset = other.transform.position - transform.position;
                float distance = offset.sqrMagnitude;
                if (distance >= bestDistance) continue;
                if (Vector3.Dot(offset.normalized, transform.forward) < -0.25f) continue;

                bestDistance = distance;
                best = other;
            }

            return best;
        }

        private void UpdateEyes(float dt, bool alive)
        {
            if (_eyes.Count == 0) return;

            if (alive && Time.time >= _nextBlink)
            {
                _blink = 1f;
                _nextBlink = Time.time + Random.Range(_blinkInterval.x, _blinkInterval.y);
            }

            _blink = Mathf.MoveTowards(_blink, 0f, dt * 9f);
            float closed = alive ? Mathf.Sin(_blink * Mathf.PI) : 1f; // a dead creature's eyes are shut

            Vector3 gaze = _lookTarget != null ? _lookTarget.position : transform.position + transform.forward * 8f;
            float lid = Mathf.Max(closed, _lidDroop);
            foreach (EyeRig eye in _eyes) eye.Update(gaze, lid, _pupilDilation, dt);
        }

        private void UpdateJaw(float dt, bool alive)
        {
            if (_jaw == null) return;

            float open;
            if (_attackJaw > -1f) open = _attackJaw;
            else
            {
                // At rest the mouth moves a little with the breath, and parts now and then.
                float slack = 0.05f + Mathf.Max(0f, Mathf.Sin(_breathPhase - 0.6f)) * 0.07f;
                open = alive ? slack + _actJaw : 0.03f;
            }

            if (!float.IsNaN(open)) _jaw.Set(open * _maxJawDegrees);
        }

        // --- Rigs -----------------------------------------------------------------------------------

        /// <summary>
        /// An eye. The eyeball's material (<c>Leeway/CreatureEye</c>) paints the sclera, iris, pupil, highlight
        /// and lid itself from the direction a pixel lies in, so looking around is a number handed to the
        /// shader - nothing is added to the model. The model is a short cylinder; its flat face is found
        /// from the mesh (the thinnest axis) and the face is the side away from the body.
        /// </summary>
        private sealed class EyeRig
        {
            private static readonly int CenterId = Shader.PropertyToID("_EyeCenter");
            private static readonly int GazeId = Shader.PropertyToID("_EyeGaze");
            private static readonly int LidId = Shader.PropertyToID("_EyeLid");
            private static readonly int IrisId = Shader.PropertyToID("_EyeIrisColor");
            private static readonly int LidColorId = Shader.PropertyToID("_EyeLidColor");
            private static readonly int PupilId = Shader.PropertyToID("_EyePupilSize");

            private readonly Renderer _ball;
            private readonly MaterialPropertyBlock _block = new();
            private readonly Vector3 _centre;
            private readonly Vector3 _forward;
            private readonly Color _iris;
            private readonly Color _lidColor;
            private readonly float _pupil;
            private Vector3 _gaze;

            public EyeRig(Transform part, CreatureGenome genome, float pupil)
            {
                MeshRenderer ball = part.GetComponentInChildren<MeshRenderer>();
                MeshFilter filter = ball != null ? ball.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) return;

                // Without the eye shader there is nothing to paint with: the plain model stays.
                if (ball.sharedMaterial == null || ball.sharedMaterial.shader == null || ball.sharedMaterial.shader.name != "Leeway/CreatureEye") return;

                _ball = ball;
                _pupil = pupil;

                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 half = Vector3.Scale(bounds.extents, ball.transform.lossyScale);
                int thin = half.x <= half.y && half.x <= half.z ? 0 : half.y <= half.z ? 1 : 2;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

                _centre = bounds.center;
                Vector3 n = axes[thin];

                // The face looks away from the body.
                Vector3 towardsBody = ball.transform.InverseTransformPoint(part.parent != null ? part.parent.position : part.position) - _centre;
                if (Vector3.Dot(n, towardsBody) > 0f) n = -n;
                _forward = n;
                _gaze = n;

                // A hue the creature's own colours suggest, so each one looks out of eyes of its own.
                Color.RGBToHSV(genome != null ? (Color)genome.PrimaryColor : Color.gray, out float hue, out _, out _);
                _iris = Color.HSVToRGB(Mathf.Repeat(hue + 0.42f, 1f), 0.62f, 0.9f);
                Color skin = genome != null ? (Color)genome.PrimaryColor : new Color(0.8f, 0.5f, 0.45f);
                _lidColor = Color.Lerp(skin, Color.black, 0.08f);
            }

            public void Update(Vector3 gazePoint, float lid, float dilation, float dt)
            {
                if (_ball == null) return;

                Vector3 toPoint = _ball.transform.InverseTransformPoint(gazePoint) - _centre;
                Vector3 want = toPoint.sqrMagnitude > 1e-8f ? toPoint.normalized : _forward;

                // The eye cannot turn all the way round in its socket.
                float angle = Vector3.Angle(_forward, want);
                const float limit = 38f;
                if (angle > limit) want = Vector3.Slerp(_forward, want, limit / angle);

                _gaze = Vector3.Slerp(_gaze, want, 1f - Mathf.Exp(-16f * dt)).normalized;

                _ball.GetPropertyBlock(_block);
                _block.SetVector(CenterId, _centre);
                _block.SetVector(GazeId, new Vector4(_gaze.x, _gaze.y, _gaze.z, dilation));
                _block.SetFloat(LidId, lid);
                _block.SetColor(IrisId, _iris);
                _block.SetColor(LidColorId, _lidColor);
                _block.SetFloat(PupilId, _pupil * 0.7f);
                _ball.SetPropertyBlock(_block);
            }

            public void Dispose() { }
        }

        /// <summary>
        /// A jaw: a copy of the mouth mesh whose lower half is bent about a hinge at the rear. The mouth
        /// models are single meshes, so this is how they open without being re-authored.
        /// </summary>
        private sealed class JawRig
        {
            private readonly MeshFilter _filter;
            private readonly Mesh _mesh;
            private readonly Vector3[] _rest;
            private readonly Vector3[] _bent;
            private readonly float[] _weights;
            private readonly Vector3 _hinge;
            private readonly Vector3 _tip;
            private float _angle = float.NaN;

            private JawRig(MeshFilter filter, Mesh source)
            {
                _filter = filter;
                _mesh = UnityEngine.Object.Instantiate(source);
                _mesh.name = source.name + " (jaw)";
                _filter.sharedMesh = _mesh;

                _rest = source.vertices;
                _bent = new Vector3[_rest.Length];
                _weights = new float[_rest.Length];

                Bounds b = source.bounds;
                _hinge = new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * 0.18f);
                _tip = new Vector3(b.center.x, b.center.y, b.max.z);

                // The lower half moves; the join between the halves is blended so it does not tear.
                float soft = b.size.y * 0.12f;
                for (int i = 0; i < _rest.Length; i++)
                {
                    float below = (b.center.y - _rest[i].y) / soft;
                    float behind = (_rest[i].z - _hinge.z) / (b.size.z * 0.2f);
                    _weights[i] = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(below * 0.5f + 0.5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(behind));
                }
            }

            public static JawRig TryCreate(Transform part)
            {
                MeshFilter filter = part.GetComponentInChildren<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return null;

                return new JawRig(filter, filter.sharedMesh);
            }

            /// <summary>The front of the mouth in world space.</summary>
            public Vector3 Mouth() => _filter != null ? _filter.transform.TransformPoint(_tip) : Vector3.zero;

            public void Set(float degrees)
            {
                if (_mesh == null || Mathf.Abs(degrees - _angle) < 0.6f) return;
                _angle = degrees;

                Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.right);
                for (int i = 0; i < _rest.Length; i++)
                {
                    float w = _weights[i];
                    Vector3 turned = _hinge + rotation * (_rest[i] - _hinge);
                    _bent[i] = Vector3.Lerp(_rest[i], turned, w);
                }

                _mesh.vertices = _bent;
                _mesh.RecalculateBounds();
            }

            public void Dispose()
            {
                if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
            }
        }
    }
}
