using System.Collections.Generic;
using UnityEngine;
using Mobs;

namespace Environment
{
    /// <summary>
    /// A pipe that teleports mobs from start to end, playing a visual circle animation.
    /// Operates without physics using GateBase AABB collision.
    /// </summary>
    public class PipeGate : GateBase
    {
        [Header("Pipe Settings")]
        [Tooltip("The spawner reference to spawn mobs at the exit")]
        public MobSpawner mobSpawner;
        
        [Tooltip("The visual prefab of the circle/ring that travels the pipe")]
        public GameObject circlePrefab;
        
        [Tooltip("Where the circle animation starts (usually inside the pipe_3 start)")]
        public Transform pipeStartPoint;
        
        [Tooltip("Where the circle animation ends and the mob pops out")]
        public Transform pipeEndPoint;

        [Tooltip("How long it takes the circle to travel the pipe (in seconds)")]
        public float travelTime = 1.0f;

        // Simple class to track active circles
        private class ActiveCircle
        {
            public GameObject visual;
            public float timer;
        }

        private List<ActiveCircle> _activeCircles = new List<ActiveCircle>();

        protected override void Update()
        {
            // Critical: call base so GateBase can do the AABB checks!
            base.Update();

            // Animate any circles currently traveling through the pipe
            for (int i = _activeCircles.Count - 1; i >= 0; i--)
            {
                var circle = _activeCircles[i];
                circle.timer += Time.deltaTime;
                
                // t goes from 0 to 1 over the travel time
                float t = circle.timer / travelTime;
                
                if (t >= 1f)
                {
                    // Reached the end!
                    // 1. Spawn the mob at the end point
                    if (mobSpawner != null)
                    {
                        // Spawn at the exit without the cannon speed boost
                        Mob newMob = mobSpawner.SpawnMob(pipeEndPoint.position, mobSpawner.mobSpeed, applyBoost: false);
                        if (newMob != null)
                        {
                            // In case the end point is still somehow inside the gate bounds,
                            // tell the gate to ignore this mob so it doesn't get sucked back in!
                            IgnoreMob(newMob);
                        }
                    }

                    // 2. Destroy the circle visual and remove from tracking
                    Destroy(circle.visual);
                    _activeCircles.RemoveAt(i);
                }
                else
                {
                    // Move the circle along the pipe
                    if (circle.visual != null)
                    {
                        circle.visual.transform.position = Vector3.Lerp(pipeStartPoint.position, pipeEndPoint.position, t);
                    }
                }
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            // 1. The mob disappears into the pipe (recycle it to the pool)
            mob.Recycle();

            // 2. Spawn a circle visual to represent the mob traveling inside
            if (circlePrefab != null && pipeStartPoint != null && pipeEndPoint != null)
            {
                GameObject newCircle = Instantiate(circlePrefab, pipeStartPoint.position, pipeStartPoint.rotation);
                _activeCircles.Add(new ActiveCircle 
                { 
                    visual = newCircle, 
                    timer = 0f 
                });
            }
        }
    }
}
