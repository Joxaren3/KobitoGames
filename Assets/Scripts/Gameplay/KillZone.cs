using UnityEngine;

/// <summary>
/// 触れたらリスポーンする範囲（床の水たまり、コンロの火など）。
/// Is Trigger を ON にした Collider と一緒に置く。
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerRespawn respawn))
        {
            respawn.Respawn();
        }
    }
}
