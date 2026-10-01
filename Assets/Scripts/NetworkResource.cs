using UnityEngine;
using Unity.Netcode;
using Sirenix.OdinInspector;

public class NetworkResource : NetworkBehaviour
{
    // Cantidad de recursos disponibles.
    public int logs = 5;

    // Color cuando el recurso est� libre.
    public Color freeColor = Color.green;

    // Color cuando el recurso tiene due�o.
    public Color takenColor = Color.red;


    // ---------------------------------------------------------
    // EL CLIENTE INTENTA RECLAMAR EL RECURSO
    // ---------------------------------------------------------

    private void OnMouseDown()
    {
        // Si el objeto todav�a no est� dentro de la red,
        // no hacemos nada.
        if (!IsSpawned)
            return;


        // Si el servidor es actualmente el due�o,
        // significa que el recurso est� libre.
        if (NetworkObject.IsOwnedByServer)
        {
            Debug.Log("Voy a reclamar el recurso.");

            // El cliente solicita al servidor
            // que le entregue el recurso.
            ClaimServerRpc();
        }
        else
        {
            Debug.Log("Ya tiene due�o.");
        }
    }


    // ---------------------------------------------------------
    // CLIENTE ? SERVIDOR
    // ---------------------------------------------------------

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClaimServerRpc(RpcParams rpcParams = default)
    {
        // El servidor vuelve a comprobar si el recurso
        // sigue libre.
        //
        // Esto es MUY IMPORTANTE:
        // no confiamos simplemente en lo que diga el cliente.

        if (!NetworkObject.IsOwnedByServer)
        {
            Debug.Log("Te ganaron, ya tiene due�o.");
            return;
        }


        // El servidor obtiene el ID del cliente
        // que hizo la petici�n.
        ulong clientId = rpcParams.Receive.SenderClientId;


        // El servidor cambia el due�o del recurso
        // y se lo asigna al cliente que lo reclam�.
        NetworkObject.ChangeOwnership(clientId);

        Debug.Log("Ahora el recurso pertenece al cliente: " + clientId);
    }


    // ---------------------------------------------------------
    // CUANDO EL DUE�O CAMBIA
    // ---------------------------------------------------------

    protected override void OnOwnershipChanged(
        ulong previous,
        ulong current)
    {
        // Actualizamos visualmente el recurso.
        ChangeColor();
    }


    // ---------------------------------------------------------
    // CUANDO EL OBJETO APARECE EN LA RED
    // ---------------------------------------------------------

    public override void OnNetworkSpawn()
    {
        // Al aparecer en la red,
        // actualizamos su color.
        ChangeColor();
    }


    // ---------------------------------------------------------
    // CAMBIAR COLOR DEL RECURSO
    // ---------------------------------------------------------

    private void ChangeColor()
    {
        // Si el servidor es due�o, est� libre.
        bool isFree = NetworkObject.IsOwnedByServer;

        // Obtenemos el Renderer del recurso.
        Renderer renderer = GetComponent<Renderer>();

        // Verde = libre
        // Rojo = ocupado
        renderer.material.color =
            isFree ? freeColor : takenColor;
    }
}