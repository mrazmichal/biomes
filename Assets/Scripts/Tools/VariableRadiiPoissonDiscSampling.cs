using MathNet.Numerics.Random;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Implementation of Variable Radii Poisson Disc Sampling in 2D.
/// Based on implementation of Poisson Disc Sampling by Sebastian Lague.
/// Original code is included in a zip file.
/// Lague's video: https://www.youtube.com/watch?v=7WcmyxyFO7o
/// Lague's code on GitHub: https://github.com/SebLague/Poisson-Disc-Sampling/blob/master/Poisson%20Disc%20Sampling%20E01/PoissonDiscSampling.cs
/// </summary>
/// <author>Sebastian Lague, Michal Mráz</author>
public static class VariableRadiiPoissonDiscSampling
{
    public static RandomSource randomSource = new UnityRandomSource(); // used for sampling from normal distribution
    
    public struct VirtualPoint{
        public Vector2 position;
        public float radius;
    }
    
    /// <summary>
    /// Normal distribution sample truncated at 3 stdDev (99.7 % of the distribution should be within this range)
    /// </summary>
    /// <param name="mean"></param>
    /// <param name="stdDev"></param>
    /// <returns></returns>
    static float getTruncatedNormalDistributionSample(float mean, float stdDev)
    {
        float sample = getNormalDistributionSample(mean, stdDev);
        while (sample < mean - 3*stdDev || sample > mean + 3*stdDev)
        {
            sample = getNormalDistributionSample(mean, stdDev);
        }
        return sample;
    }
    
    /// <summary>
    /// Normal distribution sample
    /// </summary>
    /// <param name="mean"></param>
    /// <param name="stdDev">standard deviation</param>
    /// <returns></returns>
    static float getNormalDistributionSample(float mean, float stdDev)
    {
        return (float)MathNet.Numerics.Distributions.Normal.Sample(randomSource, mean, stdDev);
    }

    static int[,] fillIntArrayWithValue(int[,] array, int value)
    {
        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                array[i, j] = value;
            }
        }
        return array;
    }

    /// <summary>
    /// Generate VirtualPoints using Variable Radii Poisson Disc Sampling
    /// </summary>
    /// <param name="sampleRegionSize">size of the region in which to sample points</param> 
    /// <param name="minR">minimum point radius</param> 
    /// <param name="maxR">maximum point radius</param> 
    /// <param name="meanR">mean point radius</param> 
    /// <param name="numSamplesBeforeRejection">number of random samples attempted from each accepted point before rejection</param>
    /// <returns></returns>
    public static List<VirtualPoint> generatePoints(Vector2 sampleRegionSize, float minR, float maxR, float meanR, int numSamplesBeforeRejection)
    {
        // We will use normal distribution for the radii of the points and truncate it at 3 stdDev (99.7 % of the distribution should be within this range)
        float stdDevR = (meanR - minR) / 3;
        // float minR = mean - 3*stdDev;
        // float maxR = mean + 3*stdDev;
        
        float cellSize = minR / Mathf.Sqrt(2); // the diagonal of the cell is minR, so that in a cell there can be at most one point
        int gridWidth = Mathf.CeilToInt((sampleRegionSize.x) / cellSize);
        int gridHeight = Mathf.CeilToInt((sampleRegionSize.y) / cellSize);
        int[,] grid = new int[gridWidth, gridHeight]; // contains index of the point in the points list, -1 if there is no point
        fillIntArrayWithValue(grid, -1);
        List<VirtualPoint> spawnPoints = new List<VirtualPoint>(); // points from which to spawn new points
        List<VirtualPoint> points = new List<VirtualPoint>();      // confirmed points

        spawnPoints.Add(new VirtualPoint{position = sampleRegionSize / 2, radius = getTruncatedNormalDistributionSample(meanR, stdDevR)});
        
        while(spawnPoints.Count > 0)
        {
            int spawnIndex = Random.Range(0, spawnPoints.Count);
            VirtualPoint spawnCentre = spawnPoints[spawnIndex];
            for (int i = 0; i < numSamplesBeforeRejection; i++)
            {
                float angle = Random.value * Mathf.PI * 2;
                Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                float candidateRadius = getTruncatedNormalDistributionSample(meanR, stdDevR);
                float candidateMinDistance = spawnCentre.radius + candidateRadius;
                float candidateMaxDistance = 2 * candidateMinDistance;
                float candidateDistance = Random.Range(candidateMinDistance, candidateMaxDistance);
                Vector2 candidatePosition = spawnCentre.position + dir * candidateDistance;
                VirtualPoint candidate = new VirtualPoint{position = candidatePosition, radius = candidateRadius};
                
                if (isValid(candidate, sampleRegionSize, minR, maxR, points, grid, cellSize))
                {
                    points.Add(candidate);
                    spawnPoints.Add(candidate);
                    grid[(int)(candidate.position.x / cellSize), (int)(candidate.position.y / cellSize)] = points.Count -1;
                }
            }
            spawnPoints.RemoveAt(spawnIndex);
        }
        
        return points;
    }
    
    /// <summary>
    /// Check if the candidate point is valid
    /// </summary>
    /// <param name="candidate"></param> Candidate point to check if it doesn't collide with existing points
    /// <param name="sampleRegionSize"></param> Size of the region in which to sample points
    /// <param name="minR"></param> Minimum point radius
    /// <param name="maxR"></param> Maximum point radius
    /// <param name="points"></param> List of existing points
    /// <param name="grid"></param> Grid of points - acceleration structure for checking of collisions between points
    /// <param name="cellSize"></param> Size of the cell in the grid
    /// <returns></returns>
    static bool isValid(VirtualPoint candidate, Vector2 sampleRegionSize, float minR, float maxR, List<VirtualPoint> points, int[,] grid, float cellSize)
    {
        
        if (!(candidate.position.x >= 0 && candidate.position.x <= sampleRegionSize.x && candidate.position.y >= 0 && candidate.position.y <= sampleRegionSize.y))
        {
            return false;
        }
        
        float distanceOfInterest = candidate.radius + maxR;
        int numberOfCellsOfInterestInDirection = Mathf.CeilToInt(distanceOfInterest / cellSize);
        
        int cellX = (int)(candidate.position.x / cellSize);
        int cellY = (int)(candidate.position.y / cellSize);
        
        int searchStartX = Mathf.Max(0, cellX - numberOfCellsOfInterestInDirection);
        int searchEndX = Mathf.Min(cellX + numberOfCellsOfInterestInDirection, grid.GetLength(0) - 1);
        
        int searchStartY = Mathf.Max(0, cellY - numberOfCellsOfInterestInDirection);
        int searchEndY = Mathf.Min(cellY + numberOfCellsOfInterestInDirection, grid.GetLength(1) - 1);
        
        for (int x = searchStartX; x <= searchEndX; x++)
        {
            for (int y = searchStartY; y <= searchEndY; y++)
            {
                int pointIndex = grid[x, y];

                if (pointIndex == -1)
                {
                    continue;
                }
                
                VirtualPoint point = points[pointIndex];
                
                float distance = Vector2.Distance(point.position, candidate.position);
                if (distance < point.radius + candidate.radius)
                {
                    return false;
                }
                
            }
        }

        return true;
    }

}
