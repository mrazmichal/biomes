using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extrudes the mesh in the given direction.
/// </summary>
/// <author>Michal Mráz</author>
public static class Extrusion
{
    public static void ExtrudeMeshWallsOnly(Vector3[] vertices, int[] triangles, ref Vector3[] verticesExtr, ref int[] trianglesExtr, float extrusionAmount, Vector3 extrusionDirection)
    {
        verticesExtr = new Vector3[vertices.Length * 4];
        
        Vector3 shift = extrusionDirection * extrusionAmount;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            verticesExtr[2*i] = vertices[i]; // bottom vertices
            verticesExtr[2*i + 1] = vertices[i]; // bottom vertices second time (duplicated to ensure hard edges)
            verticesExtr[2*i + 2*vertices.Length] = vertices[i] + shift; // top vertices
            verticesExtr[2*i + 1 + 2*vertices.Length] = vertices[i] + shift; // top vertices second time
        }
        
        List<int> trianglesList = new List<int>();

        for (int i = 1; i < vertices.Length *2; i+=2)
        {
            trianglesList.Add(i);
            trianglesList.Add((i+1) % (2*vertices.Length));
            trianglesList.Add((i+1) % (2*vertices.Length) + 2*vertices.Length);

            trianglesList.Add(i);
            trianglesList.Add(2*vertices.Length + (i + 1) % (2*vertices.Length));
            trianglesList.Add(2*vertices.Length + i);
        }

        trianglesExtr = trianglesList.ToArray();
    }
    
    // not used
    public static void ExtrudeMeshCeilingOnly(Vector3[] vertices, int[] triangles, ref Vector3[] verticesExtr, ref int[] trianglesExtr, float extrusionAmount, Vector3 extrusionDirection)
    {
        verticesExtr = new Vector3[vertices.Length];
        vertices.CopyTo(verticesExtr, 0);
        for (int i = 0; i < verticesExtr.Length; i++)
        {
            verticesExtr[i] += extrusionDirection * extrusionAmount;
        }
        trianglesExtr = new int[triangles.Length];
        triangles.CopyTo(trianglesExtr, 0);
    }

}
