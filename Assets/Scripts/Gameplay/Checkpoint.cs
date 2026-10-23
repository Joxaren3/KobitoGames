using UnityEngine;

/// <summary>
/// 通過するとリスポーン地点を更新するチェックポイント。
/// Is Trigger を ON にした Collider と一緒に置く。
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerRespawn respawn))
        {
            respawn.SetCheckpoint(transform);
        }
    }
}
