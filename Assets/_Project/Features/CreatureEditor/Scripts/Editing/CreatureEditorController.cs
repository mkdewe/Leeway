using System;
using Leeway.Creature.Domain;
using MessagePipe;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Ties the input, the sculpting session, the preview and the camera into one editor loop.
    /// </summary>
    /// <remarks>
    /// It holds all the knowledge of "what a click does" — which keeps <see cref="CreatureEditorSession"/>
    /// a pure layer of genome operations, testable without a scene.
    /// </remarks>
    public class CreatureEditorController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreatureEditorInput _input;
        [SerializeField] private CreatureEditorSession _session;
        [SerializeField] private CreatureBodyPreview _preview;
        [SerializeField] private VertebraHandleSet _handles;
        [SerializeField] private PartRotationGizmo _rotationGizmo;
        [SerializeField] private SpineExtendGizmo _extendGizmo;

        [Tooltip("Joint handles for the selected leg. Left empty: legs simply are not editable.")]
        [SerializeField] private LegBoneGizmo _legGizmo;
        [SerializeField] private PartDragGhost _ghost;
        [SerializeField] private CreatureEditorCameraRig _cameraRig;

        [Header("Selection")]
        [Tooltip("The vertebra handle layer. The raycast asks about it alone, so a click catches neither the body nor the ground.")]
        [SerializeField] private LayerMask _handleMask;

        [Tooltip("The preview torso's layer - clicking it toggles the visibility of the spine bones.")]
        [SerializeField] private LayerMask _bodyMask;

        [SerializeField] private float _radiusStep = 0.02f;

        [Tooltip("How many pixels the cursor may move for a press to still count as a click rather than a drag.")]
        [SerializeField] private float _clickSlopPixels = 4f;

        [Tooltip("How far past the body a part has to be pulled to detach it. Lower = parts vanish on a twitch of the hand.")]
        [SerializeField] private float _partDetachSlopPixels = 90f;

        private UnityEngine.Camera _camera;
        private CreatureBody _localBody;
        private IDisposable _bodySubscription;

        private bool _dragging;
        private Plane _dragPlane;
        /// <summary>The grab point in world space: for a vertebra the offset from the bone, for a part the point itself.</summary>
        private Vector3 _dragGrabWorld;

        private Vector3 _dragStartOffset;

        /// <summary>
        /// The spine segment's length at the moment of the grab. Dragging a vertebra only rotates it, so
        /// this number does not change — see <see cref="UpdateDrag"/>.
        /// </summary>
        private float _dragSegmentLength;

        /// <summary>The last cursor position that was still over the body — detachment is measured from it.</summary>
        private Vector2 _lastOnBodyPointer;

        private VertebraHandle _hoveredHandle;
        private int _hoveredVertebra = -1;

        private PartHandle _hoveredPart;
        private bool _draggingPart;
        private int _partDragIndex = -1;
        private bool _partDragMirrored;

        /// <summary>The part the selection is on. A second click on it shows the rotation gizmo.</summary>
        private int _selectedPart = -1;

        private bool _partWasSelectedOnPress;
        private Vector2 _pressPointer;

        private bool _paletteDragging;
        private int _paletteDragPartId;
        private bool _paletteDragMirrored;

        private bool _rotating;
        private GizmoAxis _rotationAxis;
        private Vector3 _rotationGrabDirection;
        private Quaternion _rotationStart;

        private bool _extendDragging;
        private SpineEnd _extendEnd;

        private bool _draggingLeg;

        /// <summary>Which leg joint the player is holding. The hip (0) is immovable, so it never lands here.</summary>
        private int _legDragJoint = -1;

        /// <summary>The arrow's axis in preview space, so the measurement is in genome units.</summary>
        private Vector3 _extendAxis;

        /// <summary>The cursor's distance from the end of the spine at the moment of the grab — the measurement is relative to it.</summary>
        private float _extendGrabOffset;

        /// <summary>How far you have to drag for one vertebra. This is the length of the segment the operation adds.</summary>
        private float _extendStep;

        /// <summary>Whether this drag has changed anything yet — if not, releasing counts as a click.</summary>
        private bool _extendChanged;

        public CreatureEditorMode Mode => _cameraRig != null ? _cameraRig.Mode : CreatureEditorMode.Sculpt;

        private void Awake() => _camera = UnityEngine.Camera.main;

        private void OnEnable()
        {
            if (_input == null) return;

            _input.SelectPressed += OnSelectPressed;
            _input.SelectReleased += OnSelectReleased;
            _input.AddVertebraPressed += OnAddVertebra;
            _input.RemoveVertebraPressed += OnRemoveVertebra;
            _input.ToggleModePressed += OnToggleMode;
            _input.ReturnToEditorPressed += OnReturnToEditor;
            _input.ApplyPressed += OnApply;
            _input.RevertPressed += OnRevert;
            _input.CycleSelectionPressed += OnCycleSelection;
            _input.ToggleSymmetryPressed += OnToggleSymmetry;
        }

        private void OnDisable()
        {
            if (_input == null) return;

            _input.SelectPressed -= OnSelectPressed;
            _input.SelectReleased -= OnSelectReleased;
            _input.AddVertebraPressed -= OnAddVertebra;
            _input.RemoveVertebraPressed -= OnRemoveVertebra;
            _input.ToggleModePressed -= OnToggleMode;
            _input.ReturnToEditorPressed -= OnReturnToEditor;
            _input.ApplyPressed -= OnApply;
            _input.RevertPressed -= OnRevert;
            _input.CycleSelectionPressed -= OnCycleSelection;
            _input.ToggleSymmetryPressed -= OnToggleSymmetry;
        }

        private void Start()
        {
            if (!GlobalMessagePipe.IsInitialized) return;

            _bodySubscription = GlobalMessagePipe.GetSubscriber<LocalCreatureBodyChangedMessage>().Subscribe(OnLocalBodyChanged);
        }

        private void OnDestroy() => _bodySubscription?.Dispose();

        private void Update()
        {
            if (_camera == null) _camera = UnityEngine.Camera.main;
            if (Mode != CreatureEditorMode.Sculpt) return;

            // A drag from the palette runs outside the ordinary hover/drag path: the player holds the
            // button down over the UI, so world clicks never land at all.
            if (_paletteDragging)
            {
                UpdatePaletteDrag();
                return;
            }

            // The gizmo keeps up with the part even while it is being dragged across the body.
            if (_rotationGizmo != null && _rotationGizmo.IsVisible) PlaceRotationGizmo();

            // The joint handles sit on the leg's pose, and that is recreated on every preview rebuild —
            // so we re-attach them every frame rather than once on selection.
            PlaceLegGizmo();

            if (_draggingLeg)
            {
                UpdateLegDrag();
                return;
            }

            // The extend arrows accompany the exposed vertebrae.
            if (_handles != null && _handles.Visible) PlaceExtendGizmo();
            else _extendGizmo?.Hide();

            if (_extendDragging)
            {
                UpdateExtendDrag();
                return;
            }

            if (_rotating)
            {
                UpdateRotationDrag();
                return;
            }

            if (_draggingPart)
            {
                UpdatePartDrag();
                return;
            }
            if (_dragging)
            {
                UpdateDrag();
                return;
            }

            UpdateHover();
            UpdateRadiusScroll();
        }

        // ---------- session lifecycle ----------

        private void OnLocalBodyChanged(LocalCreatureBodyChangedMessage message)
        {
            _localBody = message.Body;

            if (_cameraRig != null)
                _cameraRig.SetPlayTarget(_localBody != null ? _localBody.transform : null);

            if (_localBody == null) return;

            _localBody.BodyRebuilt += OnNetworkBodyRebuilt;
            BeginSessionFromNetworkBody();

            // The editor starts in sculpting mode, so the in-game creature should get out of sight at once.
            if (Mode == CreatureEditorMode.Sculpt) SetGameCreatureVisible(false);
        }

        private void OnNetworkBodyRebuilt(CreatureGenome genome, CreatureStats stats)
        {
            // A rebuild creates the renderers afresh, so the visibility state has to be applied again —
            // otherwise a hidden creature comes back on screen after every commit.
            SetGameCreatureVisible(Mode != CreatureEditorMode.Sculpt);

            // The server accepted a new genome — the preview has to start from what is actually in the
            // game, not from an abandoned working version.
            if (!_session.HasUnappliedChanges) BeginSessionFromNetworkBody();
        }

        private void BeginSessionFromNetworkBody()
        {
            if (_session == null || _localBody?.Genome == null) return;

            _session.Begin(_localBody.Genome);
            PublishSession();
            PublishSelection();
        }

        // ---------- selection and dragging ----------

        private void OnSelectPressed()
        {
            if (Mode != CreatureEditorMode.Sculpt || _camera == null) return;

            _pressPointer = _input.PointerPosition;

            // The gizmo is above everything: if it is exposed and the cursor lies on a ring, the click
            // should rotate rather than grab the part underneath.
            if (TryBeginRotation()) return;

            // A leg joint also lies on top of the part it grows from.
            if (TryBeginLegDrag()) return;

            // A part lies on top of a vertebra, so it takes precedence: hovering an eye grabs the eye,
            // not the vertebra underneath it.
            if (_hoveredPart != null)
            {
                _partWasSelectedOnPress = _selectedPart == _hoveredPart.GeneIndex;
                SelectPart(_hoveredPart.GeneIndex);
                BeginPartDrag(_hoveredPart);
                return;
            }

            // A click into empty space or onto the torso drops the part selection along with the gizmo.
            SelectPart(-1);

            // Spine vertebrae are only grabbed where the cursor already is (see UpdateHover) — that way
            // the click and the hover always agree on their target.
            if (_hoveredHandle != null)
            {
                _session.Select(_hoveredHandle.VertebraIndex);
                PublishSelection();
                BeginDrag(_hoveredHandle);
                return;
            }

            // The extend arrows stand outside the body, so we check them before the torso.
            if (TryBeginExtendDrag()) return;

            // A click on the torso toggles the bones' visibility; missing both hides them again.
            if (RaycastCorpus())
            {
                bool show = !(_handles?.Visible ?? false);
                _handles?.SetVisible(show);
                if (!show) _extendGizmo?.Hide();
                return;
            }

            if (_handles != null && _handles.Visible) _handles.SetVisible(false);
            _extendGizmo?.Hide();
        }

        private void OnSelectReleased()
        {
            if (_draggingLeg)
            {
                _draggingLeg = false;
                _legDragJoint = -1;
                _legGizmo?.SetHighlighted(-1);
                return;
            }

            if (_extendDragging)
            {
                // A plain click on the arrow, with no drag, adds one vertebra — the shortest route to a
                // longer creature stays where it was.
                bool wasClick = Vector2.Distance(_input.PointerPosition, _pressPointer) <= _clickSlopPixels;
                FinishExtendDrag(wasClick);
                return;
            }

            if (_rotating)
            {
                _rotating = false;
                _rotationGizmo?.SetHighlighted(null);
                return;
            }

            if (_draggingPart)
            {
                bool wasClick = Vector2.Distance(_input.PointerPosition, _pressPointer) <= _clickSlopPixels;
                bool reselected = _partWasSelectedOnPress;

                FinishPartDrag(wasClick);

                // A second click on the same part (a click, not a drag) reveals the rotation axes.
                if (wasClick && reselected && _selectedPart >= 0) ToggleRotationGizmo();
                return;
            }

            _dragging = false;
        }

        private void UpdateHover()
        {
            // A ring under the cursor takes the hover entirely — otherwise the part beneath the gizmo
            // would highlight along with the axis the player is actually aiming at.
            if (UpdateGizmoHover())
            {
                ClearHover();
                return;
            }

            if (UpdateLegHover())
            {
                ClearHover();
                return;
            }

            if (UpdateExtendHover())
            {
                ClearHover();
                return;
            }

            ResolveHover(out VertebraHandle vertebra, out PartHandle part);

            if (part != _hoveredPart)
            {
                if (_hoveredPart != null) _hoveredPart.SetHighlighted(false);

                _hoveredPart = part;

                if (_hoveredPart != null) _hoveredPart.SetHighlighted(true);
            }

            if (vertebra == _hoveredHandle) return;

            if (_hoveredHandle != null) _hoveredHandle.SetHighlighted(false);

            _hoveredHandle = vertebra;
            _hoveredVertebra = vertebra != null ? vertebra.VertebraIndex : -1;

            if (_hoveredHandle != null) _hoveredHandle.SetHighlighted(true);
        }

        /// <summary>
        /// Places the extend arrows at the ends of the spine. Called every frame along with the exposed
        /// vertebrae — bones do not survive a preview rebuild.
        /// </summary>
        private void PlaceExtendGizmo()
        {
            if (_extendGizmo == null || _preview?.Body?.Bones == null) return;

            Transform[] bones = _preview.Body.Bones;
            if (bones.Length < 2 || bones[0] == null || bones[^1] == null)
            {
                _extendGizmo.Hide();
                return;
            }

            // The "away from the body" directions at both ends of the chain.
            Vector3 headOut = (bones[0].position - bones[1].position).normalized;
            Vector3 tailOut = (bones[^1].position - bones[^2].position).normalized;

            _extendGizmo.Place(bones[0].position, headOut, bones[^1].position, tailOut);
        }

        /// <summary>
        /// Grabs an extend arrow. Returns true when the arrow took the click.
        /// </summary>
        private bool TryBeginExtendDrag()
        {
            if (_extendGizmo == null || !_extendGizmo.IsVisible || _camera == null || _session?.Working == null) return false;
            if (_preview == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_extendGizmo.TryPick(ray, out SpineEnd end)) return false;
            if (!_extendGizmo.TryGetAxis(end, out Vector3 origin, out Vector3 direction)) return false;
            if (!TryEndOfSpine(end, out Vector3 endLocal)) return false;

            _dragPlane = new Plane(-_camera.transform.forward, origin);
            if (!TryProjectPointer(out Vector3 world)) return false;

            _extendEnd = end;
            _extendAxis = _preview.transform.InverseTransformDirection(direction).normalized;
            _extendGrabOffset = Vector3.Dot(_preview.transform.InverseTransformPoint(world) - endLocal, _extendAxis);
            _extendStep = EndSegmentLength(end);
            _extendChanged = false;
            _extendDragging = true;

            return true;
        }

        /// <summary>
        /// Dragging the arrow adds and removes vertebrae.
        /// </summary>
        /// <remarks>
        /// <para>The direction decides the operation: away from the body (i.e. opposite to the previous
        /// vertebra) grows the spine, towards the middle shortens it. Without that second direction there
        /// was no way to remove an end vertebra with the mouse at all.</para>
        ///
        /// <para>The measure is a <b>segment length</b>, not pixels: a cursor moved by one segment adds a
        /// vertebra, so the body keeps up with the hand instead of growing at a rate that depends on the
        /// camera zoom. The distance is measured from the <b>current</b> end of the spine, not from the
        /// grab point — the spine moves its own anchor on every change in vertebra count, so only that
        /// measurement makes the tip of the body genuinely chase the cursor.</para>
        /// </remarks>
        private void UpdateExtendDrag()
        {
            if (!_input.IsDragging)
            {
                FinishExtendDrag(asClick: false);
                return;
            }

            if (_session?.Working == null || _preview == null || !TryProjectPointer(out Vector3 world)) return;

            Vector3 pointer = _preview.transform.InverseTransformPoint(world);
            bool changed = false;

            // A safety net: one drag will not rework more vertebrae than the whole creature is allowed
            // to have anyway.
            int guard = GenomeLimits.MaxVertebrae;

            while (guard-- > 0 && TryMeasureExtendDrag(pointer, out float travel))
            {
                if (travel >= _extendStep)
                {
                    if (!_session.ExtendSpine(_extendEnd == SpineEnd.Head)) break;
                }
                else if (travel <= -_extendStep)
                {
                    if (!_session.ShrinkSpine(_extendEnd == SpineEnd.Head)) break;
                }
                else break;

                _extendStep = EndSegmentLength(_extendEnd);
                changed = true;
            }

            if (!changed) return;

            _extendChanged = true;
            PublishSelection();
        }

        /// <summary>How far past the end of the spine the cursor stands, measured from the grab point.</summary>
        private bool TryMeasureExtendDrag(Vector3 pointer, out float travel)
        {
            travel = 0f;

            if (!TryEndOfSpine(_extendEnd, out Vector3 endLocal)) return false;

            travel = Vector3.Dot(pointer - endLocal, _extendAxis) - _extendGrabOffset;
            return true;
        }

        /// <summary>The end vertebra of the given end, in preview space.</summary>
        private bool TryEndOfSpine(SpineEnd end, out Vector3 local)
        {
            local = default;

            Transform[] bones = _preview?.Body?.Bones;
            if (bones == null || bones.Length < 2) return false;

            Transform bone = end == SpineEnd.Head ? bones[0] : bones[^1];
            if (bone == null) return false;

            local = _preview.transform.InverseTransformPoint(bone.position);
            return true;
        }

        /// <summary>Ends the arrow drag. Releasing without movement counts as a click and adds one vertebra.</summary>
        private void FinishExtendDrag(bool asClick)
        {
            if (!_extendDragging) return;

            _extendDragging = false;

            if (!asClick || _extendChanged || _session == null) return;

            _session.ExtendSpine(_extendEnd == SpineEnd.Head);
            PublishSelection();
        }

        /// <summary>
        /// The length of the spine's end segment — that is how much travel one vertebra costs.
        /// </summary>
        private float EndSegmentLength(SpineEnd end)
        {
            CreatureGenome genome = _session?.Working;
            if (genome == null || genome.VertebraCount < 2) return GenomeLimits.DefaultSegmentOffset.magnitude;

            // A segment is always described by the vertebra further from the head, so on the head side it
            // is vertebra number 1 that is asked about it.
            int index = end == SpineEnd.Head ? 1 : genome.VertebraCount - 1;
            float length = genome.GetVertebra(index).LocalOffset.magnitude;

            return Mathf.Clamp(length, GenomeLimits.MinSegmentLength, GenomeLimits.MaxSegmentLength);
        }

        /// <summary>Removes the highlight from the vertebra and part the cursor was on.</summary>
        private void ClearHover()
        {
            if (_hoveredPart != null) _hoveredPart.SetHighlighted(false);
            _hoveredPart = null;

            if (_hoveredHandle != null) _hoveredHandle.SetHighlighted(false);
            _hoveredHandle = null;
            _hoveredVertebra = -1;
        }

        /// <summary>Highlights the extend arrow under the cursor. Returns true when the cursor is on it.</summary>
        private bool UpdateExtendHover()
        {
            if (_extendGizmo == null || !_extendGizmo.IsVisible || _camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_extendGizmo.TryPick(ray, out SpineEnd end))
            {
                _extendGizmo.SetHighlighted(null);
                return false;
            }

            _extendGizmo.SetHighlighted(end);
            return true;
        }

        // ---------- leg bones ----------

        /// <summary>
        /// Re-attaches the joint handles to the selected leg's current pose.
        /// </summary>
        /// <remarks>
        /// The handles appear for the selected locomotion part and for it alone — an eye or a horn has no
        /// chain, so there is nothing to edit.
        /// </remarks>
        private void PlaceLegGizmo()
        {
            if (_legGizmo == null) return;

            if (_selectedPart < 0 || _preview == null)
            {
                _legGizmo.Hide();
                return;
            }

            _legGizmo.Show(_preview.Legs, _selectedPart);
        }

        /// <summary>Highlights the joint under the cursor. Returns true when the cursor is on one.</summary>
        private bool UpdateLegHover()
        {
            if (_legGizmo == null || !_legGizmo.IsVisible || _camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_legGizmo.TryPick(ray, out int joint))
            {
                _legGizmo.SetHighlighted(-1);
                return false;
            }

            _legGizmo.SetHighlighted(joint);
            return true;
        }

        /// <summary>Grabs a leg joint. The hip is the attachment point, so we do not drag it.</summary>
        private bool TryBeginLegDrag()
        {
            if (_legGizmo == null || !_legGizmo.IsVisible || _camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_legGizmo.TryPick(ray, out int joint) || joint <= 0) return false;

            _dragPlane = new Plane(-_camera.transform.forward, _legGizmo.JointPosition(joint));
            _legDragJoint = joint;
            _draggingLeg = true;
            _legGizmo.SetHighlighted(joint);

            return true;
        }

        /// <summary>
        /// Dragging a joint lengthens and shortens the leg.
        /// </summary>
        /// <remarks>
        /// <para>The measure is the distance from the <b>hip</b> divided by the joint number: all the
        /// chain's links are equal, so grabbing the second joint and pulling it out to a metre means "two
        /// links of half a metre". That way the same gesture gives the same result regardless of which
        /// joint the player grabbed.</para>
        ///
        /// <para>The number of bends is left alone — the scroll wheel is for that. One gesture, one
        /// change: otherwise dragging a foot could rebuild a knee in passing.</para>
        /// </remarks>
        private void UpdateLegDrag()
        {
            if (!_input.IsDragging)
            {
                _draggingLeg = false;
                _legDragJoint = -1;
                return;
            }

            if (_session?.Working == null || _legDragJoint <= 0) return;
            if (!TryProjectPointer(out Vector3 world)) return;
            if (!GenomeStatRules.TryResolveLeg(_session.Working, _session.Rules, _selectedPart, out LegSpec leg, out _)) return;

            float reach = Vector3.Distance(_legGizmo.JointPosition(0), world);
            float segment = reach / _legDragJoint;

            _session.SetPartLeg(_selectedPart, new LegSpec(leg.BendPoints, segment));
        }

        /// <summary>
        /// Scrolling over a joint adds and removes a bend — the counterpart of growing a vertebra.
        /// Returns true when the scroll was consumed by the leg.
        /// </summary>
        private bool TryScrollLegBend(float scroll)
        {
            if (_legGizmo == null || !_legGizmo.IsVisible || _camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_legGizmo.TryPick(ray, out _)) return false;

            if (!GenomeStatRules.TryResolveLeg(_session.Working, _session.Rules, _selectedPart, out LegSpec leg, out _)) return false;

            int bend = leg.BendPoints + (int)Mathf.Sign(scroll);
            if (bend == leg.BendPoints) return true;

            // The leg's reach stays the same: adding bends is meant to change the character of the gait,
            // not to lengthen the creature's legs by a whole link in passing.
            var candidate = new LegSpec(bend, leg.SegmentLength);
            if (candidate.BendPoints == leg.BendPoints) return true;

            float segment = leg.Reach / candidate.SegmentCount;
            _session.SetPartLeg(_selectedPart, new LegSpec(bend, segment));

            return true;
        }

        /// <summary>Highlights the gizmo axis under the cursor. Returns true when the cursor is on a ring.</summary>
        private bool UpdateGizmoHover()
        {
            if (_rotationGizmo == null || !_rotationGizmo.IsVisible || _camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_rotationGizmo.TryPick(ray, out GizmoAxis axis, out _))
            {
                _rotationGizmo.SetHighlighted(null);
                return false;
            }

            _rotationGizmo.SetHighlighted(axis);
            return true;
        }

        /// <summary>
        /// Vertebra and part handles share a layer, so a single raycast decides — whatever is closer to
        /// the camera wins, and only one of the two references comes back non-null.
        /// </summary>
        private void ResolveHover(out VertebraHandle vertebra, out PartHandle part)
        {
            vertebra = null;
            part = null;

            if (_camera == null) return;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _handleMask, QueryTriggerInteraction.Collide)) return;

            if (hit.collider.TryGetComponent(out PartHandle hitPart))
            {
                part = hitPart;
                return;
            }

            hit.collider.TryGetComponent(out vertebra);
        }

        private bool RaycastCorpus()
        {
            if (_camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            return Physics.Raycast(ray, 500f, _bodyMask, QueryTriggerInteraction.Collide);
        }

        private void BeginDrag(VertebraHandle handle)
        {
            Transform bone = handle.transform.parent;
            if (bone == null || bone.parent == null) return;

            // A plane facing the camera and passing through the handle: the cursor then maps
            // unambiguously onto a point in space without guessing at depth.
            _dragPlane = new Plane(-_camera.transform.forward, bone.position);
            if (!TryProjectPointer(out Vector3 world)) return;

            // We remember where on the vertebra the player grabbed it — the cursor should hold that same
            // point of the bone rather than jumping to its centre.
            _dragGrabWorld = world - bone.position;

            // The segment to the predecessor is rigid: dragging a vertebra rotates it, it does not
            // stretch it. The head has no predecessor, so there is nothing to hold.
            int index = handle.VertebraIndex;
            _dragSegmentLength = index > 0 && _session?.Working != null
                ? _session.Working.GetVertebra(index).LocalOffset.magnitude
                : 0f;

            _dragging = true;
        }

        /// <summary>
        /// Drags a vertebra along behind the cursor.
        /// </summary>
        /// <remarks>
        /// The correction is computed every frame from the bone's <b>current</b> position, not from the
        /// grab point. The reason: every change to the spine moves the anchor (the centre of the body
        /// returns to the root), so the bone escapes from under the cursor during its own
        /// drag — measuring from the grab point then computed a displacement in a frame that had itself
        /// moved, and the vertebra travelled half as far as the mouse. The loop closes on its own within
        /// a few frames and the error does not accumulate, because the source of truth is always the
        /// bone's current position.
        ///
        /// <para><b>The segment is rigid.</b> A vertebra changes its thickness, not its length — pulling
        /// it up should bend the spine at that point, not stretch the flesh. Without this, every sideways
        /// move lengthened the segment to the predecessor by the whole perpendicular component, so
        /// sculpting the silhouette lengthened the creature in passing. The spine's length changes only
        /// where that is an explicit operation — with the arrows at the ends of the body.</para>
        /// </remarks>
        private void UpdateDrag()
        {
            if (!_input.IsDragging)
            {
                _dragging = false;
                return;
            }

            int index = _session.SelectedVertebra;
            if (index < 0 || _preview?.Body == null || index >= _preview.Body.Bones.Length) return;

            Transform bone = _preview.Body.Bones[index];
            Transform parent = bone != null ? bone.parent : null;
            if (parent == null || !TryProjectPointer(out Vector3 world)) return;

            Vector3 wanted = world - _dragGrabWorld;
            Vector3 correction = parent.InverseTransformDirection(wanted - bone.position);

            Vector3 offset = _session.Working.GetVertebra(index).LocalOffset + correction;

            // The vertebra travels on a sphere of its segment's radius — it rotates relative to its
            // predecessor rather than moving away from it.
            if (_dragSegmentLength > 1e-4f && offset.sqrMagnitude > 1e-8f)
                offset = offset.normalized * _dragSegmentLength;

            _session.SetSelectedOffset(offset);
        }

        // ---------- part selection and the rotation gizmo ----------

        private void SelectPart(int partIndex)
        {
            if (_selectedPart == partIndex) return;

            _selectedPart = partIndex;

            // The gizmo always starts hidden — only a second click reveals it.
            _rotationGizmo?.Hide();
            PublishPartSelection();
        }

        private void ToggleRotationGizmo()
        {
            if (_rotationGizmo == null) return;

            if (_rotationGizmo.IsVisible)
            {
                _rotationGizmo.Hide();
                return;
            }

            ShowRotationGizmo();
        }

        private void ShowRotationGizmo() => PlaceRotationGizmo();

        /// <summary>
        /// Sticks the gizmo to the selected part. Called every frame, because a preview rebuild destroys
        /// the bones — the gizmo has to keep up with them rather than hang off them.
        /// </summary>
        private void PlaceRotationGizmo()
        {
            if (_rotationGizmo == null || _session?.Working == null) return;
            if (_selectedPart < 0 || _selectedPart >= _session.Working.PartCount) return;
            if (!TryResolvePartFrame(_selectedPart, out Transform bone, out Quaternion frame)) return;

            // We take the position from the instance itself, not from the gene: the socket from the part
            // definition adds a fixed offset, so a gizmo computed from the gene would hang beside the part.
            Transform instance = ResolveSelectedInstance();
            Vector3 position = instance != null
                ? instance.position
                : bone.TransformPoint(_session.Working.GetPart(_selectedPart).LocalPosition);

            // The rings stand in the <b>part's</b> frame, not the bone's: the player turns around the axes
            // they can see, and those have to be the same ones the gene's correction operates in.
            _rotationGizmo.Place(position, frame * _session.Working.GetPart(_selectedPart).LocalRotation);
        }

        /// <summary>
        /// The frame the rotation correction stored in the gene operates in: the bone's rotation composed
        /// with the part's automatic orientation.
        /// </summary>
        /// <remarks>
        /// This is the heart of the rotation fix. The gene's correction is applied <b>after</b> the
        /// automatic orientation (<see cref="PartPlacement.FinalRotation"/>), so the axis handed to
        /// <c>AngleAxis</c> is an axis in that frame. The gizmo used to compute it in the bone's own
        /// frame — the X ring rotated the part around a completely different direction, and the further
        /// the part stood off the bone's axis, the more the rotation diverged from the mouse movement.
        /// </remarks>
        private bool TryResolvePartFrame(int partIndex, out Transform bone, out Quaternion frame)
        {
            bone = null;
            frame = Quaternion.identity;

            if (_session?.Working == null || partIndex < 0 || partIndex >= _session.Working.PartCount) return false;

            PartGene gene = _session.Working.GetPart(partIndex);
            if (_preview?.Body?.Bones == null || gene.BoneIndex >= _preview.Body.Bones.Length) return false;

            bone = _preview.Body.Bones[gene.BoneIndex];
            if (bone == null) return false;

            PartPose pose = PartPlacement.Resolve(_session.Working, gene, SkinOffsetOf(gene.PartId), GroundAlignedOf(gene.PartId));
            frame = bone.rotation * pose.Rotation;

            return true;
        }

        private bool TryBeginRotation()
        {
            if (_rotationGizmo == null || !_rotationGizmo.IsVisible || _camera == null) return false;
            if (_session?.Working == null || _selectedPart < 0 || _selectedPart >= _session.Working.PartCount) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_rotationGizmo.TryPick(ray, out GizmoAxis axis, out Vector3 hitPoint)) return false;

            _rotationAxis = axis;
            _rotationGrabDirection = DirectionOnRing(hitPoint);
            _rotationStart = _session.Working.GetPart(_selectedPart).LocalRotation;
            _rotating = true;

            _rotationGizmo.SetHighlighted(axis);
            return true;
        }

        private void UpdateRotationDrag()
        {
            if (!_input.IsDragging)
            {
                _rotating = false;
                _rotationGizmo?.SetHighlighted(null);
                return;
            }

            if (_session?.Working == null || _selectedPart < 0 || _selectedPart >= _session.Working.PartCount) return;
            if (!_rotationGizmo.TryProjectOntoRing(_camera.ScreenPointToRay(_input.PointerPosition), _rotationAxis, out Vector3 point)) return;

            Vector3 current = DirectionOnRing(point);
            if (current.sqrMagnitude < 1e-8f || _rotationGrabDirection.sqrMagnitude < 1e-8f) return;

            Vector3 worldAxis = _rotationGizmo.WorldAxis(_rotationAxis);
            float angle = Vector3.SignedAngle(_rotationGrabDirection, current, worldAxis);

            // The angle is measured absolutely from the grab, not incrementally — increments accumulate
            // error and the part drifts under fast turning.
            if (!TryResolvePartFrame(_selectedPart, out _, out Quaternion frame)) return;

            // The axis is carried into the frame the gene's correction operates in. Without that the
            // rotation comes out around a different axis from the one the player grabbed.
            Vector3 localAxis = (Quaternion.Inverse(frame) * worldAxis).normalized;
            _session.RotatePart(_selectedPart, Quaternion.AngleAxis(angle, localAxis) * _rotationStart);
        }

        /// <summary>The direction from the gizmo's centre to a point on the ring — the basis for measuring the angle.</summary>
        private Vector3 DirectionOnRing(Vector3 worldPoint) => worldPoint - _rotationGizmo.WorldCenter;

        /// <summary>The first (non-mirrored) instance of the selected part — the gizmo's anchor point.</summary>
        private Transform ResolveSelectedInstance()
        {
            if (_preview?.Body?.PartInstances == null) return null;

            foreach (CreaturePartInstance instance in _preview.Body.PartInstances)
            {
                if (instance.GeneIndex != _selectedPart || instance.Mirrored) continue;
                if (instance.Object != null) return instance.Object.transform;
            }

            return null;
        }

        /// <summary>Toggles the selected part's symmetry — called by the key and by the HUD button.</summary>
        public void ToggleSelectedPartMirror()
        {
            if (_session?.Working == null || _selectedPart < 0 || _selectedPart >= _session.Working.PartCount) return;

            PartGene gene = _session.Working.GetPart(_selectedPart);
            _session.SetPartMirrored(_selectedPart, !gene.Mirrored);
            PublishPartSelection();
        }

        private void PublishPartSelection()
        {
            if (!GlobalMessagePipe.IsInitialized) return;

            bool valid = _session?.Working != null && _selectedPart >= 0 && _selectedPart < _session.Working.PartCount;

            bool mirrored = false;
            bool canMirror = false;

            if (valid)
            {
                PartGene gene = _session.Working.GetPart(_selectedPart);
                mirrored = gene.Mirrored;
                canMirror = _session.Rules.TryGetRule(gene.PartId, out PartRule rule) && rule.MirrorCapable;
            }

            GlobalMessagePipe.GetPublisher<CreatureEditorPartSelectionChangedMessage>()
                .Publish(new CreatureEditorPartSelectionChangedMessage(valid ? _selectedPart : -1, mirrored, canMirror));
        }

        // ---------- grabbing a part ----------

        private void BeginPartDrag(PartHandle handle)
        {
            Transform bone = handle.transform.parent;
            if (bone == null || _session?.Working == null) return;

            int index = handle.GeneIndex;
            if (index < 0 || index >= _session.Working.PartCount) return;

            _dragPlane = new Plane(-_camera.transform.forward, handle.transform.position);
            if (!TryProjectPointer(out Vector3 world)) return;

            PartGene dragged = _session.Working.GetPart(index);

            // The starting point is where the part is <b>seen</b>, not the raw value in the gene.
            // Otherwise the first cursor move jumps by the difference between the two, and the rest of the
            // drag runs at a different scale from the mouse.
            _dragGrabWorld = world;
            _dragStartOffset = PartPlacement.Resolve(_session.Working, dragged, SkinOffsetOf(dragged.PartId),
                GroundAlignedOf(dragged.PartId)).Position;
            _partDragIndex = index;
            _partDragMirrored = handle.Mirrored;
            _draggingPart = true;

            // The grab counts as "over the body" regardless of whether the ray happens to hit the torso:
            // the player grabbed a part that grows out of it.
            _lastOnBodyPointer = _input.PointerPosition;

            // The preview is rebuilt on every edit, so the instance under the cursor is about to
            // disappear — from here on we work on the gene index alone.
            _hoveredPart = null;
        }

        private void UpdatePartDrag()
        {
            if (!_input.IsDragging)
            {
                FinishPartDrag(Vector2.Distance(_input.PointerPosition, _pressPointer) <= _clickSlopPixels);
                return;
            }

            if (_session?.Working == null || _partDragIndex < 0 || _partDragIndex >= _session.Working.PartCount) return;

            PartGene gene = _session.Working.GetPart(_partDragIndex);
            int boneIndex = gene.BoneIndex;
            if (_preview?.Body == null || boneIndex >= _preview.Body.Bones.Length) return;

            Transform bone = _preview.Body.Bones[boneIndex];
            if (bone == null || !TryProjectPointer(out Vector3 world)) return;

            if (RaycastCorpus()) _lastOnBodyPointer = _input.PointerPosition;

            // One vertebra's skin ends halfway to its neighbour, so travelling along the body has to move
            // the part onto the next bone. Without that the part stops while the cursor keeps going — and
            // that is the moment it "comes apart".
            if (TryRebasePartDrag(gene, boneIndex, world)) return;

            // The displacement is computed as a <b>direction</b> from the world-space grab point, not as
            // the difference of two points in bone space: the bone may have moved in the meantime (the
            // spine's anchor shifts), and then the difference of points would also measure the movement of
            // the frame of reference itself, and the part would drift away from the cursor.
            Vector3 delta = bone.InverseTransformDirection(world - _dragGrabWorld);

            // A mirrored instance has a flipped x, so without this the dragged left-hand piece would run
            // away from the cursor instead of towards it.
            if (_partDragMirrored) delta.x = -delta.x;

            _session.MovePart(_partDragIndex, _dragStartOffset + delta);
        }

        /// <summary>
        /// Moves the dragged part onto the vertebra the cursor is over. Returns true when the move
        /// happened — the drag measurement then restarts, because the frame of reference has changed.
        /// </summary>
        /// <remarks>
        /// A part whose rule does not allow the new site (an eye off the head) simply stays on its own
        /// bone. We check that <b>before</b> the operation, so as not to bury the HUD in a rejection
        /// message on every frame of the drag.
        /// </remarks>
        private bool TryRebasePartDrag(in PartGene gene, int boneIndex, Vector3 world)
        {
            int nearest = ClosestBone(world);
            if (nearest < 0 || nearest == boneIndex) return false;
            if (!CanAttachTo(gene.PartId, nearest)) return false;

            Transform target = _preview.Body.Bones[nearest];
            if (target == null) return false;

            Vector3 local = target.InverseTransformPoint(world);

            // The gene describes the right-hand piece, so a grab on the mirrored one has to be flipped back.
            if (_partDragMirrored) local.x = -local.x;

            if (!_session.MovePartToBone(_partDragIndex, nearest, local)) return false;

            _dragStartOffset = local;
            _dragGrabWorld = world;
            return true;
        }

        /// <summary>Whether the rules allow this part on the given vertebra.</summary>
        private bool CanAttachTo(int partId, int boneIndex)
        {
            if (_session?.Working == null) return false;
            if (!_session.Rules.TryGetRule(partId, out PartRule rule)) return false;

            return (rule.AllowedSites & GenomeValidator.SiteForBone(boneIndex, _session.Working.VertebraCount)) != 0;
        }

        /// <summary>The vertebra nearest a point in world space — the boundary runs halfway between bones.</summary>
        private int ClosestBone(Vector3 world)
        {
            Transform[] bones = _preview?.Body?.Bones;
            if (bones == null) return -1;

            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                float distance = (bones[i].position - world).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Ends a part drag. It only detaches the part once the player has genuinely pulled it off the body.
        /// </summary>
        /// <remarks>
        /// <para>It used to be enough to release the button where the ray missed the torso — and parts
        /// by definition <b>stick out</b> past the silhouette. Clicking a horn, a leg or an antenna
        /// missed the body and deleted the very part the player was aiming at. Hence things vanishing at
        /// the merest touch.</para>
        ///
        /// <para>There are two gates now. A click with no movement <b>never</b> detaches — that is a
        /// selection, not a discard. And on a genuine drag what counts is the distance from the last
        /// place the cursor was still over the body, so detaching requires deliberately pulling the part
        /// aside.</para>
        /// </remarks>
        private void FinishPartDrag(bool wasClick)
        {
            if (!_draggingPart) return;

            _draggingPart = false;

            int index = _partDragIndex;
            _partDragIndex = -1;

            if (wasClick) return;
            if (RaycastCorpus()) return;

            if (Vector2.Distance(_input.PointerPosition, _lastOnBodyPointer) < _partDetachSlopPixels) return;

            if (_session == null || index < 0) return;

            // Detaching renumbers the parts, so holding the selection on the old index would already
            // point at a different part.
            _session.DetachPart(index);
            SelectPart(-1);
        }

        private bool TryProjectPointer(out Vector3 world)
        {
            world = default;
            if (_camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!_dragPlane.Raycast(ray, out float distance)) return false;

            world = ray.GetPoint(distance);
            return true;
        }

        private void UpdateRadiusScroll()
        {
            float scroll = _input.ZoomDelta;
            if (Mathf.Approximately(scroll, 0f)) return;
            if (_input.IsOrbiting) return;

            // A leg joint lies on top, so it takes the scroll from the vertebra underneath.
            if (TryScrollLegBend(scroll)) return;

            // The scroll grows the flesh around the vertebra under the cursor — regardless of what is
            // currently selected by a click for dragging.
            int index = _hoveredVertebra;
            if (index < 0 || _session.Working == null) return;

            float current = _session.Working.GetVertebra(index).Radius;
            _session.SetVertebraRadius(index, current + Mathf.Sign(scroll) * _radiusStep);
        }

        // ---------- operations ----------

        private void OnAddVertebra()
        {
            if (Mode != CreatureEditorMode.Sculpt) return;
            _session.AddVertebra();
        }

        private void OnRemoveVertebra()
        {
            if (Mode != CreatureEditorMode.Sculpt) return;

            _session.RemoveSelectedVertebra();
            PublishSelection();
        }

        private void OnCycleSelection(int direction)
        {
            if (Mode != CreatureEditorMode.Sculpt || _session.Working == null) return;

            int count = _session.Working.VertebraCount;
            if (count == 0) return;

            int next = Mathf.Max(0, _session.SelectedVertebra) + direction;
            _session.Select((next % count + count) % count);
            PublishSelection();
        }

        private void OnToggleSymmetry()
        {
            if (Mode != CreatureEditorMode.Sculpt) return;
            ToggleSelectedPartMirror();
        }

        private void OnToggleMode()
            => SetMode(Mode == CreatureEditorMode.Sculpt ? CreatureEditorMode.Play : CreatureEditorMode.Sculpt);

        private void OnReturnToEditor() => EnterSculptMode();

        /// <summary>
        /// Returns to sculpting. The entry point for the "Back to editor" button and for Escape.
        /// </summary>
        /// <remarks>
        /// Public, because a way back has to exist even for a player who does not know about Tab — and
        /// committing a genome pushes them out into the playground on its own.
        /// </remarks>
        public void EnterSculptMode() => SetMode(CreatureEditorMode.Sculpt);

        /// <summary>Attaches a part to the selected vertebra — a fallback entry point when there is no drag.</summary>
        public void AttachPart(int partId, bool mirrored)
        {
            if (_session == null) return;
            _session.AttachPart(partId, mirrored);
        }

        // ---------- dragging from the palette ----------

        /// <summary>Starts dragging a part out of the palette. Called when the player presses a button in it.</summary>
        public void BeginPaletteDrag(int partId, bool mirrored)
        {
            if (Mode != CreatureEditorMode.Sculpt || _session?.Working == null) return;

            _paletteDragPartId = partId;
            _paletteDragMirrored = mirrored;
            _paletteDragging = true;

            SelectPart(-1);
            _ghost?.Show(partId);
        }

        /// <summary>
        /// Ends a drag from the palette. Dropping on the body attaches the part at that spot; dropping
        /// beside it does nothing, because the player has just changed their mind.
        /// </summary>
        public void EndPaletteDrag()
        {
            if (!_paletteDragging) return;

            _paletteDragging = false;
            _ghost?.Hide();

            if (!TryResolveBodyDrop(out int boneIndex, out Vector3 localPosition)) return;

            _session.AttachPartAt(_paletteDragPartId, boneIndex, localPosition, _paletteDragMirrored);
        }

        private void UpdatePaletteDrag()
        {
            if (_ghost == null) return;

            if (!TryResolveBodyDrop(out int boneIndex, out Vector3 localPosition))
            {
                _ghost.SetOverBody(false);
                _ghost.FollowPointer(_camera, _input.PointerPosition);
                return;
            }

            Transform bone = _preview.Body.Bones[boneIndex];
            PartPose pose = PartPlacement.Resolve(_session.Working, boneIndex, localPosition,
                SkinOffsetOf(_paletteDragPartId), GroundAlignedOf(_paletteDragPartId));

            _ghost.SetOverBody(true);
            _ghost.Place(bone.TransformPoint(pose.Position), bone.rotation * pose.Rotation);
        }

        /// <summary>
        /// Turns the point under the cursor on the body into a "vertebra + direction" pair. The vertebra
        /// is chosen by nearest bone, because it is what decides the attachment site (head/torso/tail).
        /// </summary>
        private bool TryResolveBodyDrop(out int boneIndex, out Vector3 localPosition)
        {
            boneIndex = -1;
            localPosition = default;

            if (_camera == null || _preview?.Body?.Bones == null || _session?.Working == null) return false;

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _bodyMask, QueryTriggerInteraction.Collide)) return false;

            boneIndex = ClosestBone(hit.point);
            if (boneIndex < 0) return false;

            localPosition = _preview.Body.Bones[boneIndex].InverseTransformPoint(hit.point);
            return true;
        }

        private float SkinOffsetOf(int partId)
        {
            CreaturePartCatalog catalog = _preview != null ? _preview.Catalog : null;
            return catalog != null && catalog.TryGetPart(partId, out CreaturePartDefinition definition) && definition != null
                ? definition.SkinOffset
                : 0f;
        }

        /// <summary>
        /// Whether a part aims at the ground rather than away from the body. It has to agree with what
        /// the instantiator goes by — otherwise the editor would compute a leg's rotation differently
        /// from how it looks on screen.
        /// </summary>
        private bool GroundAlignedOf(int partId)
        {
            CreaturePartCatalog catalog = _preview != null ? _preview.Catalog : null;
            return catalog != null
                && catalog.TryGetPart(partId, out CreaturePartDefinition definition)
                && definition != null
                && definition.Category == PartCategory.Locomotion;
        }

        public void Apply() => OnApply();
        public void Revert() => OnRevert();

        private void OnApply()
        {
            if (_localBody == null || _session?.Working == null) return;

            GenomeValidationResult check = _session.ValidateForCommit();
            if (!check.IsValid)
            {
                Debug.LogWarning($"Genome not ready to commit: {check.Error} (element {check.Index}).", this);
                return;
            }

            // Nothing was changed — there is nothing to commit, and the in-game creature already carries
            // exactly this genome. Sending it again would cost everyone a body rebuild and could run into
            // the commit rate limit, trapping in the editor a player who only wanted to move the creature
            // they were given.
            if (!_session.HasUnappliedChanges)
            {
                EnterPlayMode();
                return;
            }

            if (!_localBody.RequestCommit(_session.Working, out GenomeError error))
            {
                Debug.LogWarning($"The commit did not go through: {error}.", this);
                return;
            }

            _session.MarkApplied();

            // Committing ends sculpting: the preview disappears and the player moves over to their
            // creature in the game. Without this they were left with two bodies on screen and nothing to
            // steer.
            EnterPlayMode();
        }

        /// <summary>Moves from sculpting to driving the creature in the playground.</summary>
        private void EnterPlayMode() => SetMode(CreatureEditorMode.Play);

        /// <summary>
        /// The only transition between modes — the camera, the sculpting machinery and the announcement
        /// of the change always travel together.
        /// </summary>
        /// <remarks>
        /// Every entry point used to do this on its own, and the UI had no way of learning that the
        /// player had left sculpting — the panels stayed on screen over the game. We announce the mode
        /// rather than calling the HUD directly: the controller has no reason to know which panels lie on
        /// the canvas.
        /// </remarks>
        private void SetMode(CreatureEditorMode mode)
        {
            if (_cameraRig != null)
            {
                if (_cameraRig.Mode == mode) return;
                _cameraRig.SetMode(mode);
            }

            // Leaving sculpting mid-drag must not leave a grab dangling — on returning it would resume
            // moving a part the player is no longer holding.
            if (mode == CreatureEditorMode.Sculpt) EnterSculpting();
            else ExitSculpting();

            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<CreatureEditorModeChangedMessage>().Publish(new CreatureEditorModeChangedMessage(mode));
        }

        /// <summary>
        /// Hides or shows the player's creature in the playground. It disappears while sculpting, so the
        /// scene does not hold two copies of the same creature — the sculpted one and the in-game one.
        /// </summary>
        private void SetGameCreatureVisible(bool visible)
        {
            if (_localBody == null) return;

            foreach (Renderer renderer in _localBody.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

        /// <summary>Hides all the sculpting machinery: the preview, the handles, the gizmos and any grabs in progress.</summary>
        private void ExitSculpting()
        {
            _handles?.SetVisible(false);
            SelectPart(-1);

            _dragging = false;
            _draggingPart = false;
            _partDragIndex = -1;
            _rotating = false;
            _extendDragging = false;

            _paletteDragging = false;
            _draggingLeg = false;
            _legDragJoint = -1;
            _legGizmo?.Hide();
            _ghost?.Hide();

            ClearHover();

            if (_preview != null) _preview.SetVisible(false);
            SetGameCreatureVisible(true);
        }

        /// <summary>Returns to sculpting: the preview comes back on screen and the in-game creature leaves the background.</summary>
        private void EnterSculpting()
        {
            if (_preview != null) _preview.SetVisible(true);
            SetGameCreatureVisible(false);
        }

        private void OnRevert()
        {
            _session?.Revert();
            PublishSelection();
        }

        private void PublishSession()
        {
            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<CreatureEditorSessionChangedMessage>().Publish(new CreatureEditorSessionChangedMessage(_session));
        }

        private void PublishSelection()
        {
            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<CreatureEditorSelectionChangedMessage>().Publish(new CreatureEditorSelectionChangedMessage(_session.SelectedVertebra));
        }
    }
}
