using UnityEngine;

/// <summary>
/// 落下したら最後のチェックポイントに即復帰させる。
/// 展示ではイライラさせないことが大事なので、待ち時間なしで戻す。
/// Playerにアタッチする。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("この高さより下に落ちたらリスポーン")]
    [SerializeField] float killHeight = -10f;

    PlayerController player;
    Vector3 respawnPosition;
    Quaternion respawnRotation;

    public int FallCount { get; private set; }

    void Awake()
    {
        player = GetComponent<PlayerController>();
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    void Update()
    {
        if (transform.position.y < killHeight) Respawn();
    }

    public void SetCheckpoint(Transform point)
    {
        respawnPosition = point.position;
        respawnRotation = point.rotation;
    }

    public void Respawn()
    {
        FallCount++;
        player.Teleport(respawnPosition, respawnRotation);
    }
}
