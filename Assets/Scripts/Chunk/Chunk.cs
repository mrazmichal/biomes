using NGeoHash;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using static Helpers;

/// <summary>
/// Basic environment unit, holds terrain, buildings, streets, vegetation
/// </summary>
/// <author>Michal Mráz</author>
public class Chunk : MonoBehaviour
{
    public long geohash;
    public BoundingBox boundingBox; // is in gps coordinates

    bool mapGenerationDone = false;
    bool terrainGenerationDone = false;
	[FormerlySerializedAs("didntStartLoadingYet")]
    public bool startedLoading = false;
    public bool finishedLoading = false;

    public GameObject terrain;
    public GameObject buildings;
    public GameObject streets;
    public GameObject streetColliders;
    public GameObject vegetation;

    internal int latitudeCellsCount;
    internal int longitudeCellsCount;
    
    BuildingsAndStreetsGenerator buildingsAndStreetsGenerator;
    TerrainGenerator terrainGenerator;
    BiomeGenerator biomeGenerator;

    float streetHeightOffset = 0.7f;
    float terrainSmoothingRange = 18;

    const int STREETS_AND_BUILDINGS_LAYER = 6;

    // We call this immediately after object creation, because OnAwake is called too late
    public void initializeChunk()
    {
        // Create gameObjects - wrappers for the things we are going to create in this chunk
        buildings = new GameObject();
        buildings.name = "Buildings";
        buildings.transform.parent = this.gameObject.transform;
        streets = new GameObject();
        streets.name = "Streets";
        streets.transform.parent = this.gameObject.transform;
        streetColliders = new GameObject();
        streetColliders.name = "streetColliders";
        streetColliders.transform.parent = this.gameObject.transform;
        terrain = new GameObject();
        terrain.name = "Terrain";
        terrain.transform.parent = this.gameObject.transform;
        vegetation = new GameObject();
        vegetation.name = "Vegetation";
        vegetation.transform.parent = this.gameObject.transform;

        boundingBox = GeohashCalculations.getGeohashBounds(geohash);

        printBoundingBoxDimensionsInMeters();

        buildingsAndStreetsGenerator = this.gameObject.AddComponent<BuildingsAndStreetsGenerator>(); // it's added as component so that we are able to use coroutines
        buildingsAndStreetsGenerator.Init(this);
        terrainGenerator = this.gameObject.AddComponent<TerrainGenerator>(); // it's added as component so that we are able to use coroutines
        terrainGenerator.Init(this);
        biomeGenerator = new BiomeGenerator(this);
        
    }
    
    /// <summary>
    /// Generates terrain, buildings, streets, biomes, vegetation
    /// </summary>
    internal void generateContent()
    {
        startedLoading = true;
        
        // get this information for logical matrix creation
        latitudeCellsCount = terrainGenerator.subdivided_latPointsCount - 1;
        longitudeCellsCount = terrainGenerator.subdivided_lonPointsCount - 1;
       
        // These methods send http request, then continue after receiving data.
        // They work in parallel, we then continue after both finish. 
        StartCoroutine(buildingsAndStreetsGenerator.generateBuildingsAndStreets());
        StartCoroutine(terrainGenerator.generateTerrain());
        
        // code continues in informThatMapAndTerrainGenerationBOTHDone()

    }
    
    /// <summary>
    /// The chunk represents some real world area, but the real and Unity sizes differ. Print both sizes to compare.
    /// </summary>
    public void printBoundingBoxDimensionsInMeters()
    {
        GpsVector min = new GpsVector(boundingBox.Minimum.Lat, boundingBox.Minimum.Lon);
        GpsVector max = new GpsVector(boundingBox.Maximum.Lat, boundingBox.Maximum.Lon);

        Vector3 minMeters = Gps.instance.ConvertGpsToUnityCoords(min);
        Vector3 maxMeters = Gps.instance.ConvertGpsToUnityCoords(max);

        double height = maxMeters.z - minMeters.z;
        double width = maxMeters.x - minMeters.x;
        
        Debug.Log("Chunk sizes in Unity: height " + height + " meters, width " + width + " meters");

        // Some information to asses the distortion caused by using one referenceOrigin for whole world
        double latDiff = max.latitude - min.latitude;
        double lonDiff = max.longitude - min.longitude;
        // compute real world size in meters for latitude at min.latitude
        double latMeters = latDiff / 360f * Gps.EARTH_CIRCUMFERENCE;
        double smallerCircumferenceAtLat = 2 * Math.PI * Gps.EARTH_RADIUS * Math.Cos(deg2rad(min.latitude));
        double lonMeters = lonDiff / 360f * smallerCircumferenceAtLat;
        Debug.Log("Chunk sizes in real world: height " + latMeters + " meters, width " + lonMeters + " meters");
        
    }
    
    /// <summary>
    /// buildings and streets generation has finished
    /// </summary>
    internal void informThatMapGenerationDone()
    {
        mapGenerationDone = true;

        // both functions will run in the same thread, so when the first function gets executed, the condition below won't yet be true.
        if (terrainGenerationDone && mapGenerationDone)
        {
            informThatMapAndTerrainGenerationBOTHDone();
        }
    }

    /// <summary>
    /// Terrain generation has finished
    /// </summary>
    internal void informThatTerrainGenerationDone()
    {
        terrainGenerationDone = true;

        // both functions will run in the same thread, so when the first function gets executed, the condition below won't yet be true.
        if (terrainGenerationDone && mapGenerationDone)
        {
            informThatMapAndTerrainGenerationBOTHDone();
        }

    }

    /// <summary>
    /// Terrain, buildings and streets generation has finished.
    /// Smooth terrain under streets, set colliders, snap buildings to terrain, generate biomes, generate vegetation
    /// </summary>
    private void informThatMapAndTerrainGenerationBOTHDone()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Restart();
        
        smoothTerrainUnderStreets();
        
        setTerrainColliderMesh();
        
        stitchNormalsOfNeighboringChunksTogether();
        
        tryToSnapBuildingsAndStreetsToTerrain();
        
        createStreetColliders();
        
        createBuildingColliders();
        
        stopwatch.Stop();
        Debug.Log("Terrain smoothing, setting colliders and snapping buildings to terrain took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + geohash);
        stopwatch.Restart();

        biomeGenerator.generateBiomes();
        
        stopwatch.Stop();
        Debug.Log("Generating biomes took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + geohash);
        
        finishedLoading = true;
    }
    
    /// <summary>
    /// Create building collider from the ceiling of the building. Used to detect collision of building with vegetation
    /// </summary>
    void createBuildingColliders()
    {
        for (int i = 0; i < buildings.transform.childCount; i++)
        {
            GameObject building = buildings.transform.GetChild(i).gameObject;
            GameObject ceiling = building.transform.Find("Ceiling").gameObject;
            MeshCollider meshCollider = building.AddComponent<MeshCollider>();
            MeshFilter meshFilter = ceiling.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.mesh;
            meshCollider.sharedMesh = mesh;
        }
    }

    /// <summary>
    /// Using points of streets and using the width of the streets create mesh that represents the street. Use this mesh to create collider for the street.
    /// </summary>
    void createStreetColliders()
    {
        for (int k = 0; k < streets.transform.childCount; k++)
        {
            GameObject street = streets.transform.GetChild(k).gameObject;
            LineRenderer lineRenderer = street.GetComponent<LineRenderer>();

            // Create collider gameObject
            GameObject colliderObject = new GameObject();
            colliderObject.transform.parent = streetColliders.transform;
            colliderObject.name = "StreetCollider";
            
            colliderObject.layer = STREETS_AND_BUILDINGS_LAYER;
            
            // Create street mesh collider
            MeshCollider meshColl = colliderObject.AddComponent<MeshCollider>();
            Mesh mesh = new Mesh();

            if (lineRenderer.positionCount < 2)
            {
                Debug.LogError("What is this line renderer? It has less than 2 points!");
            }

            Vector3[] points = new Vector3[lineRenderer.positionCount];
            lineRenderer.GetPositions(points);
            Vector3[] vertices = new Vector3[points.Length * 2];
            int[] triangles = new int[(points.Length - 1) * 6];

            float streetWidth = lineRenderer.startWidth;
            float halfWidth = streetWidth / 2;
            Vector3 toSky = new Vector3(0, 1, 0);
            Vector3 oldDirection = new Vector3();
            // For every street point create two vertices 
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 direction = new Vector3();
                if (i == 0)
                {
                    // Get direction from point i to i+1
                    direction = Vector3.Normalize(points[i + 1] - points[i]);
                }
                else if (i > 0 && i < points.Length - 1)
                {
                    // Average directions from i-1 to i and from i to i+1
                    Vector3 newDirection = Vector3.Normalize(points[i + 1] - points[i]);
                    direction = Vector3.Normalize(newDirection + oldDirection);
                }
                else if (i == points.Length - 1)
                {
                    // Get direction from point i-1 to i
                    direction = oldDirection;
                }
                oldDirection = direction;

                Vector3 perpendicular = Vector3.Cross(toSky, direction); // left hand rule - the result points to the right when we are facing in the direction of the direction vector
                vertices[i * 2] = points[i] + perpendicular * halfWidth;
                vertices[i * 2 + 1] = points[i] - perpendicular * halfWidth;
            }

            // Create triangles
            for (int i = 0; i < points.Length - 1; i++)
            {
                triangles[i * 6] = i * 2;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = i * 2 + 2;
                triangles[i * 6 + 3] = i * 2 + 1;
                triangles[i * 6 + 4] = i * 2 + 3;
                triangles[i * 6 + 5] = i * 2 + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;

            meshColl.sharedMesh = mesh;
            
        }
    }

    /// <summary>
    /// Set the terrain mesh as the terrain collider
    /// </summary>
    void setTerrainColliderMesh()
    {
        MeshCollider collider = terrain.GetComponent<MeshCollider>();
        MeshFilter meshFilter = terrain.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;
        collider.sharedMesh = mesh;
    }

    /// <summary>
    /// For every point of the street level the terrain around the point. This is done to make the terrain under the streets smoother, to avoid street glitching into the terrain. 
    /// </summary>
    void smoothTerrainUnderStreets()
    {
        MeshFilter meshFilter = this.terrain.GetComponent<MeshFilter>();
        // copy of mesh
        Mesh mesh = meshFilter.mesh;
        
        for (int i = 0; i < streets.transform.childCount; i++)
        {
            GameObject street = streets.transform.GetChild(i).gameObject;
            Vector3[] positions = street.GetComponent<StreetData>().vertices;

            foreach (Vector3 position in positions)
            {
                smoothTerrainAtPosition(position, mesh);
            }
            
        }

        meshFilter.mesh = mesh;
        
    }
    
    /// <summary>
    ///  Level the terrain around one point.
    /// </summary>
    void smoothTerrainAtPosition(Vector3 smoothingCenter, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        List<Vector2Int> pointsWithinRange = new List<Vector2Int>();
        getVerticesWithinRangeFromPoint(smoothingCenter, vertices, terrainSmoothingRange, pointsWithinRange);
        
        float averageHeightOfPoints = getAverageHeightOfPoints(pointsWithinRange, vertices);
        
        smoothVertices(smoothingCenter, vertices, pointsWithinRange, averageHeightOfPoints);
        
        mesh.vertices = vertices;
    }
    
    /// <summary>
    /// Get vertices that are within range
    /// </summary>
    /// <param name="point"></param>
    /// <param name="vertices"></param>
    /// <param name="range"></param>
    /// <param name="pointsWithinRange"></param>
    void getVerticesWithinRangeFromPoint(Vector3 point, Vector3[] vertices, float range, List<Vector2Int> pointsWithinRange)
    {
        int iMinIndex = 0, iMaxIndex = 0, jMinIndex = 0, jMaxIndex = 0;
        getMinAndMaxIndicesOfVerticesWithinRangeFromPoint(point, vertices, range, ref iMinIndex, ref iMaxIndex, ref jMinIndex, ref jMaxIndex);

        for (int i = iMinIndex; i <= iMaxIndex; i++)
        {
            for (int j = jMinIndex; j <= jMaxIndex; j++)
            {
                Vector3 vertex = vertices[i * (longitudeCellsCount + 1) + j];
                // distance of the vertex from the point
                float distance = Vector2.Distance(new Vector2(vertex.x, vertex.z), new Vector2(point.x, point.z));
                if (distance <= range)
                {
                    pointsWithinRange.Add(new Vector2Int(i, j));
                }
            }
        }

    }
    
    /// <summary>
    /// Get rough estimate of vertices that are in range 
    /// </summary>
    /// <param name="smoothingCenter">the point around which we want to smooth the terrain</param>
    /// <param name="vertices">the vertices of the terrain</param> 
    /// <param name="range">the range within which we want to smooth the terrain</param> 
    /// <param name="iMinIndex"></param> 
    /// <param name="iMaxIndex"></param>
    /// <param name="jMinIndex"></param>
    /// <param name="jMaxIndex"></param>
    void getMinAndMaxIndicesOfVerticesWithinRangeFromPoint(Vector3 smoothingCenter, Vector3[] vertices, float range, ref int iMinIndex, ref int iMaxIndex, ref int jMinIndex, ref int jMaxIndex)
    {
        // Define the bounding box of the circle from which we will get the points
        float zMinimum = smoothingCenter.z - range, 
            zMaximum = smoothingCenter.z + range, 
            xMinimum = smoothingCenter.x - range, 
            xMaximum = smoothingCenter.x + range;

        iMinIndex = (latitudeCellsCount + 1) - 1;
        iMaxIndex = 0;
        jMinIndex = (latitudeCellsCount + 1) - 1;
        jMaxIndex = 0;
        
        for (int i = 0; i < latitudeCellsCount + 1; i++)
        {
            float zCoordinateOfRow = vertices[i * (longitudeCellsCount + 1) + 0].z;
            if (zCoordinateOfRow >= zMinimum && zCoordinateOfRow <= zMaximum)
            {
                if (i <= iMinIndex)
                {
                    iMinIndex = i;
                }

                if (i >= iMaxIndex)
                {
                    iMaxIndex = i;
                }
            }
        }

        for (int j = 0; j < longitudeCellsCount + 1; j++)
        {
            float xCoordinateOfColumn = vertices[0 * (longitudeCellsCount + 1) + j].x;
            if (xCoordinateOfColumn >= xMinimum && xCoordinateOfColumn <= xMaximum)
            {
                if (j <= jMinIndex)
                {
                    jMinIndex = j;
                }

                if (j >= jMaxIndex)
                {
                    jMaxIndex = j;
                }
            }
        }
        
    }

    float getAverageHeightOfPoints(List<Vector2Int> pointsWithinRange, Vector3[] vertices)
    {
        int counter = 0;
        float sum = 0; 
        
        foreach (Vector2Int indices in pointsWithinRange)
        {
            int i = indices.x;
            int j = indices.y;
            Vector3 point = vertices[i*(longitudeCellsCount +1) + j];

            sum += point.y;
            counter++;
        }

        float average = sum / counter;
        return average;
    }

    /// <summary>
    /// Level the vertices around the point using their average height
    /// </summary>
    /// <param name="point">center of smoothing</param>
    /// <param name="vertices">all vertices of the chunk terrain</param>
    /// <param name="verticesWithinRange">the affected vertices in range</param>
    /// <param name="averageHeightWithinRange">average height of the affected vertices</param>
    void smoothVertices(Vector3 point, Vector3[] vertices, List<Vector2Int> verticesWithinRange, float averageHeightWithinRange)
    {
        foreach (Vector2Int vertexIndices in verticesWithinRange)
        {
            // Get column and row index of vertex
            int i = vertexIndices.x;
            int j = vertexIndices.y;
            // If it's on the edge, ignore it
            if (i == 0 || j == 0 || i == latitudeCellsCount + 1 - 1 || j == longitudeCellsCount + 1 - 1)
            {
                continue;
            }
            Vector3 vertex = vertices[i*(longitudeCellsCount +1) + j];
            // Distance of vertex from point
            float distance = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(vertex.x, vertex.z));
            // convert distance to 0..1
            float normalizedDistance = distance / terrainSmoothingRange;
            // convert to 1..0 and smooth the ends
            float alpha = Mathf.SmoothStep(1f, 0f, normalizedDistance);
            // Lerp between vertex height and average height
            vertex.y = Mathf.Lerp(vertex.y, averageHeightWithinRange, alpha);
            // Put the vertex back
            vertices[i * (longitudeCellsCount + 1) + j] = vertex;
        }
    }

    /// <summary>
    /// Stitch normals of neighboring chunk terrain together so that lighting is continuous
    /// </summary>
    void stitchNormalsOfNeighboringChunksTogether()
    {
        // first neighbor is the one neighboring from top and then they go clockwise
        long[] neighbors = GeohashCalculations.getGeohashNeighbours(this.geohash);
        
        // go through direct neighbors 0 2 4 6 (neighboring via edge)
        int neighborNumber;
        neighborNumber = 0;
        tryToStitchNormalsWithDirectNeighbor(neighbors[neighborNumber], neighborNumber);
        neighborNumber = 2;
        tryToStitchNormalsWithDirectNeighbor(neighbors[neighborNumber], neighborNumber);
        neighborNumber = 4;
        tryToStitchNormalsWithDirectNeighbor(neighbors[neighborNumber], neighborNumber);
        neighborNumber = 6;
        tryToStitchNormalsWithDirectNeighbor(neighbors[neighborNumber], neighborNumber);
        
        // go through diagonal neighbors 1 3 5 7 (neighboring via corner)
        neighborNumber = 1;
        tryToStitchNormalsAtCorners(neighbors[0], neighbors[1], neighbors[2], neighborNumber);
        neighborNumber = 3;
        tryToStitchNormalsAtCorners(neighbors[2], neighbors[3], neighbors[4], neighborNumber);
        neighborNumber = 5;
        tryToStitchNormalsAtCorners(neighbors[4], neighbors[5], neighbors[6], neighborNumber);
        neighborNumber = 7;
        tryToStitchNormalsAtCorners(neighbors[6], neighbors[7], neighbors[0], neighborNumber);
        
    }
    
    void tryToStitchNormalsAtCorners(long neighbor1Geohash, long neighbor2Geohash, long neighbor3Geohash, int neighborNumber)
    {
        Chunk neighbor1 = ChunkLoader.instance.tryToGetChunk(neighbor1Geohash);
        Chunk neighbor2 = ChunkLoader.instance.tryToGetChunk(neighbor2Geohash);
        Chunk neighbor3 = ChunkLoader.instance.tryToGetChunk(neighbor3Geohash);
        
        if (   neighbor1 is null
            || neighbor2 is null
            || neighbor3 is null
            || !neighbor1.finishedLoading
            || !neighbor2.finishedLoading
            || !neighbor3.finishedLoading )
        {
            return;
        }
        
        // stitch corners of these chunks together
        MeshFilter neighbor1MeshFilter = neighbor1.terrain.GetComponent<MeshFilter>();
        Mesh neighbor1Mesh = neighbor1MeshFilter.mesh;
        MeshFilter neighbor2MeshFilter = neighbor2.terrain.GetComponent<MeshFilter>();
        Mesh neighbor2Mesh = neighbor2MeshFilter.mesh;
        MeshFilter neighbor3MeshFilter = neighbor3.terrain.GetComponent<MeshFilter>();
        Mesh neighbor3Mesh = neighbor3MeshFilter.mesh;
        
        MeshFilter thisMeshFilter = this.terrain.GetComponent<MeshFilter>();
        Mesh thisMesh = thisMeshFilter.mesh;
            
        // Unity Documentation Note: To make changes to the normals it is important to copy the normals from the Mesh. Once the normals have been copied and changed the normals can be reassigned back to the Mesh.
        // Copy normals of the 4 neighbors around the given corner
        Vector3[] nNormals1 = neighbor1Mesh.normals;
        Vector3[] nNormals2 = neighbor2Mesh.normals;
        Vector3[] nNormals3 = neighbor3Mesh.normals;
        Vector3[] tNormals = thisMesh.normals;

        // defining minimum and maximum indices possible. Indices where the terrain edges are
        int minRow = 0;
        int maxRow = (latitudeCellsCount + 1) - 1;
        int minColumn = 0;
        int maxColumn = (longitudeCellsCount + 1) - 1;

        // vertices we will look at
        Vector3 v1;
        Vector3 v2;
        Vector3 v3;
        Vector3 v4;
        // the resulting vertex
        Vector3 res;

        if (neighborNumber == 1) // top right corner of this chunk
        {
            // left top neighbor
            v1 = nNormals1[minRow * (longitudeCellsCount + 1) + maxColumn];
            // right top neighbor
            v2 = nNormals2[minRow * (longitudeCellsCount + 1) + minColumn];
            // right bottom neighbor
            v3 = nNormals3[maxRow * (longitudeCellsCount + 1) + minColumn];
            // this chunk
            v4 = tNormals[maxRow * (longitudeCellsCount + 1) + maxColumn];
            // combine
            res = Vector3.Normalize(v1 + v2 + v3 + v4);
            // put normals into the copied normals array
            nNormals1[minRow * (longitudeCellsCount + 1) + maxColumn] = res;
            nNormals2[minRow * (longitudeCellsCount + 1) + minColumn] = res;
            nNormals3[maxRow * (longitudeCellsCount + 1) + minColumn] = res;
            tNormals[maxRow * (longitudeCellsCount + 1) + maxColumn] = res;
        } else if (neighborNumber == 3) // bottom right corner
        {
            v1 = nNormals1[minRow * (longitudeCellsCount + 1) + minColumn];
            v2 = nNormals2[maxRow * (longitudeCellsCount + 1) + minColumn];
            v3 = nNormals3[maxRow * (longitudeCellsCount + 1) + maxColumn];
            v4 = tNormals[minRow * (longitudeCellsCount + 1) + maxColumn];
            res = Vector3.Normalize(v1 + v2 + v3 + v4);
            nNormals1[minRow * (longitudeCellsCount + 1) + minColumn] = res;
            nNormals2[maxRow * (longitudeCellsCount + 1) + minColumn] = res;
            nNormals3[maxRow * (longitudeCellsCount + 1) + maxColumn] = res;
            tNormals[minRow * (longitudeCellsCount + 1) + maxColumn] = res;
        } else if (neighborNumber == 5) // bottom left corner
        {
            v1 = nNormals1[maxRow * (longitudeCellsCount + 1) + minColumn];
            v2 = nNormals2[maxRow * (longitudeCellsCount + 1) + maxColumn];
            v3 = nNormals3[minRow * (longitudeCellsCount + 1) + maxColumn];
            v4 = tNormals[minRow * (longitudeCellsCount + 1) + minColumn];
            res = Vector3.Normalize(v1 + v2 + v3 + v4);
            nNormals1[maxRow * (longitudeCellsCount + 1) + minColumn] = res;
            nNormals2[maxRow * (longitudeCellsCount + 1) + maxColumn] = res;
            nNormals3[minRow * (longitudeCellsCount + 1) + maxColumn] = res;
            tNormals[minRow * (longitudeCellsCount + 1) + minColumn] = res;
        } else if (neighborNumber == 7) // top left corner
        {
            v1 = nNormals1[maxRow * (longitudeCellsCount + 1) + maxColumn];
            v2 = nNormals2[minRow * (longitudeCellsCount + 1) + maxColumn];
            v3 = nNormals3[minRow * (longitudeCellsCount + 1) + minColumn];
            v4 = tNormals[maxRow * (longitudeCellsCount + 1) + minColumn];
            res = Vector3.Normalize(v1 + v2 + v3 + v4);
            nNormals1[maxRow * (longitudeCellsCount + 1) + maxColumn] = res;
            nNormals2[minRow * (longitudeCellsCount + 1) + maxColumn] = res;
            nNormals3[minRow * (longitudeCellsCount + 1) + minColumn] = res;
            tNormals[maxRow * (longitudeCellsCount + 1) + minColumn] = res;
        }
        
        // assign normals back to the Mesh component
        neighbor1Mesh.normals = nNormals1;
        neighbor2Mesh.normals = nNormals2;
        neighbor3Mesh.normals = nNormals3;
        thisMesh.normals = tNormals;
    }

    /// <summary>
    /// Try to stitch the terrain normals of chunks that are direct neighbors together
    /// </summary>
    /// <param name="neighborGeohash"></param>
    /// <param name="neighborNumber">So that we know in what direction the chunks are neighboring</param>
    void tryToStitchNormalsWithDirectNeighbor(long neighborGeohash, int neighborNumber)
    {
        Chunk neighbor = ChunkLoader.instance.tryToGetChunk(neighborGeohash);
        if (neighbor is null)
        {
            return;
        }

        // stop if the neighbor didnt finish loading yet - once the neighbor finishes, it will attempt stitching with this chunk
        if (!neighbor.finishedLoading)
        {
            return;
        }
        
        // stitch this chunk with top neighbor, disregard corners
        MeshFilter neighborMeshFilter = neighbor.terrain.GetComponent<MeshFilter>();
        Mesh neighborMesh = neighborMeshFilter.mesh;
        MeshFilter thisMeshFilter = this.terrain.GetComponent<MeshFilter>();
        Mesh thisMesh = thisMeshFilter.mesh;
            
        // Unity Documentation Note: To make changes to the vertices it is important to copy the vertices from the Mesh. Once the vertices have been copied and changed the vertices can be reassigned back to the Mesh.
        Vector3[] nNormals = neighborMesh.normals;
        Vector3[] tNormals = thisMesh.normals;

        if (neighborNumber == 0) // top neighbor
        {
            for (int j = 1; j < (longitudeCellsCount +1) - 1; j++)
            {
                int nColumn = j;
                int nRow = 0;
                int tColumn = j;
                int tRow = (latitudeCellsCount + 1) - 1;
                Vector3 nNormal = nNormals[nColumn + nRow * (longitudeCellsCount + 1)];
                Vector3 tNormal = tNormals[tColumn + tRow * (longitudeCellsCount + 1)];
                Vector3 res = Vector3.Normalize((0.5f * nNormal + 0.5f * tNormal));
                nNormals[nColumn + nRow * (longitudeCellsCount + 1)] = res;
                tNormals[tColumn + tRow * (longitudeCellsCount + 1)] = res;
            }
        } else if (neighborNumber == 2) // right neighbor
        {
            for (int i = 1; i < (latitudeCellsCount +1) - 1; i++)
            {
                int nColumn = 0;
                int nRow = i;
                int tColumn = (longitudeCellsCount +1) -1;
                int tRow = i;
                Vector3 nNormal = nNormals[nColumn + nRow * (longitudeCellsCount + 1)];
                Vector3 tNormal = tNormals[tColumn + tRow * (longitudeCellsCount + 1)];
                Vector3 res = Vector3.Normalize((0.5f * nNormal + 0.5f * tNormal));
                nNormals[nColumn + nRow * (longitudeCellsCount + 1)] = res;
                tNormals[tColumn + tRow * (longitudeCellsCount + 1)] = res;
            }
        } else if (neighborNumber == 4) // bottom neighbor
        {
            for (int j = 1; j < (longitudeCellsCount +1) - 1; j++)
            {
                int nColumn = j;
                int nRow = (latitudeCellsCount + 1) - 1;
                int tColumn = j;
                int tRow = 0;
                Vector3 nNormal = nNormals[nColumn + nRow * (longitudeCellsCount + 1)];
                Vector3 tNormal = tNormals[tColumn + tRow * (longitudeCellsCount + 1)];
                Vector3 res = Vector3.Normalize((0.5f * nNormal + 0.5f * tNormal));
                nNormals[nColumn + nRow * (longitudeCellsCount + 1)] = res;
                tNormals[tColumn + tRow * (longitudeCellsCount + 1)] = res;
            }
        } else if (neighborNumber == 6) // left neighbor
        {
            for (int i = 1; i < (latitudeCellsCount +1) - 1; i++)
            {
                int nColumn = (longitudeCellsCount +1) -1;
                int nRow = i;
                int tColumn = 0;
                int tRow = i;
                Vector3 nNormal = nNormals[nColumn + nRow * (longitudeCellsCount + 1)];
                Vector3 tNormal = tNormals[tColumn + tRow * (longitudeCellsCount + 1)];
                Vector3 res = Vector3.Normalize((0.5f * nNormal + 0.5f * tNormal));
                nNormals[nColumn + nRow * (longitudeCellsCount + 1)] = res;
                tNormals[tColumn + tRow * (longitudeCellsCount + 1)] = res;
            }
        }

        neighborMesh.normals = nNormals;
        thisMesh.normals = tNormals;
    }

    /// <summary>
    /// Do physics raycast from the points of buildings and streets to the terrain. And move the buildings and streets.
    /// </summary>
    private void tryToSnapBuildingsAndStreetsToTerrain()
    {
        if (!(terrainGenerationDone && mapGenerationDone))
        {
            return;
        }

        Debug.Log("OSM and Elevation data downloaded");
        
        GameObject meshGameObject = this.terrain;
        GameObject buildings = this.buildings;

        // for every building
        for (int i = 0; i < buildings.transform.childCount; i++)
        {
            Transform childTransform = buildings.transform.GetChild(i);
            GameObject building = childTransform.gameObject;
            Vector3[] outline = building.GetComponent<BuildingData>().outlineVertices;
            
            float minY = float.MaxValue;
            // for every point of the building outline do raycast and find the lowest point when projected onto the terrain
            for (int j = 0; j < outline.Length; j++)
            {
                Vector3 hitPoint = Vector3.zero;

                if (snapPointToTerrain(outline[j], ref hitPoint))
                {
                    float y = hitPoint.y;
                    minY = Math.Min(y, minY);
                }
                
            }

            // if raycast was successful
            if (minY < 20000f)
            {
                // move the building to the terrain
                building.transform.position += new Vector3(0, minY, 0);
            }
            
        }
        
        GameObject streets = this.streets;
        // for every street
        for (int i = 0; i < streets.transform.childCount; i++)
        {
            Transform childTransform = streets.transform.GetChild(i);
            GameObject street = childTransform.gameObject;

            Vector3[] vertices = street.GetComponent<StreetData>().vertices;

            // for every street vertex
            for (int j = 0; j < vertices.Length; j++)
            {
                // do raycast
                Vector3 hitPoint = Vector3.zero;
                if (snapPointToTerrain(vertices[j], ref hitPoint))
                {
                    float y = hitPoint.y;
                    // move vertex to the point hit + small height offset
                    vertices[j].y = y + streetHeightOffset;
                }
                
            }

            // assign the vertices back to the LineRenderer
            LineRenderer lineRenderer = street.GetComponent<LineRenderer>();
            lineRenderer.positionCount = vertices.Length;
            lineRenderer.SetPositions(vertices);

        }




    }

    /// <summary>
    /// Do raycast to terrain
    /// </summary>
    /// <param name="point"></param>
    /// <param name="hitPoint"></param>
    /// <returns></returns>
    public static bool snapPointToTerrain(Vector3 point, ref Vector3 hitPoint)
    {
        Vector3 rayOrigin = new Vector3(point.x, 2000f, point.z);
        Vector3 rayDirection = Vector3.down;
        RaycastHit hit;
        bool raycastSuccessful = Physics.Raycast(rayOrigin, rayDirection, out hit, 4000f, 1 << 3); // 3 is probably the id of terrain layer in Unity // bit mask of the layers
        hitPoint = hit.point;
        return raycastSuccessful;
    }
    
    public bool isGpsPointWithinChunk(GpsVector point)
    {
        if (point.latitude <= this.boundingBox.Maximum.Lat
            && point.latitude >= this.boundingBox.Minimum.Lat
            && point.longitude <= this.boundingBox.Maximum.Lon
            && point.longitude >= this.boundingBox.Minimum.Lon)
        {
            return true;
        }
        
        return false;
        
    }
    
    
}


