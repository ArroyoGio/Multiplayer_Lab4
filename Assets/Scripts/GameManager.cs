using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private static GameManager instance;

    [SerializeField] private Transform playerPrefab;

    // Distancia entre jugadores al aparecer, para que no nazcan
    // uno encima de otro y se puedan ver al pelear.
    [SerializeField] private float spawnSpacing = 3f;

    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        
    }
    public override void OnNetworkSpawn()
    {
        print(NetworkManager.Singleton.LocalClientId);
        InstancePlayerRpc();
    }

    // =========================================================
    // CLIENTE -> SERVIDOR : "QUIERO UN JUGADOR" (punto 3)
    //
    // El cliente no dice de quién es el jugador: lo decide el
    // servidor a partir de quién mandó el mensaje.
    // =========================================================
    [Rpc(SendTo.Server)]
    public void InstancePlayerRpc(RpcParams rpcParams = default)
    {
        ulong ownerId = rpcParams.Receive.SenderClientId;

        Transform player = Instantiate(playerPrefab);

        player.position = GetSpawnPosition(ownerId);

        // player.GetComponent<NetworkObject>().Spawn(true);
        player.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId, true);
    }

    // Punto de aparición de un jugador concreto.
    public Vector3 GetSpawnPosition(ulong clientId)
    {
        if (playerPrefab == null) return Vector3.zero;

        Vector3 basePosition = playerPrefab.position;

        // Colocamos a los clientes en fila, usando el id como índice.
        float offset = (clientId % 5) * spawnSpacing - spawnSpacing * 2f;

        return basePosition + Vector3.right * offset;
    }

    void Update()
    {
        
    }
    public static GameManager Instance => instance;
}
