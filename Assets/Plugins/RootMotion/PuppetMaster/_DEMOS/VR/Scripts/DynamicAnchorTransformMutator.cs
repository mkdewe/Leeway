using RootMotion.Demos;
using UnityEngine;
using Zinnia.Action;
using Zinnia.Tracking.Follow;

[RequireComponent(typeof(DynamicAnchor))]
public class DynamicAnchorTransformMutator : MonoBehaviour
{
    [SerializeField] private GameObject playAreaAlias;
    [SerializeField] private Vector2Action moveAction;
    [SerializeField] private Vector2Action rotationAction;
    private DynamicAnchor dynamicAnchor;
    private ObjectFollower playAreaFollower;

    private void Awake()
    {
        dynamicAnchor = GetComponent<DynamicAnchor>();
        playAreaFollower = playAreaAlias.GetComponent<ObjectFollower>();
    }

    private void OnEnable()
    {
        playAreaFollower.FollowModifier.PositionModifier.Modified.AddListener(MutateAnchor);
        playAreaFollower.FollowModifier.RotationModifier.Modified.AddListener(MutateAnchor);
    }

    private void OnDisable()
    {
        playAreaFollower.FollowModifier.PositionModifier.Modified.RemoveListener(MutateAnchor);
        playAreaFollower.FollowModifier.RotationModifier.Modified.RemoveListener(MutateAnchor);
    }

    public void MutateAnchor(ObjectFollower.EventData args)
    {
        if (moveAction.Value.x <= 0.1 && moveAction.Value.x >= -0.1 &&
            moveAction.Value.y <= 0.1 && moveAction.Value.y >= -0.1 &&
            rotationAction.Value.x <= 0.1 && rotationAction.Value.x >= -0.1 &&
            rotationAction.Value.y <= 0.1 && rotationAction.Value.y >= -0.1) return;

        dynamicAnchor.OnRecenter();
    }
}
