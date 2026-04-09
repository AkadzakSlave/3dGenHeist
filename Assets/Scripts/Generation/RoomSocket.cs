using UnityEngine;

public class RoomSocket : MonoBehaviour
{
    [HideInInspector]
    public bool isConnected = false;

    private void OnDrawGizmos()
    {
        Gizmos.color = isConnected ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}
