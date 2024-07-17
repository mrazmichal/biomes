using System;
using System.Collections.Generic;
using System.Linq;

// Some tips to make it faster: 
// - create lists of convex and reflex vertices and of ears
// - modify the vertices only next to the just removed ear
// Can be also extended to be able to create buildings with holes (those are also sometimes present in OSM) - All of these features are described in the following pdf
// https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf

/// <summary>
/// Triangulates polygon from list of vertices.
/// Translated to C# and modified by Michal Mráz from original source by mrbaozi, which is included in a zip file and also can be found here:
/// https://github.com/mrbaozi/triangulation/blob/master/sources/triangulate.py
/// </summary>
/// <author>mrbaozi, Michal Mráz</author>
public static class Triangulation
{


    /// <summary>
    /// Determine whether angle at point b of triangle (a, b, c) is convex
    /// We suppose that the points of the triangle are given in anti-clockwise order
    /// </summary>
    static bool IsConvex(Double2 a, Double2 b, Double2 c)
    {
        Double2 firstVector = new Double2(b.X - a.X, b.Y - a.Y);
        Double2 secondVector = new Double2(c.X - a.X, c.Y - a.Y);
        
        // if cosine of angle is bigger than 0, then the angle is convex
        if (crossProduct(firstVector, secondVector) >= 0)
        {
            return true;
        }
        return false;
    }
    
    static double crossProduct(Double2 a, Double2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    /// <summary>
    /// Check whether a given point p lies within the triangle formed by points a, b, and c
    /// </summary>
    static bool InTriangle(Double2 a, Double2 b, Double2 c, Double2 p)
    {
        double[] L = { 0, 0, 0 };
        double eps = 0.0000001;
        // calculate barycentric coefficients for point p
        // eps is needed as error correction since for very small distances denom->0
        L[0] = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y))
              / (((b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y)) + eps);
        L[1] = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y))
              / (((b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y)) + eps);
        L[2] = 1 - L[0] - L[1];
        // check if p lies in triangle (a, b, c)
        foreach (double x in L)
        {
            if (x > 1 || x < 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Find if polygon is clockwise using a sum
    /// </summary>
    public static bool IsPolygonClockwise(List<Double2> poly)
    {
        // initialize sum with last element
        double sum = (poly[0].X - poly[poly.Count - 1].X) * (poly[0].Y + poly[poly.Count - 1].Y);
        // iterate over all other elements (0 to n-1)
        for (int i = 0; i < poly.Count - 1; i++)
        {
            sum += (poly[i + 1].X - poly[i].X) * (poly[i + 1].Y + poly[i].Y);
        }
        return (sum > 0);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="indices"></param>
    /// <param name="vertices">all of the polygon vertices</param>
    /// <returns></returns>
    static Tuple<int, int, int> GetEar(DoublyLinkedCircularLinkedList<int> indices, Double2[] vertices)
    {
        // how many vertices are left
        int size = indices.Count;

        // Cannot find an ear if the polygon has less than 3 vertices
        if (size < 3)
        {
            return null;
        }
        // If the polygon has exactly 3 vertices, return them as a triangle
        if (size == 3)
        {
            Tuple<int, int, int> tri = new Tuple<int, int, int>(indices.Head.data, indices.Head.next.data, indices.Head.next.next.data);
            indices.Clear();
            return tri;
        }

        
        var current = indices.Head;
        do {
            // take 3 points around current vertex
            int p1 = current.prev.data;
            int p2 = current.data;
            int p3 = current.next.data;
            Double2 p1Point = vertices[p1];
            Double2 p2Point = vertices[p2];
            Double2 p3Point = vertices[p3];

            if (IsConvex(p1Point, p2Point, p3Point))
            {
                // for each vertex remaining in the polygon perform the triangle test
                bool tritest = false;
                var x = indices.Head;
                for (int j = 0; j < size; j++)
                {
                    Double2 xPoint = vertices[x.data];

                    List<Double2> points = new List<Double2> { p1Point, p2Point, p3Point };
                    // if x is not one of these 3 points and if x is inside the triangle, then the triangle test is true
                    if (!(points.Contains(xPoint)) && InTriangle(p1Point, p2Point, p3Point, xPoint)) 
                    {
                        tritest = true;
                        break;
                    }

                    x = x.next;
                }
            
                // if the triangle test is false, then the triangle is an ear
                if (tritest == false)
                {
                    indices.Delete(current);
                    return new Tuple<int, int, int>(p1, p2, p3);
                }
            }

            current = current.next;
            
        } while (current != indices.Head);
        
            
        Console.WriteLine("GetEar(): no ear found");
        return null;
        
    }
    
    /// <summary>
    /// Triangulate a polygon given by its vertices
    /// </summary>
    /// <param name="vertices"></param> the vertices of the polygon
    /// <param name="triangles"></param> the list of indices of vertices that form the resultant triangles
    public static void TriangulatePolygon(Double2[] vertices, List<int> triangles)
    {
        DoublyLinkedCircularLinkedList<int> indices = new DoublyLinkedCircularLinkedList<int>();
        
        for (int i = 0; i < vertices.Length; i++)
        {
            indices.Add(i);
        }

        // fix, because the algorithm assumes anticlockwise vertices here
        if (IsPolygonClockwise(vertices.ToList())) // clockwise is more common in OSM
        {
            indices.Reverse();
        }
        
        while (indices.Count >= 3)
        {
            Tuple<int, int, int> a = GetEar(indices, vertices);
            if (a == null)
            {
                break;
            }
            triangles.Add(a.Item1);
            triangles.Add(a.Item3);
            triangles.Add(a.Item2);
        }

    }
    
}
