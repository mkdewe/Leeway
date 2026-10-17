using UnityEngine;
namespace Leeway.Creature.Domain
{
    public static class CreatureSprintRules
    {
        public static float Speed(float walkingSpeed,bool sprint,bool grounded,bool hasLegs,float multiplier)
        {
            float factor=sprint&&grounded&&hasLegs?Mathf.Clamp(multiplier,1,2):1;
            return Mathf.Max(0,walkingSpeed)*factor;
        }
    }
}
