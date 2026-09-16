using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class UtilityAgent : MonoBehaviour
{
    public enum ActionType
    {
        Attack,
        Flee,
        SeekResource
    }

    [Header("Estado del NPC")]
    [Range(0f, 100f)]
    public float health = 75f;

    public float maxHealth = 100f;

    [Header("Objetos del escenario")]
    public Transform enemy;
    public Transform healthResource;
    public Transform safePoint;

    [Header("Rangos de percepción")]
    public float enemySenseRange = 12f;
    public float resourceSenseRange = 15f;
    public float attackDistance = 1.8f;

    [Header("Pesos")]
    public float attackWeight = 1f;
    public float fleeWeight = 1f;
    public float resourceWeight = 1f;

    [Header("Frecuencia de decisión")]
    public float decisionInterval = 0.5f;

    [Header("Información para depuración")]
    public ActionType currentAction;

    [SerializeField] private float attackScore;
    [SerializeField] private float fleeScore;
    [SerializeField] private float resourceScore;

    private NavMeshAgent agent;
    private float decisionTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (enemy == null ||
            healthResource == null ||
            safePoint == null)
        {
            return;
        }

        decisionTimer -= Time.deltaTime;

        if (decisionTimer <= 0f)
        {
            EvaluateUtility();
            decisionTimer = decisionInterval;
        }

        ExecuteAction();
    }

    private void EvaluateUtility()
    {
        // Salud normalizada entre 0 y 1.
        float health01 =
            Mathf.Clamp01(health / Mathf.Max(maxHealth, 0.001f));

        // Distancias actuales.
        float enemyDistance =
            Vector3.Distance(transform.position, enemy.position);

        float resourceDistance =
            Vector3.Distance(
                transform.position,
                healthResource.position
            );

        // Convertimos distancia en proximidad.
        float enemyCloseness =
            CalculateCloseness(
                enemyDistance,
                enemySenseRange
            );

        float resourceCloseness =
            CalculateCloseness(
                resourceDistance,
                resourceSenseRange
            );

        // Entre menor sea la salud, mayor será esta necesidad.
        float needHealth = 1f - health01;

        // Funciones de utilidad.
        attackScore =
            health01 *
            enemyCloseness *
            attackWeight;

        fleeScore =
            needHealth *
            enemyCloseness *
            fleeWeight;

        resourceScore =
            needHealth *
            resourceCloseness *
            resourceWeight;

        // Por defecto comenzamos suponiendo que atacar es
        // la alternativa con mayor puntuación.
        currentAction = ActionType.Attack;
        float bestScore = attackScore;

        if (fleeScore > bestScore)
        {
            bestScore = fleeScore;
            currentAction = ActionType.Flee;
        }

        if (resourceScore > bestScore)
        {
            currentAction = ActionType.SeekResource;
        }

        Debug.Log(
            "ATAQUE: " + attackScore.ToString("F2") +
            " | HUIDA: " + fleeScore.ToString("F2") +
            " | RECURSO: " + resourceScore.ToString("F2") +
            " | ACCIÓN: " + currentAction
        );
    }

    private float CalculateCloseness(
        float distance,
        float maximumDistance)
    {
        float safeMaximum =
            Mathf.Max(maximumDistance, 0.001f);

        return 1f -
               Mathf.Clamp01(distance / safeMaximum);
    }

    private void ExecuteAction()
    {
        switch (currentAction)
        {
            case ActionType.Attack:
                Attack();
                break;

            case ActionType.Flee:
                Flee();
                break;

            case ActionType.SeekResource:
                SeekResource();
                break;
        }
    }

    private void Attack()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                enemy.position
            );

        if (distance > attackDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(enemy.position);
        }
        else
        {
            // Para esta práctica llegar al rango representa
            // la ejecución de la acción de ataque.
            agent.isStopped = true;
        }
    }

    private void Flee()
    {
        agent.isStopped = false;
        agent.SetDestination(safePoint.position);
    }

    private void SeekResource()
    {
        agent.isStopped = false;
        agent.SetDestination(healthResource.position);
    }
}