using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    [Header("Wymiary Labiryntu (Muszą być NIEPARZYSTE)")]
    public Material brick;
    public Material floorMaterial;

    public int width = 11;
    public int height = 11;

    [Header("ML-Agents Obiekty")]
    public Transform agent;
    public Transform target;

    [Header("Typ Środowiska")]
    public bool usePillarGrid = false;

    [Tooltip("TRUE = Success Rate / Długość (Losowe mapy). FALSE = Heatmapa (Stała mapa).")]
    public bool regenerateEachEpisode = true;

    [Header("Ewaluacja / Seed")]
    [Tooltip("Zaznacz, aby sekwencja losowań (mapy i pozycje) była identyczna w każdym teście PPO i SAC")]
    public bool useFixedSeedSequence = true;
    public int baseSeed = 42;
    private int currentEpisodeIndex = 0; // Licznik używany do generowania seeda

    [Header("Statystyki (Curriculum Learning)")]
    public int targetsCollected = 0;

    [Header("Pillar Grid Settings")]
    [Range(0f, 0.8f)]
    public float extraWallChance = 0.3f;

    private int[,] Grid;
    private List<Vector3> pathMazes = new List<Vector3>();
    private Stack<Vector2> _tiletoTry = new Stack<Vector2>();
    private List<Vector2> offsets = new List<Vector2> { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };

    private System.Random rnd;
    private GameObject wallsContainer;

    private Vector2 _currentTile;
    public Vector2 CurrentTile
    {
        get { return _currentTile; }
        private set
        {
            if (value.x < 1 || value.x >= this.width - 1 || value.y < 1 || value.y >= this.height - 1)
            {
                throw new ArgumentException("CurrentTile must be within the one tile border all around the maze");
            }
            if (value.x % 2 == 1 || value.y % 2 == 1)
            { _currentTile = value; }
            else
            {
                throw new ArgumentException("The current square must not be both on an even X-axis and an even Y-axis");
            }
        }
    }

    void Start()
    {
        // Pierwsze wygenerowanie środowiska
        UpdateRandomSeed();
        GenerateEnvironment();
    }

    public void RespawnAgentAndTarget()
    {
        currentEpisodeIndex++;
        UpdateRandomSeed(); // Odświeżenie seeda dla nowego epizodu

        if (regenerateEachEpisode)
            GenerateEnvironment(); // Przebudowuje ściany i losuje pozycje agenta/celu
        else
            PlaceAgentAndTarget(); // Ściany zostają, losuje tylko pozycje agenta/celu
    }

    private void UpdateRandomSeed()
    {
        if (useFixedSeedSequence)
        {
            // Gwarantuje tę samą serię losowań dla każdego uruchomienia (PPO/SAC)
            rnd = new System.Random(baseSeed + currentEpisodeIndex);
        }
        else
        {
            // Całkowita, niepowtarzalna losowość
            rnd = new System.Random(Guid.NewGuid().GetHashCode());
        }
    }

    public void GenerateEnvironment()
    {
        if (usePillarGrid)
            GeneratePillarGrid();
        else
            GenerateMaze();
    }

    public void GenerateMaze()
    {
        SetupContainer();

        Grid = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Grid[x, y] = 1;

        CurrentTile = Vector2.one;
        _tiletoTry.Push(CurrentTile);
        CreateMaze();
        BuildWalls();
        PlaceAgentAndTarget();
    }

    private void CreateMaze()
    {
        while (_tiletoTry.Count > 0)
        {
            Grid[(int)CurrentTile.x, (int)CurrentTile.y] = 0;
            var neighbors = GetValidMazeNeighbors(CurrentTile);
            if (neighbors.Count > 0)
            {
                _tiletoTry.Push(CurrentTile);
                CurrentTile = neighbors[rnd.Next(neighbors.Count)];
            }
            else
            {
                CurrentTile = _tiletoTry.Pop();
            }
        }
    }

    private List<Vector2> GetValidMazeNeighbors(Vector2 centerTile)
    {
        var valid = new List<Vector2>();
        foreach (var offset in offsets)
        {
            Vector2 toCheck = centerTile + offset;
            if (toCheck.x % 2 == 1 || toCheck.y % 2 == 1)
                if (IsInside(toCheck) && Grid[(int)toCheck.x, (int)toCheck.y] == 1
                    && HasThreeWallsIntact(toCheck))
                    valid.Add(toCheck);
        }
        return valid;
    }

    private bool HasThreeWallsIntact(Vector2 tile)
    {
        int count = 0;
        foreach (var offset in offsets)
        {
            Vector2 n = tile + offset;
            if (IsInside(n) && Grid[(int)n.x, (int)n.y] == 1)
                count++;
        }
        return count == 3;
    }

    public void GeneratePillarGrid()
    {
        SetupContainer();
        Grid = new int[width, height];

        // outer walls only
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                if (x == 0 || x == width - 1 || z == 0 || z == height - 1)
                    Grid[x, z] = 1;

        // scatter completely random single pillars
        int numPillars = (width * height) / 10; // ~10% coverage

        for (int i = 0; i < numPillars; i++)
        {
            int px = rnd.Next(2, width - 2);
            int pz = rnd.Next(2, height - 2);
            Grid[px, pz] = 1;
        }

        EnsureConnectivity();
        BuildWalls();
        PlaceAgentAndTarget();
    }

    private void EnsureConnectivity()
    {
        List<Vector2> openCells = new List<Vector2>();
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                if (Grid[x, z] == 0)
                    openCells.Add(new Vector2(x, z));

        if (openCells.Count == 0) return;

        Vector2 start = openCells[0];
        HashSet<Vector2> visited = FloodFill(start);

        List<Vector2> unreachable = new List<Vector2>();
        foreach (var cell in openCells)
            if (!visited.Contains(cell))
                unreachable.Add(cell);

        int maxAttempts = 1000;
        int attempts = 0;
        while (unreachable.Count > 0 && attempts < maxAttempts)
        {
            attempts++;
            Vector2 cell = unreachable[rnd.Next(unreachable.Count)];

            int[] dx = { 0, 0, 1, -1 };
            int[] dz = { 1, -1, 0, 0 };

            for (int d = 0; d < 4; d++)
            {
                int nx = (int)cell.x + dx[d];
                int nz = (int)cell.y + dz[d];
                int nx2 = (int)cell.x + dx[d] * 2;
                int nz2 = (int)cell.y + dz[d] * 2;

                if (IsInsideInt(nx2, nz2) &&
                    Grid[nx, nz] == 1 &&
                    Grid[nx2, nz2] == 0 &&
                    visited.Contains(new Vector2(nx2, nz2)))
                {
                    Grid[nx, nz] = 0;
                    visited = FloodFill(start);
                    unreachable.Clear();
                    foreach (var c in openCells)
                        if (!visited.Contains(c))
                            unreachable.Add(c);
                    break;
                }
            }
        }
    }

    private HashSet<Vector2> FloodFill(Vector2 start)
    {
        var visited = new HashSet<Vector2>();
        var queue = new Queue<Vector2>();
        queue.Enqueue(start);
        visited.Add(start);

        int[] dx = { 0, 0, 1, -1 };
        int[] dz = { 1, -1, 0, 0 };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                var next = new Vector2(current.x + dx[d], current.y + dz[d]);
                if (IsInsideInt((int)next.x, (int)next.y) &&
                    !visited.Contains(next) &&
                    Grid[(int)next.x, (int)next.y] == 0)
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return visited;
    }

    private bool IsInside(Vector2 p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }

    private bool IsInsideInt(int x, int z) =>
        x >= 0 && z >= 0 && x < width && z < height;

    public void TargetReached()
    {
        targetsCollected++;
    }

    private void SetupContainer()
    {
        if (wallsContainer != null) Destroy(wallsContainer);
        wallsContainer = new GameObject("WallsContainer");
        wallsContainer.transform.parent = transform;
        wallsContainer.transform.localPosition = Vector3.zero;
        pathMazes.Clear();
        _tiletoTry.Clear();
    }

    private void BuildWalls()
    {
        // floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = wallsContainer.transform;
        floor.transform.localPosition = new Vector3((width - 1) / 2f, -0.5f, (height - 1) / 2f);
        floor.transform.localScale = new Vector3(width, 1, height);
        if (floorMaterial != null)
            floor.GetComponent<Renderer>().material = floorMaterial;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (Grid[x, z] == 1)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.parent = wallsContainer.transform;
                    wall.transform.localPosition = new Vector3(x, 0.5f, z);
                    wall.tag = "Wall";
                    if (brick != null)
                        wall.GetComponent<Renderer>().material = brick;
                }
                else
                {
                    pathMazes.Add(new Vector3(x, 0.5f, z));
                }
            }
        }
    }

    private void PlaceAgentAndTarget()
    {
        if (agent != null && target != null && pathMazes.Count > 0)
        {
            int randomAgentIndex = rnd.Next(0, pathMazes.Count);
            agent.localPosition = pathMazes[randomAgentIndex];

            int randomTargetIndex;
            do
            {
                randomTargetIndex = rnd.Next(0, pathMazes.Count);
            } while (randomTargetIndex == randomAgentIndex);

            target.localPosition = pathMazes[randomTargetIndex];
        }
    }
}