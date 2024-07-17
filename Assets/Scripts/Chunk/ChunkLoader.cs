using System.Collections.Generic;
using UnityEngine;
using Priority_Queue;

/// <summary>
/// Load chunks based on their distance from the player. Unload chunks which are too far away.
/// </summary>
/// <author>Michal Mráz</author>
public class ChunkLoader : MonoBehaviour
{
    public static ChunkLoader instance;
    
    float totalTimeElapsed = 0.0f;
    float timeSinceLoadingSomeChunk = 0.0f;

    GameObject worldGameObject;
    Dictionary<long, Chunk> chunks = new Dictionary<long, Chunk>(); // find chunks by their geohash
    // Some info about how to use SimplePriorityQueue can be found here: https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/wiki/Using-the-SimplePriorityQueue
    SimplePriorityQueue<long> toLoad = new SimplePriorityQueue<long>(); // chunks to load, ordered by distance
    float loadingDelay = 1f; // try to load a chunk every 1 seconds
    float toleratedAdditionalDistanceBeforeUnload = 200f;
    
    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        worldGameObject = GameObject.Find("World");
        EventManager.OnWorldCenterSet += WorldCenterSet;
    }
    
    /// <summary>
    /// Event handler for world center set event - unload all chunks
    /// </summary>
    void WorldCenterSet()
    {
        unloadAllChunks();
    }

    // Update is called once per frame
    void Update()
    {
        totalTimeElapsed += Time.unscaledDeltaTime;
        timeSinceLoadingSomeChunk += Time.unscaledDeltaTime;

        // wait for GPS to initialize - could need edit based on GPS init. speed
        if (totalTimeElapsed < 1.5f)
        {
            return;
        }
		
		// Delay loading new chunks so that the game doesn't freeze for longer time
		if (timeSinceLoadingSomeChunk > loadingDelay)
        {
			timeSinceLoadingSomeChunk = 0.0f;
			
			if (!Gps.instance.worldCenterNotYetSet)
			{
                toLoad.Clear();
                findChunksToLoad();
                if (toLoad.Count > 0)
                {
                    loadOneChunk();
                }
                // while (toLoad.Count > 0)
                // {
                //     loadOneChunk();
                // }

                // Find and free chunks that are too far away
                List<long> chunksToFree = findChunksToFree();
                freeChunks(chunksToFree);
            }
		}

    }
    
    void freeChunks(List<long> chunksToFree)
    {
        foreach (long geohash in chunksToFree)
        {
            Chunk chunk = chunks[geohash];
            Destroy(chunk.gameObject);
            Debug.Log("Destroying chunk " + geohash);
            chunks.Remove(geohash);
        }
    }

    /// <summary>
    /// Find chunks that are too far away and could be freed
    /// </summary>
    /// <returns></returns>
    List<long> findChunksToFree()
    {
        List<long> chunksToFree = new List<long>();

        foreach (long currGeohash in chunks.Keys)
        {
            GpsVector playerLocationInGps = Gps.instance.getGps();
            Vector3 playerLocationInUnityCoords = Gps.instance.ConvertGpsToUnityCoords(playerLocationInGps);
            
            GpsVector chunkCenterInGps = getChunkCenterInGps(currGeohash);
            Vector3 chunkCenterInUnityCoords = Gps.instance.ConvertGpsToUnityCoords(chunkCenterInGps);
            
            // measure distance
            double distance = Vector3.Distance(chunkCenterInUnityCoords, playerLocationInUnityCoords);

            if (distance > Settings.chunkGenerationDistance + toleratedAdditionalDistanceBeforeUnload)
            {
                chunksToFree.Add(currGeohash);
            }
        }

        return chunksToFree;

    }

    private void loadOneChunk()
    {
        long geohash = toLoad.Dequeue();    
        loadChunkByItsGeohash(geohash);        
    }
    
    public void unloadAllChunks()
    {
        foreach (long geohash in chunks.Keys)
        {
            Chunk chunk = chunks[geohash];
            Destroy(chunk.gameObject);
        }
        chunks.Clear();
    }

    /// <summary>
    /// Add chunk to queue if the chunk is not in dictionary or if it is in dictionary, but it didnt start loading yet.
    /// </summary>
    /// <param name="geohash"></param>
    /// <param name="distance"></param>
    private void tryAddingGeohashToLoadQueue(long geohash, float distance){
        if (!chunks.ContainsKey(geohash) || (chunks.ContainsKey(geohash) && !chunks[geohash].startedLoading))
        {
            toLoad.Enqueue(geohash, distance);
        }

    }

    /// <summary>
    /// Goes through the nearby chunks and adds those that are within radius to the loading queue. 
    /// </summary>
    private void findChunksToLoad()
    {
        // Get player location in unity coordinates
        GpsVector playerLocationInGps = Gps.instance.getGps();
        Vector3 playerLocationInUnityCoords = Gps.instance.ConvertGpsToUnityCoords(playerLocationInGps);
        
        // Get geohash of the area where player is
        long firstGeohash = GeohashCalculations.getGeohash(playerLocationInGps);

        // Add the geohash to local exploration queue
        List<long> toExplore = new List<long>();
        toExplore.Add(firstGeohash);
        // Keep track of those we already met - and don't add them again to the exploration queue
        HashSet<long> alreadyMetSet = new HashSet<long>();
        alreadyMetSet.Add(firstGeohash);

        while (toExplore.Count > 0)
        {
            // take geohash from the queue
            long currGeohash = toExplore[0];
            toExplore.RemoveAt(0);

            // Measure distance of chunk center from the player
            GpsVector chunkCenterInGps = getChunkCenterInGps(currGeohash);
            Vector3 chunkCenterInUnityCoords = Gps.instance.ConvertGpsToUnityCoords(chunkCenterInGps);
            double distance = Vector3.Distance(chunkCenterInUnityCoords, playerLocationInUnityCoords);

            // skip if the chunk is too far away
            if (distance > Settings.chunkGenerationDistance)
            {
                continue;
            }

            // add the chunk into loading queue
            tryAddingGeohashToLoadQueue(currGeohash, (float)distance);

            // Add it's neighbors to the local exploration queue
            long[] neighbours = GeohashCalculations.getGeohashNeighbours(currGeohash);
            foreach (long neighbour in neighbours)
            {
                if (alreadyMetSet.Contains(neighbour))
                {
                    continue;
                }
                
                toExplore.Add(neighbour);
                alreadyMetSet.Add(neighbour);
            }

        }
        
    }
    
    GpsVector getChunkCenterInGps(long currGeohash)
    {
        return GeohashCalculations.getGeohashCenter(currGeohash);
    }

    private void loadChunkByItsGeohash(long geohash)
    {
        Chunk chunk = getOrCreateChunk(geohash);
        
        chunk.generateContent();
    }
    
    /// <summary>
    /// Create the chunk object and initialize some basic things. Don't generate the chunk content yet.
    /// </summary>
    /// <param name="geohash"></param>
    /// <returns></returns>
    private Chunk getOrCreateChunk(long geohash)
    {
        Chunk chunk;
        
        chunk = tryToGetChunk(geohash);
        if ((Object)chunk != null)
        {
            return chunk;
        }
        
        // Create chunk object and append to world
        GameObject chunkGameObject = new GameObject();
        chunkGameObject.name = "Chunk";
        chunk = chunkGameObject.AddComponent<Chunk>();
        chunkGameObject.transform.parent = worldGameObject.transform;

        // add to library
        chunks.Add(geohash, chunk);

        chunk.geohash = geohash;

        chunk.initializeChunk();

        return chunk;
    }

    /// <summary>
    /// Try to get the chunk if it already exists
    /// </summary>
    /// <param name="geohash"></param>
    /// <returns></returns>
    public Chunk tryToGetChunk(long geohash)
    {
        if (chunks.ContainsKey(geohash))
        {
            return chunks[geohash];
        }
        return null;
    }
}
