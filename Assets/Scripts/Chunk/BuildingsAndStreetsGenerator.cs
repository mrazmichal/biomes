using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;
using NGeoHash;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

/// <summary>
/// Download OSM data of buildings and streets for a given area and generate their representations in the game world.
/// </summary>
/// <author>Michal Mráz</author>
public class BuildingsAndStreetsGenerator : MonoBehaviour
{
    Chunk chunk;
    string osmXmlText;
    float subdivisionSegmentSize = 3;
    const int STREETS_AND_BUILDINGS_LAYER = 6;
    float defaultBuildingHeight = 7f;
    
    static UnityEngine.Color[] pastelColors ={
        new UnityEngine.Color(0.9f, 0.9f, 0.9f), // light gray
        new UnityEngine.Color(0.8f, 0.8f, 0.9f), // light blue
        new UnityEngine.Color(0.9f, 0.9f, 0.8f), // light yellow
        new UnityEngine.Color(0.8f, 0.9f, 0.9f), // light teal
        new UnityEngine.Color(0.9f, 0.8f, 0.9f), // light purple
        new UnityEngine.Color(0.9f, 0.8f, 0.8f), // light pink
        new UnityEngine.Color(0.8f, 0.9f, 0.8f), // light green
        new UnityEngine.Color(0.8f, 0.8f, 0.9f), // light periwinkle
        new UnityEngine.Color(0.9f, 0.9f, 0.7f), // light lemon
        new UnityEngine.Color(0.7f, 0.9f, 0.9f), // light aqua
        new UnityEngine.Color(0.9f, 0.7f, 0.9f), // light lavender
        new UnityEngine.Color(0.9f, 0.7f, 0.7f), // light peach
        new UnityEngine.Color(0.7f, 0.9f, 0.7f), // light mint
        new UnityEngine.Color(0.7f, 0.7f, 0.9f), // light sky blue
        new UnityEngine.Color(0.9f, 0.9f, 0.6f)  // light buttercup
    };

    UnityEngine.Color orange = new UnityEngine.Color(1.0f, 0.5f, 0.0f);
    UnityEngine.Color red = new UnityEngine.Color(1.0f, 0.0f, 0.0f);
    UnityEngine.Color yellow = new UnityEngine.Color(1.0f, 1.0f, 0.0f);
    UnityEngine.Color green = new UnityEngine.Color(0.0f, 1.0f, 0.0f);
    UnityEngine.Color blue = new UnityEngine.Color(0.0f, 0.0f, 1.0f);
    UnityEngine.Color purple = new UnityEngine.Color(0.5f, 0.0f, 0.5f);
    UnityEngine.Color gray = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
    UnityEngine.Color white = new UnityEngine.Color(1.0f, 1.0f, 1.0f);
    UnityEngine.Color pink = new UnityEngine.Color(1.0f, 0.8f, 0.8f);
    UnityEngine.Color cyan = new UnityEngine.Color(0.0f, 1.0f, 1.0f);
    UnityEngine.Color brown = new UnityEngine.Color(0.5f, 0.25f, 0.0f);
    UnityEngine.Color magenta = new UnityEngine.Color(1.0f, 0.0f, 1.0f);
    UnityEngine.Color black = new UnityEngine.Color(0.0f, 0.0f, 0.0f);
    
    public void Init(Chunk chunk)
    {
        this.chunk = chunk;
    }
    
    public IEnumerator generateBuildingsAndStreets()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Restart();
        
        BoundingBox bbox = chunk.boundingBox;
        string bboxString = bbox.Minimum.Lat + "," + bbox.Minimum.Lon + "," + bbox.Maximum.Lat + "," + bbox.Maximum.Lon;
        
        string urlBase = "https://overpass-api.de/api/interpreter?data=";
        // output in xml, timeout 30 seconds, within bounding box, get ways with tag highway, get ways with tag building and get ways with tag building:part. Take these ways and add to them the nodes that create them (recurse down). Return the ways and the nodes and also return centers of the ways (out center).
        string query = "[out:xml][timeout:30][bbox:" + bboxString + "]; (way[\"highway\"]; way[\"building\"]; way[\"building:part\"]; relation[\"building\"]; relation[\"building:part\"];); (._;>;);out center;";
        string url = urlBase + query;

        Debug.Log("OSM url");
        Debug.Log(url);
        
        osmXmlText = "";
        
        yield return Downloading.downloadDataFromUrl(url, (result) => osmXmlText = result);

        Debug.Log("OSM XML text:");
        Debug.Log(osmXmlText);

        stopwatch.Stop();
        Debug.Log("Downloading OSM data took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + chunk.geohash);
        stopwatch.Restart();

        if (string.IsNullOrEmpty(osmXmlText))
        {
            Debug.LogError("Received an empty response to OSM request.");
        }
        else
        {
            CreateGameObjectsFromOsm(osmXmlText);    
        }
        
        stopwatch.Stop();
        Debug.Log("Processing OSM data took " + stopwatch.ElapsedMilliseconds + " ms on chunk " + chunk.geohash);
        
        Encoding encoding = Encoding.UTF8;
        int byteCount = encoding.GetByteCount(osmXmlText);
        Debug.Log("Size of OSM data: ~" + byteCount / 1024.0 + " kBytes");

    }
    
    private void CreateGameObjectsFromOsm(string osmXmlText)
    {
        GameObject buildings = chunk.buildings;
        GameObject streets = chunk.streets;
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(osmXmlText); // in case of exception (XmlException) the document remains empty
        XmlNodeList nodesList = xmlDoc.GetElementsByTagName("node");
        Dictionary<long, GpsVector> nodesDict = new Dictionary<long, GpsVector>();

        extractAndStoreNodeGpsInTheDictionary(nodesList, nodesDict);

        XmlNodeList listOfWays = xmlDoc.GetElementsByTagName("way");
        XmlNodeList listOfRelations = xmlDoc.GetElementsByTagName("relation");
        
        // Find the outer ways of multipolygon buildings relations
        HashSet<long> outerWaysOfMultipolygonBuildings = new HashSet<long>();
        foreach (XmlNode relation in listOfRelations)
        {
            if (isMultipolygonBuilding(relation))
            {
                long id = getIdOfOuterWayOfMultipolygonBuildingRelation(relation);
                outerWaysOfMultipolygonBuildings.Add(id);
            }
        }

        // Iterate over the way elements and create GameObjects to represent them
        foreach (XmlNode way in listOfWays)
        {
            if (isStreet(way))
            {
                processStreetWay(way, nodesDict, streets);
            } else if (isBuilding(way))
            {
                processBuildingWay(way, nodesDict, buildings);
            } else if (outerWaysOfMultipolygonBuildings.Contains(long.Parse(way.Attributes["id"].Value)))
            {
                processBuildingWay(way, nodesDict, buildings);
            }

        }
        
        chunk.informThatMapGenerationDone();
        
    }
    
    long getIdOfOuterWayOfMultipolygonBuildingRelation(XmlNode relation)
    {
        XmlNodeList members = relation.SelectNodes("member");
        foreach (XmlNode member in members)
        {
            XmlAttribute typeAttribute = member.Attributes["type"];
            XmlAttribute refAttribute = member.Attributes["ref"];
            XmlAttribute roleAttribute = member.Attributes["role"];
            if ("way".Equals(typeAttribute.Value) && "outer".Equals(roleAttribute.Value))
            {
                return long.Parse(refAttribute.Value);
            }
        }

        return -1;
    }

    bool isMultipolygonBuilding(XmlNode relation)
    {
        // find if its building or building:part
        XmlNodeList tagNodes = relation.SelectNodes("tag");
        foreach (XmlNode tagNode in tagNodes)
        {
            XmlAttribute kAttribute = tagNode.Attributes["k"];

            if ("building".Equals(kAttribute.Value) || "building:part".Equals(kAttribute.Value))
            {
                return true;
            }
        }

        return false;
    }
    
    void processBuildingWay(XmlNode way, Dictionary<long, GpsVector> nodesDict, GameObject buildings)
    {
        float height = 0.0f;
        if (Settings.useBuildingHeights)
        {
            height = getHeightOfBuildingWay(way);
        }
        if (height < defaultBuildingHeight)
        {
            height = defaultBuildingHeight;
        }
        
        List<GpsVector> gpsPointsOfWay = new List<GpsVector>();
        makeGpsPointsFromWayNodes(way, nodesDict, gpsPointsOfWay);
        
        // find node named center
        Nullable<GpsVector> buildingCenterGps = getBuildingCenterGps(way);
        if (buildingCenterGps == null)
        {
            return;
        }
        
        Vector3 centerPosition = Gps.instance.ConvertGpsToUnityCoords(buildingCenterGps.Value);
        
        // We discard the building if it's center is not in this chunk - let the other chunk handle it
        if (!this.chunk.isGpsPointWithinChunk(buildingCenterGps.Value))
        {
            return;
        }
        
        // Create a list to store the vertex coordinates
        List<Vector3> vertices = new List<Vector3>();
        convertGpsPointsToVertices(gpsPointsOfWay, vertices);

        if (vertices.Count < 3 || !(vertices.First().Equals(vertices.Last()) ) )
        {
            return;
        }

        // remove last vertex (is duplicate of first)
        vertices.RemoveAt(vertices.Count - 1);
        
        GameObject building = new GameObject();
        building.name = "Building";

        createBuildingFromOutlineVertices(vertices, building, height);

        building.transform.parent = buildings.transform;
        
        BuildingData data = building.AddComponent<BuildingData>();
        data.outlineVertices = vertices.ToArray();
        
        building.layer = STREETS_AND_BUILDINGS_LAYER;

        // createBuildingCenterPin(building, centerPosition);
        
    }
    
    void createBuildingCenterPin(GameObject building, Vector3 centerPosition)
    {
        // debugging feature
        // put a "pin" on the center of the building
        // the pin's y will be adjusted later when the building is snapped to terrain
        GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pin.transform.position = new Vector3(centerPosition.x, centerPosition.y + 2, centerPosition.z);
        float scale = 3.0f;
        pin.transform.localScale = new Vector3(scale, scale, scale);
        pin.transform.parent = building.transform;
        Renderer renderer = pin.GetComponent<Renderer>();
        renderer.material.color = UnityEngine.Color.green;
    }

    float getHeightOfBuildingWay(XmlNode way)
    {
        XmlNodeList tagNodes = way.SelectNodes("tag");
        foreach (XmlNode tagNode in tagNodes)
        {
            XmlAttribute kAttribute = tagNode.Attributes["k"];
            XmlAttribute vAttribute = tagNode.Attributes["v"];
            if ("height".Equals(kAttribute.Value))
            {
                string str = vAttribute.Value;
                float height = extractTheFirstFloatFromString(str);
                if (height > 0.0f)
                {
                    return height;
                }
            }
        }
        foreach (XmlNode tagNode in tagNodes)
        {
            XmlAttribute kAttribute = tagNode.Attributes["k"];
            XmlAttribute vAttribute = tagNode.Attributes["v"];
            if ("building:levels".Equals(kAttribute.Value))
            {
                float levels = float.Parse(vAttribute.Value, CultureInfo.InvariantCulture);
                float height = levels * 3f;
                if (height > 0.0f)
                {
                    return height;
                }
            }
        }

        return 0.0f;
    }
    
    float extractTheFirstFloatFromString(string input)
    {
        // Use regex to find the first float number in the string
        Match match = Regex.Match(input, @"-?\d+(\.\d+)?");
        if (match.Success && float.TryParse(match.Value, out float number))
        {
            return number;
        }
        return 0.0f;
    }

    GpsVector? getBuildingCenterGps(XmlNode way)
    {
        XmlNode centerNode = way.SelectSingleNode("center");
        if (centerNode is null)
        {
            return null;
        }
        
        // get the GPS coordinates of center
        double buildingCenterLat = double.Parse(centerNode.Attributes["lat"].Value, CultureInfo.InvariantCulture);
        double buildingCenterLon = double.Parse(centerNode.Attributes["lon"].Value, CultureInfo.InvariantCulture);
        GpsVector centerPositionGps = new GpsVector(buildingCenterLat, buildingCenterLon);

        return centerPositionGps;
    }

    void processStreetWay(XmlNode way, Dictionary<long, GpsVector> nodesDict, GameObject streets)
    {
        List<GpsVector> gpsPointsOfWay = new List<GpsVector>();
        makeGpsPointsFromWayNodes(way, nodesDict, gpsPointsOfWay);

        // Stop if not enough points
        if (gpsPointsOfWay.Count < 2)
        {
            return;
        }

        string highwayTagValue = getStreetWayHighwayTagValue(way);

        List<List<GpsVector>> listOfStreetsAndTheirGpsPoints = new List<List<GpsVector>>();
        clampStreetWayToChunk(gpsPointsOfWay, listOfStreetsAndTheirGpsPoints);

        // Go through each list and create street gameObject from it
        foreach (List<GpsVector> streetGpsPoints in listOfStreetsAndTheirGpsPoints)
        {
            List<Vector3> streetVertices = new List<Vector3>();
            convertGpsPointsToVertices(streetGpsPoints, streetVertices);
            
            List<Vector3> streetVerticesSubdivided = new List<Vector3>();
            subdivideTheStreet(streetVertices, streetVerticesSubdivided);

            createStreetGameObject(streetVerticesSubdivided, highwayTagValue, streets);
            
        }

    }

    string getStreetWayHighwayTagValue(XmlNode way)
    {
        // the complete overview of possible highway values can be found here:
        //https://wiki.openstreetmap.org/wiki/Key:highway

        XmlNodeList tagNodes = way.SelectNodes("tag");

        foreach (XmlNode tagNode in tagNodes)
        {
            XmlAttribute kAttribute = tagNode.Attributes["k"];
            XmlAttribute vAttribute = tagNode.Attributes["v"];

            if ("highway".Equals(kAttribute.Value))
            {
                // is for example "motorway"
                return vAttribute.Value;
            }
        }

        return "";
    }

    /// <summary>
    /// From given vertices generate street gameObject and make streets it's parent
    /// </summary>
    /// <param name="streetVertices"></param>
    /// <param name="highwayTagValue"></param>
    /// <param name="streets"></param>
    void createStreetGameObject(List<Vector3> streetVertices, string highwayTagValue, GameObject streets)
    {
        
        GameObject street = new GameObject();
        street.name = "Street";
        street.transform.parent = streets.transform;

        street.transform.rotation = Quaternion.Euler(90, 0, 0);

        UnityEngine.Color col = orange; // default colour
        
        float wideStreetWidth =7.5f;
        float narrowStreetWidth = 3f;
        
        float streetWidth = narrowStreetWidth;

        // We assign different colors and widths to different kinds if roads. Special kinds, paths etc. could be distinguished in the future
        // the complete overview of highway tag can be found here:
        //https://wiki.openstreetmap.org/wiki/Key:highway
        switch (highwayTagValue)
        {
            case "motorway":
                streetWidth = wideStreetWidth;
                break;
            case "trunk":
                streetWidth = wideStreetWidth;
                break;
            case "primary":
                streetWidth = wideStreetWidth;
                break;
            case "secondary":
                streetWidth = wideStreetWidth;
                break;
            case "tertiary":
                streetWidth = wideStreetWidth;
                break;
            case "unclassified":
                streetWidth = wideStreetWidth;
                break;
            case "residential":
                streetWidth = wideStreetWidth;
                break;
            case "service":
                streetWidth = wideStreetWidth;
                break;
            case "motorway_link":
                streetWidth = wideStreetWidth;
                break;
            case "trunk_link":
                streetWidth = wideStreetWidth;
                break;
            case "primary_link":
                streetWidth = wideStreetWidth;
                break;
            case "secondary_link":
                streetWidth = wideStreetWidth;
                break;
            case "tertiary_link":
                streetWidth = wideStreetWidth;
                break;
            case "living_street":
                streetWidth = wideStreetWidth;
                break;
            case "pedestrian":
                streetWidth = wideStreetWidth;
                break;
            case "track":
                streetWidth = wideStreetWidth;
                break;
            case "road":
                streetWidth = wideStreetWidth;
                break;
            default:
                break;
        }

        // Tool to visualize different kinds of roads
        // We assign different colours to different kinds if roads. Special kinds, paths etc. are just coloured black for now.
        // the complete overview of highway can be found here:
        //https://wiki.openstreetmap.org/wiki/Key:highway
        //switch (vAttribute.Value)
        //{
        //    case "motorway":
        //        col = red;
        //        break;
        //    case "trunk":
        //        col = orange;
        //        break;
        //    case "primary":
        //        col = yellow;
        //        break;
        //    case "secondary":
        //        col = green;
        //        break;
        //    case "tertiary":
        //        col = blue;
        //        break;
        //    case "unclassified":
        //        col = purple;
        //        break;
        //    case "residential":
        //        col = gray;
        //        break;
        //    case "service":
        //        col = white;
        //        break;
        //    case "motorway_link":
        //        col = red;
        //        break;
        //    case "trunk_link":
        //        col = orange;
        //        break;
        //    case "primary_link":
        //        col = yellow;
        //        break;
        //    case "secondary_link":
        //        col = green;
        //        break;
        //    case "tertiary_link":
        //        col = blue;
        //        break;
        //    case "living_street":
        //        col = pink;
        //        break;
        //    case "pedestrian":
        //        col = cyan;
        //        break;
        //    case "track":
        //        col = brown;
        //        break;
        //    case "road":
        //        col = magenta;
        //        break;
        //    default:
        //        col = black;
        //        break;
        //}

        LineRenderer lineRenderer = street.AddComponent<LineRenderer>();
        lineRenderer.positionCount = streetVertices.Count;
        lineRenderer.SetPositions(streetVertices.ToArray());
        lineRenderer.material.color = col;

        Material material = MaterialPicker.instance.highwayMaterial;
        lineRenderer.material = material;

        lineRenderer.useWorldSpace = true;
        lineRenderer.generateLightingData = true;

        lineRenderer.startWidth = streetWidth;
        lineRenderer.endWidth = streetWidth;

        lineRenderer.alignment = LineAlignment.TransformZ;

        StreetData data = street.AddComponent<StreetData>();
        data.vertices = streetVertices.ToArray();
        
        // add to streets layer
        street.layer = STREETS_AND_BUILDINGS_LAYER;
        
    }

    /// <summary>
    /// Subdivide the street into smaller segments. It is done so the street intersects less with terrain. 
    /// </summary>
    /// <param name="streetVertices"></param>
    /// <param name="streetVerticesSubdivided"></param>
    void subdivideTheStreet(List<Vector3> streetVertices, List<Vector3> streetVerticesSubdivided)
    {
        Vector3 oldVertex = Vector3.zero;
        int k = 0;
        foreach (Vector3 currVertex in streetVertices)
        {
            // special case for the first vertex - just add it
            if (k++ == 0)
            {
                streetVerticesSubdivided.Add(currVertex);
                oldVertex = currVertex;
                continue;
            }
            
            // create new vertices while distance of oldVertex and currVertex is too big
            while(Vector3.Distance(oldVertex, currVertex) > subdivisionSegmentSize)
            {
                Vector3 direction = Vector3.Normalize(currVertex - oldVertex);
                Vector3 newVertex = oldVertex + direction * subdivisionSegmentSize;
                streetVerticesSubdivided.Add(newVertex);
                oldVertex = newVertex;
            }
            streetVerticesSubdivided.Add(currVertex);

            oldVertex = currVertex;
        }
    }

    void convertGpsPointsToVertices(List<GpsVector> gpsPoints, List<Vector3> vertices)
    {
        foreach (GpsVector gps in gpsPoints)
        {
            vertices.Add(Gps.instance.ConvertGpsToUnityCoords(gps));
        }
    }

    void makeGpsPointsFromWayNodes(XmlNode way, Dictionary<long, GpsVector> nodesDict, List<GpsVector> gpsPointsOfWay)
    {
        XmlNodeList nodesOfWay = way.SelectNodes("nd");
        
        for (int i = 0; i < nodesOfWay.Count; i++)
        {
            long id = long.Parse(nodesOfWay[i].Attributes["ref"].Value);
            if (nodesDict.ContainsKey(id))
            {
                GpsVector pos = nodesDict[id];
                gpsPointsOfWay.Add(pos);
            }                    
        }
    }

    void extractAndStoreNodeGpsInTheDictionary(XmlNodeList nodesList, Dictionary<long, GpsVector> nodesDict)
    {
        // Extract the gps of each node and store it in the dictionary
        foreach (XmlNode node in nodesList)
        {
            long id = long.Parse(node.Attributes["id"].Value);
            double lat = double.Parse(node.Attributes["lat"].Value, CultureInfo.InvariantCulture);
            double lon = double.Parse(node.Attributes["lon"].Value, CultureInfo.InvariantCulture);
            GpsVector nodeGps = new GpsVector(lat, lon);
            // Vector3 position = Gps.instance.ConvertGpsToUnityCoords(nodeGps);
            nodesDict.Add(id, nodeGps);
        }
    }

    /// <summary>
    /// Remove parts of the street that are not on this chunk
    /// </summary>
    /// <param name="gpsPointsOfWay"></param>
    /// <param name="listOfStreetsAndTheirGpsPoints"></param>
    void clampStreetWayToChunk(List<GpsVector> gpsPointsOfWay, List<List<GpsVector>> listOfStreetsAndTheirGpsPoints)
    {
        // Get rid of parts of way that are not within the chunk
        // If necessary, create more than one street (if the way for example goes out of chunk borders and then comes back, we divide it into more parts)
        
        GpsVector oldGps = new GpsVector();
        bool oldIsWithin = false;
        bool firstIteration = true;
        
        foreach (GpsVector currGps in gpsPointsOfWay)
        {
            bool currentIsWithin = this.chunk.isGpsPointWithinChunk(currGps);
            
            if (firstIteration)
            {
                // if the first point is within the chunk, create a new street and add the point to it
                if (currentIsWithin)
                {
                    listOfStreetsAndTheirGpsPoints.Add(new List<GpsVector>());
                    listOfStreetsAndTheirGpsPoints.Last().Add(currGps);
                }
                firstIteration = false;
                oldGps = currGps;
                oldIsWithin = currentIsWithin;
                continue;
            }
            
            if (!oldIsWithin && !currentIsWithin)
            {
                
                GpsVector[] results = new GpsVector[2];
                
                // We have an old line segment defined by oldGps and currGps. 
                // We want to find a new (possibly shorter) line segment, where it's both endpoints are within the chunk.
                if (getLineSegmentWithinChunk(oldGps, currGps, results))
                {
                    // create new street
                    listOfStreetsAndTheirGpsPoints.Add(new List<GpsVector>());
                
                    listOfStreetsAndTheirGpsPoints.Last().Add(results[0]);
                    listOfStreetsAndTheirGpsPoints.Last().Add(results[1]);
                }
                
            } else if (!oldIsWithin && currentIsWithin)
            {
                // create new street
                listOfStreetsAndTheirGpsPoints.Add(new List<GpsVector>());
                
                // create new point on chunk border
                GpsVector createdGps = createGpsPointOnIntersectionWithChunkBorder(oldGps, currGps);
                listOfStreetsAndTheirGpsPoints.Last().Add(createdGps);
                listOfStreetsAndTheirGpsPoints.Last().Add(currGps);
                
            } else if (oldIsWithin && !currentIsWithin)
            {
                // end street
                // add created point to current street
                GpsVector createdGps = new GpsVector();
                createdGps = createGpsPointOnIntersectionWithChunkBorder(oldGps, currGps);
                listOfStreetsAndTheirGpsPoints.Last().Add(createdGps);
                
            } else if (oldIsWithin && currentIsWithin)
            {
                // add curr to curr street
                listOfStreetsAndTheirGpsPoints.Last().Add(currGps);
                
            }
            
            // important!
            oldGps = currGps;
            oldIsWithin = currentIsWithin;

        }

    }
    
    /// <summary>
    /// We have an old line segment defined by oldGps and currGps. 
    /// We want to find a new (possibly shorter) line segment, where it's both endpoints are within the chunk.
    /// </summary>
    /// <param name="oldGps"></param>
    /// <param name="currGps"></param>
    /// <param name="results"></param>
    /// <returns></returns>
    bool getLineSegmentWithinChunk(GpsVector oldGps, GpsVector currGps, GpsVector[] results)
    {
        GpsVector[] candidates = new GpsVector[4];
        getPossibleIntersectionsWithChunkBorders(oldGps, currGps, candidates);
        
        return chooseTwoCorrectLineSegmentIntersectionsWithChunkBorders(oldGps, currGps, candidates, results);
        
    }
    
    bool chooseTwoCorrectLineSegmentIntersectionsWithChunkBorders(GpsVector oldGps, GpsVector currGps, GpsVector[] candidates, GpsVector[] results)
    {
        bool success = false;
        
        GpsVector lineBoundingBoxMin = new GpsVector(Math.Min(oldGps.latitude, currGps.latitude), Math.Min(oldGps.longitude, currGps.longitude));
        GpsVector lineBoundingBoxMax = new GpsVector(Math.Max(oldGps.latitude, currGps.latitude), Math.Max(oldGps.longitude, currGps.longitude));
        GpsVector chunkBBoxMin = new GpsVector(this.chunk.boundingBox.Minimum.Lat, this.chunk.boundingBox.Minimum.Lon);
        GpsVector chunkBBoxMax = new GpsVector(this.chunk.boundingBox.Maximum.Lat, this.chunk.boundingBox.Maximum.Lon);

        int counter = 0;
        foreach (GpsVector point in candidates)
        {
            bool valid = isGpsPointWithinBoundingBox(point, chunkBBoxMin, chunkBBoxMax) && isGpsPointWithinBoundingBox(point, lineBoundingBoxMin, lineBoundingBoxMax);
            if (valid)
            { 
                counter++;
                
                results[counter-1] = point;
                if (counter == 2)
                {
                    success = true;
                    break;
                }
            }
        }

        return success;

    }

    GpsVector createGpsPointOnIntersectionWithChunkBorder(GpsVector oldGps, GpsVector currGps)
    {
        GpsVector[] candidates = new GpsVector[4];
        getPossibleIntersectionsWithChunkBorders(oldGps, currGps, candidates);

        return chooseOneCorrectIntersectionWithChunkBorderCandidate(oldGps, currGps, candidates);
        
    }
    
    GpsVector chooseOneCorrectIntersectionWithChunkBorderCandidate(GpsVector oldGps, GpsVector currGps, GpsVector[] candidates)
    {
        GpsVector lineBoundingBoxMin = new GpsVector(Math.Min(oldGps.latitude, currGps.latitude), Math.Min(oldGps.longitude, currGps.longitude));
        GpsVector lineBoundingBoxMax = new GpsVector(Math.Max(oldGps.latitude, currGps.latitude), Math.Max(oldGps.longitude, currGps.longitude));
        GpsVector chunkBBoxMin = new GpsVector(this.chunk.boundingBox.Minimum.Lat, this.chunk.boundingBox.Minimum.Lon);
        GpsVector chunkBBoxMax = new GpsVector(this.chunk.boundingBox.Maximum.Lat, this.chunk.boundingBox.Maximum.Lon);

        foreach (GpsVector point in candidates)
        {
            bool valid = isGpsPointWithinBoundingBox(point, chunkBBoxMin, chunkBBoxMax) && isGpsPointWithinBoundingBox(point, lineBoundingBoxMin, lineBoundingBoxMax);
            if (valid)
            {
                return point;
            }
        }

        // At this point one of the candidates should have been chosen! 
        Debug.LogError( "Failed to createGpsPointOnIntersectionWithChunkBorder.", this.chunk);
        return candidates[0];
    }

    /// <summary>
    /// Get points where the line between oldGps and currGps intersects with the chunk borders
    /// </summary>
    /// <param name="oldGps"></param>
    /// <param name="currGps"></param>
    /// <param name="gpsVectors"></param>
    void getPossibleIntersectionsWithChunkBorders(GpsVector oldGps, GpsVector currGps, GpsVector[] gpsVectors)
    {
        double bottomEdgeLat = this.chunk.boundingBox.Minimum.Lat;
        double leftEdgeLon = this.chunk.boundingBox.Minimum.Lon;
        double topEdgeLat = this.chunk.boundingBox.Maximum.Lat;
        double rightEdgeLon = this.chunk.boundingBox.Maximum.Lon;
        
        double t;
        t = inverseLerp(oldGps.latitude, currGps.latitude, topEdgeLat);
        gpsVectors[0] = new GpsVector(topEdgeLat, lerp(oldGps.longitude, currGps.longitude, t));
        
        t = inverseLerp(oldGps.longitude, currGps.longitude, rightEdgeLon);
        gpsVectors[1] = new GpsVector(lerp(oldGps.latitude, currGps.latitude, t), rightEdgeLon);
        
        t = inverseLerp(oldGps.latitude, currGps.latitude, bottomEdgeLat);
        gpsVectors[2] = new GpsVector(bottomEdgeLat, lerp(oldGps.longitude, currGps.longitude, t));
        
        t = inverseLerp(oldGps.longitude, currGps.longitude, leftEdgeLon);
        gpsVectors[3] = new GpsVector(lerp(oldGps.latitude, currGps.latitude, t), leftEdgeLon);
        
    }
    
    
    bool isGpsPointWithinBoundingBox(GpsVector point, GpsVector bBoxMin, GpsVector bBoxMax)
    {
        if (point.latitude <= bBoxMax.latitude + Gps.inaccuracyTolerance.latitude
            && point.latitude >= bBoxMin.latitude - Gps.inaccuracyTolerance.latitude
            && point.longitude <= bBoxMax.longitude + Gps.inaccuracyTolerance.longitude
            && point.longitude >= bBoxMin.longitude - Gps.inaccuracyTolerance.longitude)
        {
            return true;
        }
        
        return false;
    }

    double lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }
    double inverseLerp(double a, double b, double value)
    {
        if (a != b)
        {
            return ((value - a) / (b - a));
        }
        
        return 0;
            
    }

    private static void renderBuildingOutlineFromOutlineVertices(List<Vector3> vertices, GameObject building)
    {
        LineRenderer lineRenderer = building.AddComponent<LineRenderer>();
        lineRenderer.positionCount = vertices.Count;
        lineRenderer.SetPositions(vertices.ToArray());
        lineRenderer.material.color = UnityEngine.Color.green;
    }

    private static void createBuildingFromOutlineVertices(List<Vector3> vertices, GameObject building, float height)
    {
        // create mesh vertices
        List<Vector3> verticesInAPlane = vertices.Select(v => new Vector3(v.x, 0, v.z)).ToList(); // remove the y coord if there is any
        Vector3[] meshVertices = verticesInAPlane.ToArray();

        if (meshVertices.Length == 0)
        {
            return;
        }

        // create mesh triangles
        Double2[] verticesForTriangulation = verticesInAPlane.Select(v => new Double2(v.x, v.z)).ToArray();
        List<int> triangles = new List<int>();
        Triangulation.TriangulatePolygon(verticesForTriangulation, triangles);  
        int[] meshTriangles = triangles.ToArray();
        createBuildingFromFloorMeshData(meshVertices, meshTriangles, building, height);
        
    }

    private static void createBuildingFromFloorMeshData(Vector3[] meshVertices, int[] meshTriangles, GameObject building, float height)
    {
        GameObject walls = createWalls(meshVertices, meshTriangles, height);
        GameObject ceiling = createCeiling(meshVertices, meshTriangles, height);

        // Append the objects to building
        walls.transform.parent = building.transform;
        ceiling.transform.parent = building.transform;

        // Set buildings colors
        //UnityEngine.Color col = new UnityEngine.Color();
        //System.Random random = new System.Random();
        //col = pastelColors[random.Next(0, pastelColors.Count())];
        //walls.GetComponent<MeshRenderer>().material.color = col;
        //ceiling.GetComponent<MeshRenderer>().material.color = col;

        Material material = MaterialPicker.instance.buildingMaterial;
        walls.GetComponent<MeshRenderer>().material = material;
        ceiling.GetComponent<MeshRenderer>().material = material;
    }

    private static GameObject createWalls(Vector3[] meshVertices, int[] meshTriangles, float height)
    {
        GameObject gameObject = new GameObject();
        gameObject.name = "Walls";

        // Create a mesh object and set its vertices, triangles
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Create walls by extrusion of outline
        // Extrusion amount (in world units)
        float extrusionAmount = height;
        Vector3 extrusionDirection = Vector3.up;
        Vector3[] verticesExtruded = null;
        int[] trianglesExtruded = null;

        // Fix, because the algorithm assumes clockwise vertices
        if (!Triangulation.IsPolygonClockwise(meshVertices.Select(v => new Double2(v.x, v.z)).ToList()))
        {
            meshVertices = meshVertices.Reverse().ToArray();
        }

        Extrusion.ExtrudeMeshWallsOnly(meshVertices, meshTriangles, ref verticesExtruded, ref trianglesExtruded, extrusionAmount, extrusionDirection);
        
        mesh.vertices = verticesExtruded;
        mesh.triangles = trianglesExtruded;

        mesh.RecalculateNormals();

        return gameObject;
    }

    private static GameObject createCeiling(Vector3[] meshVertices, int[] meshTriangles, float height)
    {
        GameObject gameObject = new GameObject();
        gameObject.name = "Ceiling";

        // Create a mesh object and set its vertices, triangles
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        
        // Create ceiling by translation
        // Extrusion amount (in world units)
        float extrusionAmount = height;
        Vector3 extrusionDirection = Vector3.up;

        Vector3[] verticesExtruded = new Vector3[meshVertices.Length];
        int[] trianglesExtruded = new int[meshTriangles.Length];
        meshVertices.CopyTo(verticesExtruded, 0);
        for (int i = 0; i < verticesExtruded.Length; i++)
        {
            verticesExtruded[i] += extrusionDirection * extrusionAmount;
        }
        meshTriangles.CopyTo(trianglesExtruded, 0);

        mesh.vertices = verticesExtruded;
        mesh.triangles = trianglesExtruded;
        
        mesh.RecalculateNormals();

        return gameObject;
    }

    private static bool isStreet(XmlNode way)
    {
        bool ret = false;

        foreach (XmlNode tagNode in way.ChildNodes)
        {
            if (tagNode.Name == "tag")
            {
                XmlAttribute kAttribute = tagNode.Attributes["k"];

                if (kAttribute.Value == "highway")
                {
                    ret = true;
                }
            }
        }

        return ret;
    }

    private static bool isBuilding(XmlNode way)
    {
        bool ret = false;

        foreach (XmlNode tagNode in way.ChildNodes)
        {
            if (tagNode.Name == "tag")
            {
                XmlAttribute kAttribute = tagNode.Attributes["k"];

                if (kAttribute.Value == "building" || kAttribute.Value == "building:part")
                {
                    ret = true;
                }
            }
        }

        return ret;
    }

}
