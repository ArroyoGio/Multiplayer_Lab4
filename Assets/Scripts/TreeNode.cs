using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class TreeNode : NetworkBehaviour
{
    // =========================================================
    // ESTADO DEL ÁRBOL
    // =========================================================

    // Color actual del árbol.
    // Gris = libre
    // Verde = tiene dueño
    public NetworkVariable<Color> treeColor =
        new NetworkVariable<Color>(
            Color.gray,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Indica si el árbol tiene dueño.
    private NetworkVariable<bool> isClaimed =
        new NetworkVariable<bool>(false);

    // Vida / cantidad de cortes.
    public NetworkVariable<int> logs =
        new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Nombre del dueño.
    // FixedString32Bytes es adecuado para enviar
    // textos pequeños mediante Netcode.
    public NetworkVariable<FixedString32Bytes> ownerName =
        new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes("Sin reclamar"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );


    // =========================================================
    // TEXTO DEL ÁRBOL
    // =========================================================

    [SerializeField]
    private TextMeshProUGUI treeInfoText;


    // =========================================================
    // CLIC EN EL ÁRBOL
    // =========================================================

    private void OnMouseDown()
    {
        if (!IsSpawned)
            return;

        // Si está libre, cualquier jugador puede intentar reclamarlo.
        if (!isClaimed.Value)
        {
            ClaimTreeRpc();
        }

        // Si tiene dueño, solamente el dueño puede cortarlo.
        else if (IsOwner)
        {
            ChopTreeRpc();
        }

        // Otro jugador intenta tocar un árbol ocupado.
        else
        {
            Debug.Log("Te ganaron, el árbol ya tiene dueño.");
        }
    }


    // =========================================================
    // RECLAMAR EL ÁRBOL
    // =========================================================

    [Rpc(SendTo.Server)]
    private void ClaimTreeRpc(RpcParams rpcParams = default)
    {
        // El servidor valida nuevamente que esté libre.
        if (isClaimed.Value)
        {
            Debug.Log("Te ganaron, el árbol ya tiene dueño.");
            return;
        }

        // Identificamos al jugador que hizo clic.
        ulong who = rpcParams.Receive.SenderClientId;

        // El árbol queda reclamado.
        isClaimed.Value = true;

        // El servidor entrega la propiedad al jugador.
        NetworkObject.ChangeOwnership(who);

        // Cambiamos el color del árbol.
        treeColor.Value = Color.green;

        // Guardamos el nombre del dueño.
        ownerName.Value =
            new FixedString32Bytes("Player " + who);

        Debug.Log(
            "Server: El árbol ahora pertenece al jugador " + who
        );
    }


    // =========================================================
    // CORTAR EL ÁRBOL
    // =========================================================

    [Rpc(SendTo.Server)]
    private void ChopTreeRpc()
    {
        // Restamos una vida.
        logs.Value--;

        Debug.Log(
            "Corte realizado. Vida restante: " + logs.Value
        );

        // Cuando se queda sin vidas...
        if (logs.Value <= 0)
        {
            // El árbol vuelve a estar libre.
            NetworkObject.RemoveOwnership();

            isClaimed.Value = false;

            // Color de árbol libre.
            treeColor.Value = Color.gray;

            // Eliminamos el dueño.
            ownerName.Value =
                new FixedString32Bytes("Sin reclamar");

            // Reiniciamos la vida.
            logs.Value = 3;

            Debug.Log(
                "Server: Árbol agotado. Ahora está libre."
            );
        }
    }


    // =========================================================
    // CAMBIO DE COLOR
    // =========================================================

    private void OnColorChanged(
        Color previous,
        Color current)
    {
        ChangeColor();
    }


    // =========================================================
    // CAMBIO DE VIDA
    // =========================================================

    private void OnLogsChanged(
        int previous,
        int current)
    {
        Debug.Log(
            "Vida del árbol: " + current
        );

        UpdateTreeInfo();
    }


    // =========================================================
    // CAMBIO DE DUEÑO
    // =========================================================

    private void OnOwnerNameChanged(
        FixedString32Bytes previous,
        FixedString32Bytes current)
    {
        Debug.Log(
            "Nuevo dueño del árbol: " + current
        );

        UpdateTreeInfo();
    }


    // =========================================================
    // CUANDO EL ÁRBOL APARECE EN LA RED
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // Nos suscribimos a los cambios de las variables de red.
        treeColor.OnValueChanged += OnColorChanged;

        logs.OnValueChanged += OnLogsChanged;

        ownerName.OnValueChanged += OnOwnerNameChanged;

        // Aplicamos inmediatamente el estado actual.
        // Esto es importante para los jugadores que
        // entran tarde a la partida.
        ChangeColor();

        UpdateTreeInfo();

        Debug.Log(
            "Árbol sincronizado. " +
            "Color: " + treeColor.Value +
            " | Vida: " + logs.Value +
            " | Dueño: " + ownerName.Value
        );
    }


    // =========================================================
    // CUANDO EL ÁRBOL SALE DE LA RED
    // =========================================================

    public override void OnNetworkDespawn()
    {
        // Nos desuscribimos de los cambios.
        treeColor.OnValueChanged -= OnColorChanged;

        logs.OnValueChanged -= OnLogsChanged;

        ownerName.OnValueChanged -= OnOwnerNameChanged;
    }


    // =========================================================
    // CAMBIAR COLOR VISUAL
    // =========================================================

    private void ChangeColor()
    {
        Renderer renderer = GetComponent<Renderer>();

        if (renderer == null)
            return;

        renderer.material.color = treeColor.Value;
    }


    // =========================================================
    // ACTUALIZAR INFORMACIÓN SOBRE EL ÁRBOL
    // =========================================================

    private void UpdateTreeInfo()
    {
        if (treeInfoText == null)
            return;

        treeInfoText.text =
            ownerName.Value.ToString() +
            "\nVida: " +
            logs.Value;
    }
}