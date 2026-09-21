using Leeway.CreatureEditor;
using UnityEngine;

namespace Leeway.Combat
{
    /// <summary>
    /// A blow landed. Published on every machine that can see it, so the interface and the effects
    /// can react without any of them having to know about the combat code.
    /// </summary>
    /// <remarks>
    /// The damage is carried in the message rather than being read back off the body: hit points are
    /// a SyncVar and arrive on their own schedule, so a listener reading them would sometimes report
    /// the blow before last.
    /// </remarks>
    public readonly struct CreatureStruckMessage
    {
        /// <summary>Who struck. Null on a client that cannot see the attacker.</summary>
        public readonly CreatureBody Attacker;

        public readonly CreatureBody Target;
        public readonly float Damage;
        public readonly Vector3 Point;

        /// <summary>Whether the blow was heavy enough to put the target on the ground.</summary>
        public readonly bool KnockedDown;

        public CreatureStruckMessage(CreatureBody attacker, CreatureBody target, float damage, Vector3 point, bool knockedDown)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
            Point = point;
            KnockedDown = knockedDown;
        }
    }

    /// <summary>A creature ran out of hit points.</summary>
    public readonly struct CreatureDiedMessage
    {
        public readonly CreatureBody Body;

        /// <summary>Who killed it, where that is known.</summary>
        public readonly CreatureBody Killer;

        public CreatureDiedMessage(CreatureBody body, CreatureBody killer)
        {
            Body = body;
            Killer = killer;
        }
    }
}
