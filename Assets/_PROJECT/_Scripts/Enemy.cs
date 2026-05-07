using System.Collections;
using UnityEngine;
using UnityEngine.AI; // Necessário para o NavMeshAgent
using UnityEngine.Pool;

public class Enemy : MonoBehaviour, IDamageable, IImpactReceiver
{
    [Header("Status do Punk")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("IA e Navegação")]
    public NavMeshAgent agent;
    private Transform playerTarget;

    [Header("Impacto")]
    [SerializeField] private float impactDeceleration = 18f;
    [SerializeField] private float maxImpactSpeed = 8f;
    [SerializeField] private float impactStopThreshold = 0.05f;
    private Vector3 externalImpactVelocity;

    [Header("Feedback Visual")]
    [Tooltip("Arraste o SpriteRenderer do inimigo aqui para ele piscar ao tomar dano.")]
    public SpriteRenderer spriteRenderer;
    public Color damageColor = Color.red;
    private Color originalColor;

    // Referência do Object Pool enviada pelo EnemySpawner
    private IObjectPool<Enemy> _pool;

    public void SetPool(IObjectPool<Enemy> pool)
    {
        _pool = pool;
    }

    private void Awake()
    {
        // Pega automaticamente o NavMeshAgent anexado ao Punk
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        // Procura automaticamente o Player na cena (O seu Player precisa ter a tag "Player"!)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTarget = player.transform;
        }
        else
        {
            Debug.LogWarning("<color=yellow>[Enemy]</color> O inimigo não achou o Player! Certifique-se de que o objeto do jogador tem a tag 'Player'.");
        }
    }

    private void OnEnable()
    {
        // Sempre que o inimigo for "puxado" do pool, a vida volta ao máximo
        currentHealth = maxHealth;

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Reativa a movimentação caso tenha sido desativada ao morrer
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    private void Update()
    {
        // O Cérebro da Roda Punk: persegue o jogador incessantemente!
        if (agent != null && agent.isActiveAndEnabled && playerTarget != null)
        {
            agent.SetDestination(playerTarget.position);
        }

        UpdateImpactMovement();
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        // Feedback visual do soco
        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashDamageRoutine());
        }

        // Checa se o punk foi nocauteado
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ReceiveImpact(ImpactData impact)
    {
        Vector3 direction = impact.direction;

        if (direction.sqrMagnitude < 0.0001f && impact.sourcePosition != Vector3.zero)
            direction = transform.position - impact.sourcePosition;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || impact.force <= 0f) return;

        direction.Normalize();
        float speedLimit = impact.maxExternalSpeed > 0f ? impact.maxExternalSpeed : maxImpactSpeed;
        externalImpactVelocity += direction * impact.force;
        externalImpactVelocity = Vector3.ClampMagnitude(externalImpactVelocity, speedLimit);
    }

    private void UpdateImpactMovement()
    {
        if (externalImpactVelocity.sqrMagnitude <= impactStopThreshold * impactStopThreshold)
        {
            externalImpactVelocity = Vector3.zero;
            return;
        }

        Vector3 displacement = externalImpactVelocity * Time.deltaTime;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.Move(displacement);
        else
            transform.position += displacement;

        externalImpactVelocity = Vector3.MoveTowards(
            externalImpactVelocity,
            Vector3.zero,
            impactDeceleration * Time.deltaTime);
    }

    private IEnumerator FlashDamageRoutine()
    {
        spriteRenderer.color = damageColor;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor;
    }

    private void Die()
    {
        // Para a movimentação ao morrer
        if (agent != null)
        {
            agent.isStopped = true;
        }

        // Devolvemos este inimigo para o Spawner reciclar
        if (_pool != null)
        {
            _pool.Release(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
