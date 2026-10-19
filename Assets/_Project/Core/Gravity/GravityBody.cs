using UnityEngine;
using UnityEngine.SceneManagement;

namespace Leeway.Gravity
{
    /// <summary>Per-body force for unpredicted physics. Dedicated physics scenes call Step explicitly;
    /// predicted controllers apply their own gravity inside the replicated tick.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class GravityBody : MonoBehaviour
    {
        private Rigidbody _body;
        public Scene SourceScene { get; set; }
        public bool ExternallyStepped { get; set; }
        public float GravityScale { get; set; } = 1f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            SourceScene = gameObject.scene;
        }

        private void FixedUpdate()
        {
            if (!ExternallyStepped) Step();
        }

        public void Step()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            if (!isActiveAndEnabled || _body.isKinematic || _body.IsSleeping()) return;
            _body.AddForce(GravityWorld.AccelerationAt(_body.worldCenterOfMass,
                SourceScene.IsValid() ? SourceScene : gameObject.scene) * GravityScale, ForceMode.Acceleration);
        }
    }
}
