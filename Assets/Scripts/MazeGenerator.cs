using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    [Header("Wymiary Labiryntu (Musz¹ byæ NIEPARZYSTE)")]
    public int width = 11;
    public int height = 11;
    public Material brick;
    public Material floorMaterial;

    [Header("ML-Agents Obiekty")]
    public Transform agent;
    public Transform target;

    private int[,] Maze;
    private List<Vector3> pathMazes = new List<Vector3>();
    private Stack<Vector2> _tiletoTry = new Stack<Vector2>();
    private List<Vector2> offsets = new List<Vector2> { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };
    private System.Random rnd = new System.Random(Guid.NewGuid().GetHashCode());

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

    public void GenerateMaze()
    {
        // 1. Czyszczenie poprzedniego labiryntu
        if (wallsContainer != null) Destroy(wallsContainer);
        wallsContainer = new GameObject("WallsContainer");
        wallsContainer.transform.parent = this.transform;
        wallsContainer.transform.localPosition = Vector3.zero;

        pathMazes.Clear();
        _tiletoTry.Clear();

        // 2. Pod³oga
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = wallsContainer.transform;

        floor.transform.localPosition = new Vector3((width - 1) / 2f, -0.5f, (height - 1) / 2f);
        floor.transform.localScale = new Vector3(width, 1, height);
        
        if (floorMaterial != null)
        {
            floor.GetComponent<Renderer>().material = floorMaterial;
        }

        // 3. Generowanie uk³adu
        Maze = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Maze[x, y] = 1;
            }
        }
        CurrentTile = Vector2.one;
        _tiletoTry.Push(CurrentTile);
        Maze = CreateMaze();

        // 4. Stawianie bloków (Oœ Z zamiast Y)
        for (int i = 0; i <= Maze.GetUpperBound(0); i++)
        {
            for (int j = 0; j <= Maze.GetUpperBound(1); j++)
            {
                if (Maze[i, j] == 1)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.parent = wallsContainer.transform;
                    wall.transform.localPosition = new Vector3(i, 0.5f, j);
                    wall.tag = "Wall"; // Konieczne dla wzroku Agenta

                    if (brick != null)
                    {
                        wall.GetComponent<Renderer>().material = brick;
                    }
                }
                else if (Maze[i, j] == 0)
                {
                    pathMazes.Add(new Vector3(i, 0.5f, j));
                }
            }
        }

        // 5. Respawn Agenta i Celu
        if (agent != null && target != null && pathMazes.Count > 0)
        {
            agent.localPosition = new Vector3(1, 0.5f, 1);
            int randomTargetIndex = rnd.Next(pathMazes.Count / 2, pathMazes.Count);
            target.localPosition = pathMazes[randomTargetIndex];
        }
    }

    public int[,] CreateMaze()
    {
        List<Vector2> neighbors;
        while (_tiletoTry.Count > 0)
        {
            Maze[(int)CurrentTile.x, (int)CurrentTile.y] = 0;
            neighbors = GetValidNeighbors(CurrentTile);

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
        return Maze;
    }

    private List<Vector2> GetValidNeighbors(Vector2 centerTile)
    {
        List<Vector2> validNeighbors = new List<Vector2>();
        foreach (var offset in offsets)
        {
            // Przywrócono oryginaln¹ matematykê z Twojego skryptu (bez moich modyfikacji)
            Vector2 toCheck = new Vector2(centerTile.x + offset.x, centerTile.y + offset.y);

            if (toCheck.x % 2 == 1 || toCheck.y % 2 == 1)
            {
                if (IsInside(toCheck) && Maze[(int)toCheck.x, (int)toCheck.y] == 1 && HasThreeWallsIntact(toCheck))
                {
                    validNeighbors.Add(toCheck);
                }
            }
        }
        return validNeighbors;
    }

    private bool HasThreeWallsIntact(Vector2 Vector2ToCheck)
    {
        int intactWallCounter = 0;
        foreach (var offset in offsets)
        {
            Vector2 neighborToCheck = new Vector2(Vector2ToCheck.x + offset.x, Vector2ToCheck.y + offset.y);
            if (IsInside(neighborToCheck) && Maze[(int)neighborToCheck.x, (int)neighborToCheck.y] == 1)
            {
                intactWallCounter++;
            }
        }
        return intactWallCounter == 3;
    }

    private bool IsInside(Vector2 p)
    {
        // Przywrócono oryginalne granice z Twojego skryptu
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }
}