using UnityEngine;
using Mobs;

namespace Environment
{
    /// <summary>
    /// A gate that instructs friendly mobs to stop moving forward and instead follow a specific Transform.
    /// Useful for guiding mobs to a specific target or path.
    /// Operates without physics using GateBase AABB collision.
    /// </summary>
    public class FollowGate : GateBase
    {
        [Header("Follow Settings")]
        [Tooltip("The transform that the mobs will follow when they pass through this gate.")]
        public Transform targetTransform;

        [Tooltip("Optional custom speed for the mob while following. Leave at 0 to use the mob's normal walking speed.")]
        public float followSpeed = 0f;

        protected override void OnMobEntered(Mob mob)
        {
            // Only affect friendly mobs (including player's big mobs)
            if (mob.IsEnemy) return;

            if (targetTransform != null)
            {
                mob.StartFollowing(targetTransform, followSpeed);
            }
        }
    }
}
