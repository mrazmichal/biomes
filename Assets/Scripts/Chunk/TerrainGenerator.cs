using Newtonsoft.Json;
using NGeoHash;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// <summary>
/// Download elevation data and generate chunk terrain
/// </summary>
/// <author>Michal Mráz</author>
public class TerrainGenerator : MonoBehaviour
{
    // Dimensions of input data
    // limit is 512 locations per request to Google Elevation API - 19 * 25 is 475
    internal int latPointsCount = 19; // (number of cells in latitude dimension is numberOfLatitudePoints -1)
    internal int lonPointsCount = 25; // in Prague one square is then approximately 16 meters x 16 meters

    // number of main cell subdivisons in one dimension
    // example: if 3, then one main cell will have 3x3 subcells, if 2 then 2x2 subcells
    // was tested just for 2
    internal int cellSubdivisionsCount = 2; // is 1 or bigger // 1 means no subdivision happens

    internal int subdivided_latPointsCount;
    internal int subdivided_lonPointsCount;
    
    const int TERRAIN_LAYER = 3;

    Chunk chunk;
    
    public void Init(Chunk chunk)
    {
        this.chunk = chunk;

        subdivided_latPointsCount = (latPointsCount - 1) * (cellSubdivisionsCount - 1) + latPointsCount;
        subdivided_lonPointsCount = (lonPointsCount - 1) * (cellSubdivisionsCount - 1) + lonPointsCount;
    }

    /// <summary>
    /// Store information about terrain point.
    /// </summary>
    public struct TerrainPoint
    {
        public GpsVector gps;
        public Vector3 pos;
    }

    /// <summary>
    /// Type of object that we will deserialize the downloaded JSON into
    /// </summary>
    [System.Serializable]
    public class ElevationData
    {
        public ElevationResult[] results;
        public string status;
    }

    [System.Serializable]
    public class ElevationResult
    {
        public float elevation;
        public Location location;
        public float resolution;
    }

    [System.Serializable]
    public class Location
    {
        public float lat;
        public float lng;
    }


    private string constructRequestString(TerrainPoint[] points)
    {
        StringBuilder sb = new StringBuilder();
        bool atLeastOneWord = false;
        string pointsString;

        for (int i = 0; i < points.Length; i++)
        {
            if (atLeastOneWord)
            {
                sb.Append("|");
            }
            else
            {
                atLeastOneWord = true;
            }
            
            ref GpsVector gps = ref points[i].gps;
            sb.Append(gps.latitude.ToString("F6") + "," + gps.longitude.ToString("F6")); 
        }

        pointsString = sb.ToString();
        return pointsString;
    }

    string json;

    /// <summary>
    /// Download elevation data and build chunk terrain from it
    /// </summary>
    /// <returns></returns>
    public IEnumerator generateTerrain()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Restart();
        
        BoundingBox bbox = chunk.boundingBox;
        TerrainPoint[] points = generatePoints(bbox);
        string pointsString = constructRequestString(points);        

        string url = "https://maps.googleapis.com/maps/api/elevation/json?locations=" + pointsString + "&key=" + Settings.googleApiKey;
        json = "";

        yield return Downloading.downloadDataFromUrl(url, (result) => json = result);
        
        // Deserialize JSON into prepared class
        ElevationData elevationData = JsonConvert.DeserializeObject<ElevationData>(json);

        if (elevationData.status != "OK")
        {
            Debug.LogError("Elevation API status not OK");
            yield break;
        }
        
        stopwatch.Stop();
        Debug.Log("Downloading elevation data took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + chunk.geohash);
        stopwatch.Restart();

        // Loop through the results array and extract the latitude and longitude data
        int i = 0;
        foreach (ElevationResult result in elevationData.results)
        {
            if (i < points.Length)
            {
                points[i].pos.y = result.elevation;
            }
            i++;
        }

        // Subdivide the terrain points so that terrain smoothing has more points to work with
        TerrainPoint[] pointsForMesh;
        if (cellSubdivisionsCount != 1)
        {
            pointsForMesh = subdivideTerrainPoints(points);
        } else
        {
            pointsForMesh = points;
        }
    
        createTerrainMesh(pointsForMesh);     
        
        stopwatch.Stop();
        Debug.Log("Processing elevation data took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + chunk.geohash);

        chunk.informThatTerrainGenerationDone();
        
    }
    
    /// <summary>
    ///  Generate terrain points inside given bounding box
    /// </summary>
    /// <param name="bbox"></param>
    /// <returns></returns>
    public TerrainPoint[] generatePoints(BoundingBox bbox)
    {
        GpsVector min = new GpsVector(bbox.Minimum.Lat, bbox.Minimum.Lon);
        GpsVector max = new GpsVector(bbox.Maximum.Lat, bbox.Maximum.Lon);

        double latDiff = max.latitude - min.latitude;
        double lonDiff = max.longitude - min.longitude;

        double latPartSize = latDiff / (latPointsCount - 1); // divide by count of areas between points
        double lonPartSize = lonDiff / (lonPointsCount - 1);

        // create 2D grid
        TerrainPoint[] points = new TerrainPoint[latPointsCount * lonPointsCount];
             
        // assign lat lng to points
        for (int i = 0; i < latPointsCount; i++)
        {
            for (int j = 0; j < lonPointsCount; j++)
            {
                ref GpsVector gpsPoint = ref points[i * lonPointsCount + j].gps;
                gpsPoint.latitude = min.latitude + i * latPartSize;
                gpsPoint.longitude = min.longitude + j * lonPartSize;
            }
        }

        // assign converted coords
        for (int i = 0; i < latPointsCount; i++)
        {
            for (int j = 0; j < lonPointsCount; j++)
            {
                ref GpsVector gpsPoint = ref points[i * lonPointsCount + j].gps;
                Vector3 unityCoords = Gps.instance.ConvertGpsToUnityCoords(new GpsVector(gpsPoint.latitude, gpsPoint.longitude));
                points[i * lonPointsCount + j].pos = unityCoords;
            }
        }

        return points;
    }

    /// <summary>
    /// Interpolate between terrain points using parameters to create new points
    /// </summary>
    //TerrainPoint res = BilinearInterpolation(bottomLeft_mainCellCorner, topLeft_mainCellCorner, bottomRight_mainCellCorner, topRight_mainCellCorner, alpha, beta);
    public TerrainPoint BilinearInterpolation(TerrainPoint bl, TerrainPoint tl, TerrainPoint br, TerrainPoint tr, float u, float v)
    {
        // Bilinear interpolation of gps
        GpsVector lGps = GpsVector.Lerp(bl.gps, tl.gps, u);
        GpsVector rGps = GpsVector.Lerp(br.gps, tr.gps, u);
        GpsVector resGps = GpsVector.Lerp(lGps, rGps, v);
        
        // Bilinear interpolation of unity coordinates (to get elevation)
        Vector3 lPos = Vector3.Lerp(bl.pos, tl.pos, u);
        Vector3 rPos = Vector3.Lerp(br.pos, tr.pos, u);
        Vector3 resPos = Vector3.Lerp(lPos, rPos, v);

        Vector3 convertedGps = Gps.instance.ConvertGpsToUnityCoords(resGps);
        
        TerrainPoint res = new TerrainPoint();
        res.gps = resGps;
        res.pos = convertedGps;
        res.pos.y = resPos.y; // apply the interpolated elevation

        return res;
    }

    /// <summary>
    /// Create new terrain points, interpolate their elevation and randomly modify the resulting elevation.
    /// We assume that the input points are corners of "main cells" which this function subdivides.
    /// </summary>
    /// <param name="inputPoints">the points with elevation data that we got from Google Elevation API</param>
    /// <returns>Corner points of the smaller cells that were created by subdivision of the larger input cells</returns>
    private TerrainPoint[] subdivideTerrainPoints(TerrainPoint[] inputPoints)
    {
        // we write here latitude and longitude, but it's actually x and z coordinates
        // we operate in meters

        // cells are the squares, points are the corners of the cells. Important!
        int latCellsCount = latPointsCount - 1;
        int lonCellsCount = lonPointsCount - 1;

        // number of subdivision cells in the lat dimension
        int subdivided_latCellsCount = cellSubdivisionsCount * latCellsCount;
        int subdivided_lonCellsCount = cellSubdivisionsCount * lonCellsCount;
        int subdivided_latPointsCount = subdivided_latCellsCount + 1;
        int subdivided_lonPointsCount = subdivided_lonCellsCount + 1;

        int subdivided_allPointsCount = subdivided_latPointsCount * subdivided_lonPointsCount;

        TerrainPoint[] subdivided_points = new TerrainPoint[subdivided_allPointsCount];

        // Go through 1D "inputPoints" array and create a 2D "mainPoints" array
        TerrainPoint[,] mainPoints = new TerrainPoint[latPointsCount, lonPointsCount];
        for (int k = 0; k < inputPoints.Length; k++)
        {
            int i = k / (lonPointsCount);
            int j = k % (lonPointsCount);
            mainPoints[i, j] = inputPoints[k];
        }

        for (int i = 0; i < subdivided_latPointsCount; i++)
        {
            for (int j = 0; j < subdivided_lonPointsCount; j++)
            {
                // compute in which main cell we are 
                int mainLatCellId = i / cellSubdivisionsCount;
                int mainLonCellId = j / cellSubdivisionsCount;
                float alpha = (float)(i % cellSubdivisionsCount) / cellSubdivisionsCount;
                float beta = (float)(j % cellSubdivisionsCount) / cellSubdivisionsCount;

                // Clamp
                if (mainLatCellId > latCellsCount - 1)
                {
                    mainLatCellId = latCellsCount - 1;
                    alpha = 1;
                }
                if (mainLatCellId < 0)
                {
                    mainLatCellId = 0;
                    alpha = 0;
                }
                if (mainLonCellId > lonCellsCount - 1)
                {
                    mainLonCellId = lonCellsCount - 1;
                    beta = 1;
                }
                if (mainLonCellId < 0)
                {
                    mainLonCellId = 0;
                    beta = 0;
                }

                TerrainPoint bottomLeft_mainCellCorner = mainPoints[mainLatCellId, mainLonCellId];
                TerrainPoint topLeft_mainCellCorner = mainPoints[mainLatCellId +1, mainLonCellId];
                TerrainPoint bottomRight_mainCellCorner = mainPoints[mainLatCellId, mainLonCellId +1];
                TerrainPoint topRight_mainCellCorner = mainPoints[mainLatCellId +1, mainLonCellId +1];

                TerrainPoint res = BilinearInterpolation(bottomLeft_mainCellCorner, topLeft_mainCellCorner, bottomRight_mainCellCorner, topRight_mainCellCorner, alpha, beta);
                
                if ((i % cellSubdivisionsCount == 0) || (j % cellSubdivisionsCount == 0))
                {
                    // Randomly modify elevation of the newly created points
                    // use seed based on gps of the point 
                    int seed = (int)((res.gps.latitude + res.gps.longitude * 0.005) * 10000);
                    UnityEngine.Random.InitState(seed);
                    res.pos.y += UnityEngine.Random.Range(-0.7f, 0.7f);
                }
                
                subdivided_points[j + i * subdivided_lonPointsCount] = res;

            }
        }

        return subdivided_points;
    }

    /// <summary>
    /// The points will become vertices of the terrain mesh
    /// </summary>
    /// <param name="points"></param>
    private void createTerrainMesh(TerrainPoint[] points)
    {
        GameObject terrain = chunk.terrain;
        terrain.layer = TERRAIN_LAYER;

        // Create mesh
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = terrain.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrain.AddComponent<MeshRenderer>();

        // init vertices, triangles, uvs
        Vector3[] vertices = new Vector3[points.Length];
        int[] triangles = new int[(subdivided_latPointsCount - 1) * (subdivided_lonPointsCount - 1) * 6]; // 2 triangles for every square, each triangle has 3 points
        Vector2[] uvs = new Vector2[points.Length];

        // find minimum and maximum x and z coordinatesw of all points - used as bounds for UV calculation
        ref Vector3 minPoint = ref points[0].pos;
        ref Vector3 maxPoint = ref points[points.Length - 1].pos;
        float xSize = maxPoint.x - minPoint.x;
        float zSize = maxPoint.z - minPoint.z;
        float minX = minPoint.x;
        float minZ = minPoint.z;

        // input points locations and uvs
        for (int i = 0; i < points.Length; i++)
        {
            ref Vector3 point = ref points[i].pos;
            vertices[i] = point;
            uvs[i] = new Vector2((point.x - minX) / xSize, (point.z - minZ) / zSize); // could be used for textures
        }

        // construct triangles
        int index = 0;
        for (int i = 0; i < subdivided_latPointsCount - 1; i++)
        {
            for (int j = 0; j < subdivided_lonPointsCount - 1; j++)
            {
                // triangles contain indices of vertices
                triangles[index + 0] = i * subdivided_lonPointsCount + j;
                triangles[index + 1] = (i + 1) * subdivided_lonPointsCount + j;
                triangles[index + 2] = i * subdivided_lonPointsCount + j + 1;
                triangles[index + 3] = (i + 1) * subdivided_lonPointsCount + j + 1;
                triangles[index + 4] = i * subdivided_lonPointsCount + j + 1;
                triangles[index + 5] = (i + 1) * subdivided_lonPointsCount + j;
                index += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals(); // otherwise lighting issues
        meshRenderer.material.color = Color.red;
        
        meshFilter.mesh = mesh;

        MeshCollider collider = terrain.AddComponent<MeshCollider>();

        Material material = MaterialPicker.instance.defaultTerrainMaterial;

        terrain.GetComponent<MeshRenderer>().material = material;

    }
    
}
