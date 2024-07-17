using NGeoHash;
using System;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

/// <summary>
/// Generate areas for biomes, assign biomes to those areas, generate vegetation
/// </summary>
/// <author>Michal Mráz</author>
public class BiomeGenerator
{
    // biome and vegetation generator data
    int numberOfBiomeCentersInOneDimension = 2;
    GpsVector[,] biomeCentersGps;
    Vector2[,] biomeCenters2D;
    int[,] biomeCentersColors;
    int[,] cellColors_extended;
    
    int[,] logicalMatrixBiomes;
    bool[,] logicalMatrixIsOccupied;
    Vector3[,] logicalMatrixPoints;

    float cellWidthInX;
    float cellHeightInZ;
    Vector3 bBoxMin;
    Vector3 bBoxMax;

    Chunk chunk;
    
    // The fix is height/width of the chunk. Is used to correctly scale the noise. For example in Prague the size of a chunk is about 392.842 meters in width and 305.748 meters in height.
    float latitudeFix = 0.7782976362f;

    const int VEGETATION_LAYER = 7;
    
    const int DESERT = 0;
    const int FOREST = 1;
    const int BEACH = 2;
    const int PLAINS = 3;
    
    public GameObject swordPrefab = ItemsPicker.instance.items[0];
    public GameObject shieldPrefab = ItemsPicker.instance.items[1];

    int randomGeneratorSeed;

    float minDistanceForQuestItems = 200f;
    
    public BiomeGenerator(Chunk chunk)
    {
        this.chunk = chunk;
    }
    
    public void generateBiomes()
    {
        calculateBBox();
        calculateCellSize();
        // calculateLatitudeFix();
        
        //generate Biome centers - here and in neighboring chunks to keep adjacency
        generateBiomeCentersGps();
        
        // seed based on geohash value
        randomGeneratorSeed = (int)(this.chunk.geohash % Int32.MaxValue);
        Random.InitState(randomGeneratorSeed);
        
        colorBiomeCenters();
        convertBiomeCentersGpsToUnityCoords();

        // we calculate 2 more cells in each direction - so that cell texture blending works also at chunk borders
        cellColors_extended = new int[this.chunk.latitudeCellsCount +4, this.chunk.longitudeCellsCount +4];
        colorCellsByBiomes_extended();
        
        Material newMaterial = createMaterialForTerrainShader();
        // assign the material to terrain
        this.chunk.terrain.GetComponent<MeshRenderer>().material = newMaterial;
        
        Vector2 sampleRegionSize = new Vector2(bBoxMax.x - bBoxMin.x, bBoxMax.z - bBoxMin.z);
        float minR = 2.5f;
        float maxR = 5.5f;
        // float minR = 2.0f;
        // float maxR = 3.5f;
        float meanR = minR + (maxR - minR) / 2;
        int numSamplesBeforeRejection = 30;
        List<VariableRadiiPoissonDiscSampling.VirtualPoint> virtualVegetationPoints = VariableRadiiPoissonDiscSampling.generatePoints(sampleRegionSize, minR, maxR, meanR, numSamplesBeforeRejection);
        
        // visualizeVirtualVegetationPoints(virtualVegetationPoints);
        
        generateVegetation(virtualVegetationPoints, meanR, bBoxMin);
        generateQuestItems();
        
        // good for visualization:
        //createAndSnapCentersOfBiomesPinsToTerrain();

    }
    
    /// <summary>
    /// Show how Variable radii poisson disc sampling works
    /// </summary>
    /// <param name="virtualVegetationPoints"></param>
    void visualizeVirtualVegetationPoints(List<VariableRadiiPoissonDiscSampling.VirtualPoint> virtualVegetationPoints)
    {
        foreach (VariableRadiiPoissonDiscSampling.VirtualPoint vp in virtualVegetationPoints)
        {
            Vector3 point = new Vector3(bBoxMin.x + vp.position.x, 1000, bBoxMin.z + vp.position.y);
            
            GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pin.transform.position = point;
            pin.transform.localScale = new Vector3(vp.radius *2, vp.radius *2, vp.radius *2);
            pin.transform.parent = chunk.transform;
            Renderer renderer = pin.GetComponent<Renderer>();
            renderer.material.color = UnityEngine.Color.red;
        }
    }

    /// <summary>
    ///  Latitude fix did problems with generation stability, so it is not used right now
    /// </summary>
    void calculateLatitudeFix()
    {
        // In prague the size of a chunk is about 392.842 meters in width and 305.748 meters in height, the fix is height/width
        latitudeFix = (float)(bBoxMax.z - bBoxMin.z) / (bBoxMax.x - bBoxMin.x);
    }

    void generateVegetation(List<VariableRadiiPoissonDiscSampling.VirtualPoint> virtualPoints, float meanR, Vector3 bBoxMin)
    {

        foreach (VariableRadiiPoissonDiscSampling.VirtualPoint vp in virtualPoints)
        {
            // Discard some points based on vegetation density value
            float value = Random.Range(0f, 1f);
            if (value > Settings.vegetationDensity)
            {
                continue;
            }

            // Proto position
            Vector3 point = new Vector3(bBoxMin.x + vp.position.x, 0, bBoxMin.z + vp.position.y);
            
            // lushness using perlin noise - make meadows in some places
            GpsVector pointGpsCoords = Gps.instance.ConvertUnityCoordsToGps(point);
            float lushnessScale = 369f;
            value = Mathf.PerlinNoise((float) pointGpsCoords.latitude * lushnessScale, (float) pointGpsCoords.longitude * lushnessScale);
            if (value > Settings.vegetationLushness)
            {
                continue;
            }
            
            // Scale
            float scale = vp.radius / meanR; // 4.0f is the average radius of the vegetation prefab
            
            // Snap position to terrain
            Vector3 snappedPoint = point;
            Vector3 hitPoint = Vector3.zero;
            if (Chunk.snapPointToTerrain(point, ref hitPoint))
            {
                snappedPoint = hitPoint;
            }

            // Rotation
            float angle = UnityEngine.Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            
            int biome = findBiomeOfPoint(snappedPoint);
            GameObject vegetationPrefab = chooseVegetationToPlant(biome);
            if ((System.Object)vegetationPrefab == null)
            {
                continue;
            }

            // Create vegetation gameObject
            GameObject obj = GameObject.Instantiate(vegetationPrefab, snappedPoint, rotation, chunk.vegetation.transform);
            obj.transform.localScale = new Vector3(1, 1, 1) * scale;
            obj.layer = VEGETATION_LAYER;

            // Set the vegetation for destruction if it collides with something
            obj.AddComponent<DestroyObjectOnCollision>();
            Rigidbody rigid = obj.AddComponent<Rigidbody>();
            rigid.isKinematic = true;
            rigid.useGravity = false;
            obj.GetComponent<Collider>().isTrigger = true;

        }
       
    }
    
    void generateQuestItems()
    {
        // in every chunk try to generate one sword in desert biome and one shield in forest biome
        
        int numOfTries = 20;

        bool swordGenerated = false;
        bool shieldGenerated = false;

        for (int k = 0; k < numOfTries; k++)
        {
            float x = Random.Range(bBoxMin.x, bBoxMax.x);
            float z = Random.Range(bBoxMin.z, bBoxMax.z);

            Vector3 point = new Vector3(x, 0, z);
            
            // If distance of the point from world center is too small, discard it (so players have to walk a bit)
            if (Vector2.Distance(new Vector2(point.x, point.z),Vector2.zero) < minDistanceForQuestItems)
            {
                continue;
            }
            
            int biome = findBiomeOfPoint(point);
            
            Vector3 snappedPoint = point;
            
            // Get position on terrain
            if ((biome == DESERT && !swordGenerated) || (biome == FOREST && !shieldGenerated))
            {
                Vector3 hitPoint = Vector3.zero;
                if (Chunk.snapPointToTerrain(point, ref hitPoint))
                {
                    snappedPoint = hitPoint;
                }

                snappedPoint.y += 8;
            } else
            {
                continue;
            }

            if (biome == DESERT && !swordGenerated)
            {
                GameObject sword = GameObject.Instantiate(swordPrefab, snappedPoint, Quaternion.identity, chunk.vegetation.transform);
                swordGenerated = true;
            } else if (biome == FOREST && !shieldGenerated)
            {
                GameObject shield = GameObject.Instantiate(shieldPrefab, snappedPoint, Quaternion.identity, chunk.vegetation.transform);
                shieldGenerated = true;
            }

            if (swordGenerated && shieldGenerated)
            {
                break;
            }
            
        }

    }

    /// <summary>
    /// Create special material for this chunk, that will color the terrain by biomes. The material is based on an existing material. 
    /// </summary>
    Material createMaterialForTerrainShader()
    {
        Texture2D texture = createBiomesTextureFromCellColors();
        // create new material as a copy of the materialForTerrainShader
        // and then modify it, each chunk has its own material
        Material material = MaterialPicker.instance.terrainShaderMaterial;
        Material newMaterial = new Material(material);
        newMaterial.SetTexture("_FifthTex", texture);
        
        // number of cells in width and in height
        double _WidthCellsCount = this.chunk.longitudeCellsCount;
        double _HeightCellsCount = this.chunk.latitudeCellsCount;
        // add +2 cells beyond chunk borders in every direction
        double _ExtendedWidthCellsCount = this.chunk.longitudeCellsCount + 4; 
        double _ExtendedHeightCellsCount = this.chunk.latitudeCellsCount + 4;
        // ratio by which we will stretch the uv to accomodate also the cells beyond chunk borders
        double _WidthRatio = _WidthCellsCount / _ExtendedWidthCellsCount;
        double _HeightRatio = _HeightCellsCount / _ExtendedHeightCellsCount;
        // cell size in the stretched uv coordinates
        double _CellWidthInUv = 1 / _ExtendedWidthCellsCount;
        double _CellHeightInUv = 1 / _ExtendedHeightCellsCount;
        
        newMaterial.SetFloat("_WidthCellsCount", (float)_WidthCellsCount);
        newMaterial.SetFloat("_HeightCellsCount", (float)_HeightCellsCount);
        newMaterial.SetFloat("_ExtendedWidthCellsCount", (float)_ExtendedWidthCellsCount);
        newMaterial.SetFloat("_ExtendedHeightCellsCount", (float)_ExtendedHeightCellsCount);
        newMaterial.SetFloat("_WidthRatio", (float)_WidthRatio);
        newMaterial.SetFloat("_HeightRatio", (float)_HeightRatio);
        newMaterial.SetFloat("_CellWidthInUv", (float)_CellWidthInUv);
        newMaterial.SetFloat("_CellHeightInUv", (float)_CellHeightInUv);
        
        newMaterial.name = "special terrain material for this chunk";

        return newMaterial;
        
    }

    /// <summary>
    /// This texture will be used to color the cells of terrain mesh by biome textures
    /// </summary>
    /// <returns></returns>
    Texture2D createBiomesTextureFromCellColors()
    {
        int textureHeight = cellColors_extended.GetLength(0);
        int textureWidth = cellColors_extended.GetLength(1);
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true);
        for (int i = 0; i < textureHeight; i++)
        {
            for (int j = 0; j < textureWidth; j++)
            {
                int value = cellColors_extended[i, j];
                Color color = encodeIntegerToColor(value);
                texture.SetPixel(j,i, color);
            }
        }
        
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        return texture;
    }
    
    Color encodeIntegerToColor(int value)
    {
        float normalizedValue = value / 255.0f;
        return new Color(normalizedValue, 0, 0, 1); // Assuming a red channel encoding
    }

    void colorCellsByBiomes_extended()
    {
        for (int i = 0; i < cellColors_extended.GetLength(0); i++)
        {
            float z = bBoxMin.z - 2* cellHeightInZ + cellHeightInZ / 2 + i * cellHeightInZ;
            
            for (int j = 0; j < cellColors_extended.GetLength(1); j++)
            {
                float x = bBoxMin.x - 2* cellWidthInX + cellWidthInX / 2 + j * cellWidthInX;

                Vector3 point = new Vector3(x, 0, z);
                
                int biome = findBiomeOfPoint(point);
                
                cellColors_extended[i, j] = biome;
            }
        }
        
    }
    
    /// <summary>
    /// Move the point using noise functions
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    Vector3 movePointForColoringByBiomes(Vector3 point)
    {
        GpsVector gps = Gps.instance.ConvertUnityCoordsToGps(point);
        Vector2 cellSizes = new Vector2(cellWidthInX, cellHeightInZ);

        int seedX, seedZ;
        float gpsScale, offsetsScale;
        Vector2 randomAddition = Vector2.zero;
        
        seedX = 723;
        seedZ = 189;
        gpsScale = 1150.0f;
        Vector2 biggerOffsets = generateOffsetUsingPerlinNoise(gps, seedX, seedZ, gpsScale);
        
        seedX = 500;
        seedZ = 1000;
        gpsScale = gpsScale*2;
        Vector2 smallerOffsets = generateOffsetUsingPerlinNoise(gps, seedX, seedZ, gpsScale);

        offsetsScale = 5.0f;
        randomAddition = biggerOffsets * cellSizes * offsetsScale;
        
        offsetsScale = offsetsScale /2;
        randomAddition += smallerOffsets * cellSizes * offsetsScale;
        
        point += new Vector3(randomAddition.x, 0, randomAddition.y);

        return point;

    }
    
    Vector2 generateOffsetUsingPerlinNoise(GpsVector gps, int seedX, int seedZ, float gpsScale)
    {
        // The input to Perlin noise are latitude and longitude of a gps point. Latitude is multiplied by a fix and longitude is divided by 2 so that the noise has similar scale in both directions. 
        float xOffset = Mathf.PerlinNoise(seedX + (float)gps.latitude * latitudeFix * gpsScale, 
            seedX + (float)gps.longitude / 2 * gpsScale);
        float zOffset = Mathf.PerlinNoise(seedZ + (float)gps.latitude * latitudeFix * gpsScale, 
            seedZ + (float)gps.longitude / 2 * gpsScale);
        
        // Transform the offset from range <0,1> to <-1,1>
        xOffset = (xOffset - 0.5f) *2;
        zOffset = (zOffset - 0.5f) *2;

        Vector2 offsets = new Vector2(xOffset, zOffset);
        return offsets;
    }

    /// <summary>
    /// Generate biome centers in the chunk.
    /// Also generate two more in every direction so that biomes in different chunks connect nicely.
    /// </summary>
    void generateBiomeCentersGps()
    {
        // minimum and maximum GPS of the chunk
        GpsVector minGps = new GpsVector(chunk.boundingBox.Minimum.Lat, chunk.boundingBox.Minimum.Lon);
        GpsVector maxGps = new GpsVector(chunk.boundingBox.Maximum.Lat, chunk.boundingBox.Maximum.Lon);

        double latDiff = maxGps.latitude - minGps.latitude;
        double lonDiff = maxGps.longitude - minGps.longitude;

        // Size of one area in which we generate one biome center
        double latPartSize = latDiff / numberOfBiomeCentersInOneDimension;
        double lonPartSize = lonDiff / numberOfBiomeCentersInOneDimension;

        // We generate 2 extra biome centers in each direction
        biomeCentersGps = new GpsVector[numberOfBiomeCentersInOneDimension + 4, numberOfBiomeCentersInOneDimension + 4];

        for (int i = 0; i < biomeCentersGps.GetLength(0); i++)
        {
            for (int j = 0; j < biomeCentersGps.GetLength(1); j++)
            {
                // At first we generate the biome center in the middle of it's area
                // we assume i is direction of latitude, north
                // j is direction of longitude, east
                biomeCentersGps[i, j] = new GpsVector(minGps.latitude - 2*latPartSize + i * latPartSize + latPartSize / 2,
                    minGps.longitude - 2*lonPartSize + j * lonPartSize + lonPartSize / 2);
                
                // The biome centers default positions are on a grid. If we didn't multiply by lonDiff, the centers that are on a diagonal would have the same seed. 
                // If we multiply by lonDiff, the centers with the same seed will be so far away that we don't have to care about them.
                int seed = (int)((biomeCentersGps[i, j].latitude + biomeCentersGps[i, j].longitude * lonDiff) *100000); // we multiply longitude by lonDiff, because otherwise the seed would be equal for centers where (latitude + longitude) are the same
                Random.InitState(seed);
                biomeCentersGps[i, j].latitude += Random.Range(-0.5f, 0.5f) * latPartSize; // we could also try using gaussian distribution, so that the biome centers are closer to the middle of their respective areas and farther away from each other
                biomeCentersGps[i, j].longitude += Random.Range(-0.5f, 0.5f) * lonPartSize;
            }
        }

    }
    
    void convertBiomeCentersGpsToUnityCoords()
    {
        biomeCenters2D = new Vector2[biomeCentersGps.GetLength(0), biomeCentersGps.GetLength(1)];
        for (int i = 0; i < biomeCentersGps.GetLength(0); i++)
        {
            for (int j = 0; j < biomeCentersGps.GetLength(1); j++)
            {
                Vector3 point = Gps.instance.ConvertGpsToUnityCoords(biomeCentersGps[i, j]);
                biomeCenters2D[i,j] = new Vector2(point.x, point.z);
            }
        }
        
    }


    /// <summary>
    /// Debug tool to visualize positions of biome centers
    /// </summary>
    private void createAndSnapCentersOfBiomesPinsToTerrain()
    {
        float val = Random.value;
        
        for (int i = 0; i < biomeCenters2D.GetLength(0); i++)
        {
            for (int j = 0; j < biomeCenters2D.GetLength(1); j++)
            {
                // put a "pin" where biome center is
                GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pin.transform.position = new Vector3(biomeCenters2D[i, j].x, 400.0f, biomeCenters2D[i, j].y);
    
                // snap pin to terrain
                Vector3 hitPoint = Vector3.zero;
                if (Chunk.snapPointToTerrain(pin.transform.position, ref hitPoint))
                {
                    pin.transform.position = hitPoint;
                }
    
                // float scale = 10.0f;
                float scale = 40.0f;
                pin.transform.localScale = new Vector3(scale, scale, scale);
                pin.transform.parent = chunk.transform;
                Renderer renderer = pin.GetComponent<Renderer>();

                renderer.material.color = UnityEngine.Color.yellow;

            }
        }
        
    }
    
    void colorBiomeCenters()
    {
        biomeCentersColors = new int[biomeCentersGps.GetLength(0), biomeCentersGps.GetLength(1)];
        
        for (int i = 0; i < biomeCentersGps.GetLength(0); i++)
        {
            for (int j = 0; j < biomeCentersGps.GetLength(1); j++)
            {
                float temperatureScale = 100f;
                float humidityScale = 200f;
                float seed = 7345;
                
                temperatureScale *= 4;
                humidityScale *= 4;
                
                float temperature = Mathf.PerlinNoise(seed + (float)biomeCentersGps[i,j].latitude * latitudeFix * temperatureScale, 
                    seed + (float)biomeCentersGps[i,j].longitude / 2 * temperatureScale);
                float humidity = Mathf.PerlinNoise(seed + (float)biomeCentersGps[i,j].latitude * latitudeFix * humidityScale, 
                    seed + (float)biomeCentersGps[i,j].longitude / 2 * humidityScale);
                
                if (temperature < 0.5)
                {
                    if (humidity < 0.5)
                    {
                        biomeCentersColors[i, j] = 0;
                    }
                    else
                    {
                        biomeCentersColors[i, j] = 1;
                    }
                }
                else
                {
                    if (humidity < 0.5)
                    {
                        biomeCentersColors[i, j] = 2;
                    }
                    else
                    {
                        biomeCentersColors[i, j] = 3;
                    }
                }
            }
        }
        
    }
    
    GameObject chooseVegetationToPlant(int biome)
    {
        int treeIndex = biome;
        
        // Some biomes are less dense then other biomes
        float randomPlantPruner = UnityEngine.Random.Range(0f, 1f);
        float randomPlantSelector = UnityEngine.Random.Range(0f, 1f);

        if (biome == DESERT)
        {
            if (randomPlantPruner > 0.43)
            {
                return null;
            }
            
            if (randomPlantSelector <= 0.07)
            {
                treeIndex = 4;
            }
            else if (randomPlantSelector <= 0.7)
            {
                treeIndex = 0;
            }
            else
            {
                treeIndex = 5;
            }
        }
        else if (biome == FOREST){
            
            if (randomPlantSelector <= 0.3)
            {
                treeIndex = 6;
            }
            else
            {
                treeIndex = 1;
            }
        }
        else if (biome == BEACH)
        {
            if (randomPlantPruner > 0.65)
            {
                return null;
            }
            
            if (randomPlantSelector <= 0.5)
            {
                treeIndex = 2;
            }
            else
            {
                treeIndex = 7;
            }
        } else if (biome == PLAINS)
        {
            if (randomPlantPruner > 0.65)
            {
                return null;
            }
            
            treeIndex = biome;
        }
        
        return VegetationPicker.instance.trees[treeIndex];
        
    }


    /// <summary>
    /// Find biome of point using biome centers. And move the point before that using noise functions
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private int findBiomeOfPoint(Vector3 point)
    {
        point = movePointForColoringByBiomes(point);
        
        Vector2 point2D = new Vector2(point.x, point.z);

        int biome = -1;
        float closestBiomeDistance = 10000000;

        for (int i = 0; i < biomeCenters2D.GetLength(0); i++)
        {
            for (int j = 0; j < biomeCenters2D.GetLength(1); j++)
            {
                Vector2 centerOfBiome = biomeCenters2D[i, j];
                float distance = Vector2.Distance(point2D, centerOfBiome);
                if (distance < closestBiomeDistance)
                {
                    closestBiomeDistance = distance;
                    biome = biomeCentersColors[i, j];
                }
            }
        }
        return biome;
    }

    void calculateBBox()
    {
        Coordinates bbMinGps = this.chunk.boundingBox.Minimum;
        Coordinates bbMaxGps = this.chunk.boundingBox.Maximum;
        Vector3 bbMin = Gps.instance.ConvertGpsToUnityCoords(new GpsVector(bbMinGps.Lat, bbMinGps.Lon));
        Vector3 bbMax = Gps.instance.ConvertGpsToUnityCoords(new GpsVector(bbMaxGps.Lat, bbMaxGps.Lon));
        this.bBoxMin = bbMin;
        this.bBoxMax = bbMax;
    }
    
    void calculateCellSize()
    {
        float xSize = bBoxMax.x - bBoxMin.x;
        float zSize = bBoxMax.z - bBoxMin.z;
        float xPartSize = xSize / this.chunk.longitudeCellsCount;
        float zPartSize = zSize / this.chunk.latitudeCellsCount;

        this.cellWidthInX = xPartSize;
        this.cellHeightInZ = zPartSize;
    }
    
}


