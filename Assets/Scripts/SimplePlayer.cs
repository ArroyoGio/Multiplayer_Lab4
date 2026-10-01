using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class SimplePlayer : NetworkBehaviour
{
    // =========================================================
    // MOVIMIENTO (esto ya funcionaba, se conserva tal cual)
    // =========================================================

    public float speed;

    // =========================================================
    // CONFIGURACIÓN DEL COMBATE
    // =========================================================

    // Vida inicial y máxima. Solo la usa el servidor.
    public int maxHealth = 100;

    public int attackDamage = 34;
    public float attackRange = 2.5f;
    public float attackCooldown = 0.8f;
    public float respawnDelay = 3f;
    public KeyCode attackKey = KeyCode.Space;
    public bool attackWithLeftMouseButton = true;

    // Identificador reservado para "no hay objetivo".
    // Netcode no usa el 0 como NetworkObjectId.
    private const ulong NoTarget = 0;

    private static readonly Color DeadColor = new Color(0.22f, 0.22f, 0.26f, 1f);

    // =========================================================
    // ESTADO REPLICADO (punto 2)
    //
    // ESTADO  -> controlado por el servidor.
    // Todos los clientes LEEEN, solo el servidor ESCRIBE.
    // =========================================================

    public NetworkVariable<int> health = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private double nextAttackServerTime;
    private float nextAttackClientTime;
    private Vector3 initialPosition;
    private MeshRenderer bodyRenderer;
    private Color aliveColor;
    private int deadVisualState = -1;
    private Coroutine respawnRoutine;

    // =========================================================
    // ESTADO DERIVADO (punto 4)
    //
    // IsDead NO es una NetworkVariable: se CALCULA desde health.
    // Si lo guardásemos, sería un estado redundante que podría
    // desincronizarse de la vida.
    // =========================================================

    public bool IsDead => health.Value <= 0;

    public float HealthRatio => maxHealth <= 0
        ? 0f
        : Mathf.Clamp01(health.Value / (float)maxHealth);


    private void Awake()
    {
        initialPosition = transform.position;

        bodyRenderer = GetComponentInChildren<MeshRenderer>();

        if (bodyRenderer != null)
        {
            aliveColor = bodyRenderer.material.color;
        }
    }


    // =========================================================
    // ENTRADA DEL DUEÑO
    // =========================================================

    void Update()
    {
        if (!IsOwner) return;

        // El dueño SOLO PIDE atacar. El servidor decide si impacta.
        if (WantsToAttack())
        {
            RequestAttack();
        }

        // Si estamos muertos no hay movimiento (punto 4).
        // El servidor también lo comprueba en el RPC de movimiento.
        if (IsDead) return;

        if (Input.GetAxisRaw("Horizontal") == 0 && Input.GetAxisRaw("Vertical") == 0) return;

        float x = Input.GetAxisRaw("Horizontal") * speed * Time.deltaTime;
        float y = Input.GetAxisRaw("Vertical") * speed * Time.deltaTime;
        ValideteMoventRpc(x, y);
    }

    private bool WantsToAttack()
    {
        if (Time.time < nextAttackClientTime) return false;

        if (Input.GetKeyDown(attackKey)) return true;

        return attackWithLeftMouseButton && Input.GetMouseButtonDown(0);
    }


    // =========================================================
    // GOLPEAR (el cliente solo propone, nunca decide)
    // =========================================================

    private void RequestAttack()
    {
        // No enviamos más peticiones de las necesarias.
        // El servidor vuelve a cronometrar por su cuenta: no confía en esto.
        nextAttackClientTime = Time.time + attackCooldown;

        // El cliente SOLO dice a QUIÉN quiere golpear.
        // No calcula daño, no toca la vida de nadie.
        RequestAttackRpc(FindCandidateTargetId());

        // Feedback inmediato del golpe. Es local y no fiable:
        // es decoración, no información de juego.
        CombatVfx.PlaySwing(transform.position);
    }

    private ulong FindCandidateTargetId()
    {
        if (NetworkManager.Singleton == null) return NoTarget;

        Vector3 aimPoint = GetAimPoint();

        ulong bestId = NoTarget;
        float bestDistance = float.MaxValue;

        foreach (NetworkObject netObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            SimplePlayer other = netObject.GetComponent<SimplePlayer>();

            if (other == null || other == this) continue;
            if (!other.IsSpawned || other.IsDead) continue;

            if (Vector3.Distance(transform.position, other.transform.position) > attackRange) continue;

            // De los que están en rango nos quedamos con el más
            // cercano al punto que apunta el ratón.
            float distanceToAim = Vector3.Distance(
                aimPoint,
                other.transform.position + Vector3.up
            );

            if (distanceToAim < bestDistance)
            {
                bestDistance = distanceToAim;
                bestId = other.NetworkObjectId;
            }
        }

        return bestId;
    }

    private Vector3 GetAimPoint()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return transform.position + transform.forward * attackRange;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.point;
        }

        // Si el ratón no apunta a nada, usamos el suelo.
        Plane groundPlane = new Plane(
            Vector3.up,
            new Vector3(0f, transform.position.y, 0f)
        );

        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position + transform.forward * attackRange;
    }


    // =========================================================
    // CLIENTE -> SERVIDOR : "QUIERO ATACAR" (punto 3)
    //
    // El cliente pide, el servidor valida y el servidor aplica el daño.
    // =========================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ValideteMoventRpc(float x, float y)
    {
        // Aunque el cliente pida moverse, el servidor manda:
        // un jugador muerto no se mueve.
        if (IsDead) return;

        transform.position += new Vector3(x, 0, y);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
    private void RequestAttackRpc(ulong targetNetworkObjectId)
    {
        // ---------- VALIDACIÓN DEL SERVIDOR ----------
        // No nos fiamos de nada de lo que diga el cliente.

        // 1. ¿El atacante sigue vivo?
        if (IsDead) return;

        // 2. ¿Respeta el tiempo de espera? Lo cronometra el servidor.
        double serverTime = NetworkManager.ServerTime.Time;

        if (serverTime < nextAttackServerTime) return;

        nextAttackServerTime = serverTime + attackCooldown;

        // 3. El cliente no ha visto a nadie: golpe al aire.
        if (targetNetworkObjectId == NoTarget) return;

        // 4. ¿Ese objetivo existe de verdad?
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject targetObject))
        {
            return;
        }

        SimplePlayer target = targetObject.GetComponent<SimplePlayer>();

        // 5. Solo nos peleamos con otros jugadores.
        if (target == null || target == this) return;

        // 6. ¿El objetivo sigue vivo?
        if (target.IsDead) return;

        Vector3 attackerCenter = transform.position + Vector3.up;
        Vector3 targetCenter = target.transform.position + Vector3.up;

        // 7. ¿Está en rango? Lo medimos con la posición del SERVIDOR.
        if (Vector3.Distance(attackerCenter, targetCenter) > attackRange) return;

        // 8. ¿El golpe impacta de verdad? Lo comprobamos con la física
        //    del servidor, no con la del cliente.
        Collider targetCollider = target.GetComponentInChildren<Collider>();

        if (targetCollider == null) return;
        if (!IsInsideAttackSphere(attackerCenter, targetCollider)) return;

        // ---------- EL SERVIDOR APLICA EL DAÑO ----------

        Vector3 hitPoint = targetCollider.ClosestPoint(attackerCenter);

        target.ApplyDamage(attackDamage);

        // ---------- EVENTOS DE ACOMPAÑAMIENTO (punto 5) ----------
        // Se mandan sobre el objeto del objetivo y son NO FIABLES:
        // si se pierde alguno, no se rompe nada.

        target.PlayHitEffectRpc(hitPoint, attackDamage);

        if (target.IsDead)
        {
            target.PlayDeathEffectRpc(targetCenter);
        }
    }

    private bool IsInsideAttackSphere(Vector3 center, Collider targetCollider)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            center,
            attackRange,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i] == targetCollider) return true;
        }

        return false;
    }


    // =========================================================
    // DAÑO : SOLO EL SERVIDOR (punto 1 y punto 3)
    //
    // La vida SOLO cambia aquí, y solo si IsServer.
    // =========================================================

    private void ApplyDamage(int amount)
    {
        // Cinturón de seguridad: nadie más que el servidor toca la vida.
        if (!IsServer) return;
        if (IsDead) return;
        if (amount <= 0) return;

        // Escribir en la NetworkVariable es lo que replica la vida
        // a todos los clientes. No hay ninguna otra forma de cambiarla.
        health.Value = Mathf.Max(0, health.Value - amount);

        if (IsDead)
        {
            StartRespawnCountdown();
        }
    }


    // =========================================================
    // REAPARICIÓN (la decide y la aplica el servidor)
    // =========================================================

    private void StartRespawnCountdown()
    {
        if (respawnRoutine != null) StopCoroutine(respawnRoutine);

        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Puede que el objeto ya no esté en la red.
        if (!IsSpawned || !IsServer) yield break;

        respawnRoutine = null;

        // La vida vuelve al máximo. Como es una NetworkVariable,
        // el cambio se replica solo y en todas las máquinas
        // IsDead vuelve a ser false porque es un estado derivado.
        health.Value = maxHealth;

        transform.position = GetRespawnPoint();
    }

    private Vector3 GetRespawnPoint()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetSpawnPosition(OwnerClientId);
        }

        return initialPosition;
    }


    // =========================================================
    // ESTADO DERIVADO : CAMBIO DE VIDA (punto 2 y punto 4)
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // Vida inicial: la escribe el servidor (punto 2).
        if (IsServer)
        {
            health.Value = maxHealth;
        }

        // Nos suscribimos a los cambios de la vida (punto 2).
        health.OnValueChanged += OnHealthChanged;

        // Un jugador que entra tarde recibe el valor actual sin que
        // se dispare OnValueChanged, así que aplicamos el estado
        // a mano (igual que hace TreeNode).
        ApplyDerivedState();
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int previous, int current)
    {
        // Llega a TODAS las máquinas porque la vida se replica.
        // Aquí no se decide nada: solo se refleja el cambio.
        ApplyDerivedState();
    }

    private void ApplyDerivedState()
    {
        bool dead = IsDead;

        int newVisualState = dead ? 1 : 0;

        if (deadVisualState == newVisualState) return;

        deadVisualState = newVisualState;

        // Efecto visual derivado. Solo pintamos:
        // ni la vida ni el daño se tocan aquí.
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = dead ? DeadColor : aliveColor;
        }
    }


    // =========================================================
    // EVENTOS DE ACOMPAÑAMIENTO (punto 5)
    //
    // Son NO FIABLES: son decoración (sonido, partículas, daño
    // flotante). Si se pierde algún paquete no pasa nada y
    // NUNCA controlan la vida.
    // =========================================================

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void PlayHitEffectRpc(Vector3 hitPoint, int damageAmount)
    {
        CombatVfx.PlayImpact(hitPoint);

        FloatingDamageText.Spawn(
            hitPoint + Vector3.up * 0.4f,
            damageAmount,
            CombatVfx.DamageNumberColor
        );
    }

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void PlayDeathEffectRpc(Vector3 position)
    {
        CombatVfx.PlayDeath(position);
    }
}
