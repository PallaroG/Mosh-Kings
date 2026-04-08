using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Prefab do inimigo. Precisa conter Enemy.cs e NavMeshAgent.")]
    public Enemy enemyPrefab;

    [Tooltip("Centro do círculo de spawn. Se null, usa o próprio spawner.")]
    public Transform spawnCenter;

    [Header("Spawn em Círculo")]
    [Tooltip("Raio do círculo. Inimigos nascem na borda (circunferência).")]
    public float spawnRadius = 20f;

    [Tooltip("Tempo entre spawns (segundos).")]
    public float spawnInterval = 2f;

    [Header("NavMesh")]
    [Tooltip("Distância máxima para encontrar ponto válido no NavMesh próximo ao ponto sorteado.")]
    public float navMeshSampleMaxDistance = 4f;

    [Header("Pooling")]
    [Tooltip("Quantidade inicial de objetos no pool.")]
    public int poolDefaultCapacity = 20;

    [Tooltip("Quantidade máxima que o pool mantém.")]
    public int poolMaxSize = 100;

    [Tooltip("Se true, gera automaticamente em loop.")]
    public bool autoSpawn = true;

    private IObjectPool<Enemy> pool;
    private float timer;

    private void Awake()
    {
        pool = new ObjectPool<Enemy>(
            createFunc: CreateEnemy,
            actionOnGet: OnGetEnemy,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: false, // em produção pode deixar false para desempenho
            defaultCapacity: poolDefaultCapacity,
            maxSize: poolMaxSize
        );
    }

    private void Update()
    {
        if (!autoSpawn || enemyPrefab == null) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            TrySpawnEnemy();
        }
    }

    /// <summary>
    /// Permite spawn manual via outros sistemas.
    /// </summary>
    public bool TrySpawnEnemy()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Vector3 desired = GetPointOnCircleEdge(center, spawnRadius);

        // Garante posição válida no NavMesh
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
        {
            Enemy enemy = pool.Get();
            enemy.transform.position = hit.position;
            enemy.transform.rotation = Quaternion.LookRotation((center - hit.position).normalized, Vector3.up);
            enemy.SetPool(pool); // garante referência do pool no inimigo
            return true;
        }

        // Se não encontrou navmesh perto do ponto, falha esse ciclo de spawn.
        return false;
    }

    private Vector3 GetPointOnCircleEdge(Vector3 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        return new Vector3(center.x + x, center.y, center.z + z);
    }

    private Enemy CreateEnemy()
    {
        Enemy enemyInstance = Instantiate(enemyPrefab);
        enemyInstance.SetPool(pool);
        return enemyInstance;
    }

    private void OnGetEnemy(Enemy enemy)
    {
        enemy.gameObject.SetActive(true);
    }

    private void OnReleaseEnemy(Enemy enemy)
    {
        enemy.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(Enemy enemy)
    {
        if (enemy != null)
            Destroy(enemy.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, spawnRadius);
    }
}