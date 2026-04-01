using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Opcional: se definido, usa este alvo em vez de buscar por tag Player.")]
    public Transform targetOverride;

    [Header("Comportamento")]
    [Tooltip("Com que frequência atualiza destino do agente.")]
    public float repathInterval = 0.2f;

    [Tooltip("Vida simples para exemplo.")]
    public int maxHealth = 100;

    private int currentHealth;
    private NavMeshAgent agent;
    private Transform target;
    private IObjectPool<Enemy> pool;
    private float repathTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// Chamado pelo spawner após criar/obter objeto do pool.
    /// </summary>
    public void SetPool(IObjectPool<Enemy> objectPool)
    {
        pool = objectPool;
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        repathTimer = 0f;

        ResolveTarget();

        // Garante estado limpo ao reusar do pool
        if (agent != null)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
    }

    private void Update()
    {
        if (target == null || agent == null || !agent.isOnNavMesh) return;

        repathTimer += Time.deltaTime;
        if (repathTimer >= repathInterval)
        {
            repathTimer = 0f;
            agent.SetDestination(target.position);
        }
    }

    private void ResolveTarget()
    {
        if (targetOverride != null)
        {
            target = targetOverride;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        target = player != null ? player.transform : null;
    }

    /// <summary>
    /// Exemplo de dano.
    /// </summary>
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// Em vez de destruir, devolve para o pool.
    /// </summary>
    public void Die()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (pool != null)
            pool.Release(this);
        else
            gameObject.SetActive(false); // fallback de segurança
    }
}