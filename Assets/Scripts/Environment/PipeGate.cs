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
        
        [Tooltip("The sequence of points the circle will follow. Place at least 2 points (Start and End). Add more to create a smooth curved path.")]
        public List<Transform> pathPoints = new List<Transform>();

        [Tooltip("How long it takes the circle to travel the pipe (in seconds)")]
        public float travelTime = 1.0f;

        [Tooltip("Fixed X rotation of the circle so it faces the camera as it travels. 50 is a good starting point.")]
        public float cameraFacingXRotation = 50f;

        // Simple class to track active circles
        private class ActiveCircle
        {
            public GameObject visual;
            public float timer;
            public bool isBigMob;
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
                    if (mobSpawner != null && pathPoints.Count > 0)
                    {
                        Transform endPoint = pathPoints[pathPoints.Count - 1];
                        // Spawn at the exit without the cannon speed boost
                        // Preserve the type (Normal or Big)
                        Mob newMob = circle.isBigMob
                            ? mobSpawner.SpawnBigMob(endPoint.position, applyBoost: false)
                            : mobSpawner.SpawnMob(endPoint.position, mobSpawner.mobSpeed, applyBoost: false);

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
                    // Move the circle along the pipe path
                    if (circle.visual != null && pathPoints.Count >= 2)
                    {
                        circle.visual.transform.position = GetPathPosition(t);
                        // Fixed rotation — always faces the camera at the same angle
                        circle.visual.transform.rotation = Quaternion.Euler(cameraFacingXRotation, 0f, 0f);
                    }
                }
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            // Capture if it was a big mob before recycling
            bool isBig = mob.IsBigMob;

            // 1. The mob disappears into the pipe (recycle it to the pool)
            mob.Recycle();

            // 2. Spawn a circle visual to represent the mob traveling inside
            if (circlePrefab != null && pathPoints.Count >= 2)
            {
                GameObject newCircle = Instantiate(circlePrefab, pathPoints[0].position, pathPoints[0].rotation);
                _activeCircles.Add(new ActiveCircle 
                { 
                    visual = newCircle, 
                    timer = 0f,
                    isBigMob = isBig
                });
            }
        }

        /// <summary>
        /// Calculates a smooth Catmull-Rom spline position across all points in pathPoints.
        /// </summary>
        private Vector3 GetPathPosition(float t)
        {
            if (pathPoints.Count == 0) return transform.position;
            if (pathPoints.Count == 1) return pathPoints[0].position;
            if (pathPoints.Count == 2) return Vector3.Lerp(pathPoints[0].position, pathPoints[1].position, t);

            // Clamp t
            t = Mathf.Clamp01(t);
            if (t >= 1f) return pathPoints[pathPoints.Count - 1].position;

            // Number of curve segments
            int numSections = pathPoints.Count - 1;
            int currPt = Mathf.FloorToInt(t * numSections);
            float u = (t * numSections) - currPt;

            // The 4 points for Catmull-Rom
            Vector3 p0 = pathPoints[Mathf.Max(currPt - 1, 0)].position;
            Vector3 p1 = pathPoints[currPt].position;
            Vector3 p2 = pathPoints[Mathf.Min(currPt + 1, pathPoints.Count - 1)].position;
            Vector3 p3 = pathPoints[Mathf.Min(currPt + 2, pathPoints.Count - 1)].position;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) +
                (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u)
            );
        }

        /// <summary>
        /// Fixed rotation — kept for potential future use. Currently unused in favour of cameraFacingXRotation.
        /// </summary>
        private Quaternion GetPathRotation(float t)
        {
            return Quaternion.Euler(cameraFacingXRotation, 0f, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (pathPoints == null || pathPoints.Count < 2) return;

            // Draw the smooth curve
            Gizmos.color = Color.cyan;
            Vector3 prevPos = pathPoints[0] != null ? pathPoints[0].position : transform.position;
            
            int segments = pathPoints.Count * 10;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 currentPos = GetPathPosition(t);
                Gizmos.DrawLine(prevPos, currentPos);
                prevPos = currentPos;
            }

            // Draw the control points
            Gizmos.color = Color.yellow;
            for (int i = 0; i < pathPoints.Count; i++)
            {
                var pt = pathPoints[i];
                if (pt != null)
                {
                    Gizmos.DrawSphere(pt.position, 0.2f);
                    
                    // Draw faint straight lines between points for reference
                    if (i > 0 && pathPoints[i - 1] != null)
                    {
                        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); 
                        Gizmos.DrawLine(pathPoints[i - 1].position, pt.position);
                        Gizmos.color = Color.yellow; 
                    }
                }
            }
        }
#endif
    }
}
