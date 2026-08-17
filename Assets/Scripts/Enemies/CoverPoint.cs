using System.Collections.Generic;
using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    [Header("Cover Properties")]
    public float coverRadius = 1.0f;
    public bool isOccupied = false;
    public EnemyController currentOccupant;

    private static readonly List<CoverPoint> allCoverPoints = new List<CoverPoint>();
    public static IReadOnlyList<CoverPoint> AllCoverPoints => allCoverPoints;

    private void OnEnable()
    {
        if (!allCoverPoints.Contains(this))
        {
            allCoverPoints.Add(this);
        }
    }

    private void OnDisable()
    {
        allCoverPoints.Remove(this);
        if (currentOccupant != null)
        {
            currentOccupant = null;
            isOccupied = false;
        }
    }

    public bool Claim(EnemyController enemy)
    {
        if (isOccupied && currentOccupant != enemy) return false;

        isOccupied = true;
        currentOccupant = enemy;
        return true;
    }

    public void Release(EnemyController enemy)
    {
        if (currentOccupant == enemy)
        {
            currentOccupant = null;
            isOccupied = false;
        }
    }

    public static CoverPoint FindBestCoverPoint(Vector3 enemyPos, Vector3 playerPos, float maxDistance = 25f)
    {
        CoverPoint bestPoint = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < allCoverPoints.Count; i++)
        {
            var point = allCoverPoints[i];
            if (point == null || !point.gameObject.activeInHierarchy || (point.isOccupied && point.currentOccupant != null))
                continue;

            float distToEnemy = Vector3.Distance(enemyPos, point.transform.position);
            if (distToEnemy > maxDistance) continue;

            float distToPlayer = Vector3.Distance(playerPos, point.transform.position);
            if (distToPlayer < 4.0f) continue; // Don't take cover right next to player

            // Check line of sight from cover to player
            Vector3 dirToPlayer = (playerPos + Vector3.up * 1.2f) - (point.transform.position + Vector3.up * 1.2f);
            bool canSeePlayer = !Physics.Raycast(point.transform.position + Vector3.up * 1.2f, dirToPlayer.normalized, dirToPlayer.magnitude, LayerMask.GetMask("Default", "Environment", "Walls"));

            // Higher score for closer to enemy, reasonable distance from player, and clear line of sight to fire
            float score = -distToEnemy + (distToPlayer * 0.5f) + (canSeePlayer ? 15f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        return bestPoint;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, coverRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
    }
}
