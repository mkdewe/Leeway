using RootMotion.Demos;
using UnityEngine;

public class DynamicAnchorMassBoost : MonoBehaviour
{
    [SerializeField] private DynamicAnchor leftHandDynamicAnchor;
    [SerializeField] private DynamicAnchor rightHandDynamicAnchor;
    [SerializeField] private float boost = 5f;

    private void OnEnable()
    {
        leftHandDynamicAnchor.mass *= boost;
        leftHandDynamicAnchor.velocityMassAdd *= boost;
        rightHandDynamicAnchor.mass *= boost;
        rightHandDynamicAnchor.velocityMassAdd *= boost;
    }

    private void OnDisable()
    {
        leftHandDynamicAnchor.MaximizeMass();
        rightHandDynamicAnchor.MaximizeMass();
    }
}
