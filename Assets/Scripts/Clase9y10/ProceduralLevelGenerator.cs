using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
 
public class ProceduralLevelGenerator : MonoBehaviour
{
    [Header("Cuadricula")]
    [Min(5)] public int width = 12;
    [Min(5)] public int height = 8;
    [Min(1f)] public float cellSize = 2f;
 
    [Header("Generacion")]
    [Range(0f, 0.45f)]
    public float wallProbability = 0.22f;
 
    [Min(0)]
    public int rewardCount = 5;
 
    public int seed = 12345;

    // ---- MODIFICACION: zonas de riesgo ----
    [Header("Zonas de riesgo (modificacion)")]
    [Min(0)]
    public int dangerCount = 4;
    // ------------------------------------------------

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject startPrefab;
    public GameObject goalPrefab;
    public GameObject rewardPrefab;

    // ---- MODIFICACION ----
    [Header("Prefab de riesgo (modificacion)")]
    public GameObject dangerPrefab;
    // -------------------------------

   

    // 0 = transitable, 1 = muro
    private int[,] map;

    // Se usan para NO colocar zonas de riesgo encima de la ruta.
    private readonly HashSet<Vector2Int> guaranteedPath =
        new HashSet<Vector2Int>();

    // Conservamos referencias para poder limpiar una generacion anterior.
    private readonly List<GameObject> generatedObjects =
        new List<GameObject>();
 
    private void Start()
    {
        GenerateLevel();
    }
 
    public void GenerateLevel()
    {
        if (!ValidateConfiguration())
            return;
 
        ClearGenerated();
 
        // Guardamos el estado global para no alterar otros sistemas aleatorios.
        Random.State previousState = Random.state;
        Random.InitState(seed);
 
        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int goal = new Vector2Int(width - 2, height - 2);
 
        map = new int[width, height];
 
        // PASO 1: generacion inicial del mapa.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool border =
                    x == 0 ||
                    y == 0 ||
                    x == width - 1 ||
                    y == height - 1;
 
                bool protectedCell =
                    new Vector2Int(x, y) == start ||
                    new Vector2Int(x, y) == goal;
 
                if (border)
                {
                    map[x, y] = 1;
                }
                else if (protectedCell)
                {
                    map[x, y] = 0;
                }
                else
                {
                    map[x, y] =
                        Random.value < wallProbability ? 1 : 0;
                }
            }
        }
 
        // PASO 2: restriccion de jugabilidad.
        // Abrimos un corredor en L para garantizar conectividad.
        CarveGuaranteedPath(start, goal);
 
        // PASO 3: representacion visual.
        BuildGeometry();
 
        Spawn(
            startPrefab,
            CellToWorld(start, 0.5f),
            "Start"
        );
 
        Spawn(
            goalPrefab,
            CellToWorld(goal, 0.5f),
            "Goal"
        );
 
     
       

        // PASO 4: contenido adicional se agrego una lista .
        List<Vector2Int> rewardPositions;

        int spawnedRewards = SpawnRewards(start, goal, out rewardPositions);

        // ---- MODIFICACION PROPIA: colocar zonas de riesgo ----
        // Regla nueva: solo en celdas transitables que NO son
        // parte del corredor garantizado, ni S/G, ni ya tienen reliquia.
        int spawnedDangers = SpawnDangerZones(
            start, goal, rewardPositions
        );
        // --------------------------------------------------------


        string mission =
            "Llega a la meta y recolecta " +
            spawnedRewards +
            " recompensas.";
 
        Debug.Log(
            "Semilla: " + seed +
            " | Recompensas: " + spawnedRewards +
            " | Mision: " + mission
        );
 
        // Restauramos el estado anterior de UnityEngine.Random.
        Random.state = previousState;
    }
 
    private void CarveGuaranteedPath(
        Vector2Int start,
        Vector2Int goal)
    {
        for (int x = start.x; x <= goal.x; x++)
        {
            map[x, start.y] = 0;
        }
 
        for (int y = start.y; y <= goal.y; y++)
        {
            map[goal.x, y] = 0;
        }
    }
 
    private void BuildGeometry()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
 
                Spawn(
                    floorPrefab,
                    CellToWorld(cell, 0f),
                    "Floor_" + x + "_" + y
                );
 
                if (map[x, y] == 1)
                {
                    Spawn(
                        wallPrefab,
                        CellToWorld(cell, 0.5f),
                        "Wall_" + x + "_" + y
                    );
                }
            }
        }
    }
 
    private int SpawnRewards(
        Vector2Int start,
        Vector2Int goal, out List<Vector2Int> placedPositions)
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>();
 
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
 
                if (map[x, y] == 0 &&
                    cell != start &&
                    cell != goal)
                {
                    candidates.Add(cell);
                }
            }
        }
 
        // Fisher-Yates: mezcla reproducible con la misma semilla.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
 
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }
 
        int amount =
            Mathf.Min(rewardCount, candidates.Count);
        placedPositions = new List<Vector2Int>();

        for (int i = 0; i < amount; i++)
        {
            Spawn(rewardPrefab, CellToWorld(candidates[i], 0.5f), "Reward_" + i);
            placedPositions.Add(candidates[i]);
        }
 
        return amount;
    }

    // ---- MODIFICACION ----
    // Coloca sonas de riesgo :
    // transitables, fuera del corredor garantizado, sin pisar
    // S, G ni celdas que ya tienen una reliquia.
    private int SpawnDangerZones(
        Vector2Int start,
        Vector2Int goal,
        List<Vector2Int> rewardPositions)
    {


        if (dangerPrefab == null || dangerCount <= 0)
            return 0;

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(rewardPositions);

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                bool isTransitable = map[x, y] == 0;
                bool isOnPath = guaranteedPath.Contains(cell);
                bool isProtected = cell == start || cell == goal;
                bool isOccupied = occupied.Contains(cell);

                if (isTransitable && !isOnPath && !isProtected && !isOccupied)
                {
                    candidates.Add(cell);
                }
            }
        }

        // Misma tecnica de mezcla reproducible (Fisher-Yates).
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        int amount = Mathf.Min(dangerCount, candidates.Count);

        for (int i = 0; i < amount; i++)
        {
            Spawn(dangerPrefab, CellToWorld(candidates[i], 0.5f), "Danger_" + i);
        }

        return amount;
    }
    // --------------------------------

    private Vector3 CellToWorld(
        Vector2Int cell,
        float yPosition)
    {
        return transform.position +
               new Vector3(
                   cell.x * cellSize,
                   yPosition,
                   cell.y * cellSize
               );
    }
 
    private GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        string objectName)
    {
        GameObject instance = Instantiate(
            prefab,
            position,
            Quaternion.identity,
            transform
        );
 
        instance.name = objectName;
        generatedObjects.Add(instance);
 
        return instance;
    }
 
    private void ClearGenerated()
    {
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
 
        generatedObjects.Clear();
    }
 
    private bool ValidateConfiguration()
    {
        if (width < 5 || height < 5)
        {
            Debug.LogError(
                "El mapa debe tener al menos 5 x 5 celdas."
            );
            return false;
        }
 
        if (floorPrefab == null ||
            wallPrefab == null ||
            startPrefab == null ||
            goalPrefab == null)
        {
            Debug.LogError(
                "Faltan Prefabs obligatorios en el Inspector."
            );
            return false;
        }
 
        if (rewardCount > 0 && rewardPrefab == null)
        {
            Debug.LogError(
                "Reward Prefab es obligatorio cuando rewardCount > 0."
            );
            return false;
        }
        // ---- MODIFICACION PROPIA ----
        if (dangerCount > 0 && dangerPrefab == null)
        {
            Debug.LogError("Danger Prefab es obligatorio cuando dangerCount > 0.");
            return false;
        }
        // --------------------------------
        return true;
    }
}
