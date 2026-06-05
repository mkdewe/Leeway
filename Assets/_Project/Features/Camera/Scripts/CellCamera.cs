using System;
using Leeway.Creature;
using MessagePipe;
using UnityEngine;

namespace Leeway.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CellCamera : MonoBehaviour
    {
        [SerializeField] private float _followSpeed = 5f;
        [SerializeField] private float _baseOrthoSize = 6f;
        [SerializeField] private float _zoomPerSize = 0.4f;
        [SerializeField] private float _zoomSpeed = 2f;

        private Transform _target;
        private CellEntity _targetEntity;
        private UnityEngine.Camera _cam;
        private IDisposable _subscription;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _baseOrthoSize;
        }

        private void Start()
        {
            if (GlobalMessagePipe.IsInitialized)
                _subscription = GlobalMessagePipe.GetSubscriber<LocalCellChangedMessage>().Subscribe(OnLocalCellChanged);
        }

        private void OnDestroy() => _subscription?.Dispose();

        private void OnLocalCellChanged(LocalCellChangedMessage msg)
        {
            _targetEntity = msg.Cell;
            _target = msg.Cell != null ? msg.Cell.transform : null;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPos = new Vector3(_target.position.x, _target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _followSpeed);

            if (_targetEntity != null)
            {
                float targetSize = _baseOrthoSize + _targetEntity.Size * _zoomPerSize;
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, Time.deltaTime * _zoomSpeed);
            }
        }
    }
}
