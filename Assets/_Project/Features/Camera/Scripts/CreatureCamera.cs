using System;
using Leeway.Creature;
using MessagePipe;
using Unity.Cinemachine;
using UnityEngine;

namespace Leeway.Camera
{
    [RequireComponent(typeof(CinemachineCamera))]
    public class CreatureCamera : MonoBehaviour
    {
        private CinemachineCamera _vcam;
        private IDisposable _subscription;

        private void Awake() => _vcam = GetComponent<CinemachineCamera>();

        private void Start()
        {
            if (GlobalMessagePipe.IsInitialized)
                _subscription = GlobalMessagePipe.GetSubscriber<LocalCreatureChangedMessage>().Subscribe(OnLocalCreatureChanged);
        }

        private void OnDestroy() => _subscription?.Dispose();

        private void OnLocalCreatureChanged(LocalCreatureChangedMessage msg)
        {
            Transform target = msg.Creature != null ? msg.Creature.transform : null;
            _vcam.Follow = target;
            _vcam.LookAt = target;
        }
    }
}
