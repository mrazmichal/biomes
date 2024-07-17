using System;
using System.IO;
using UnityEngine;

/// <author>Michal Mráz</author>
public class WriteFramerateToFile : MonoBehaviour
{
    string fPath;
    string content;

    bool enabled = false;
    
    void Awake()
    {
        fPath = Path.Combine(Application.persistentDataPath + "/framerate.txt");
        content = "";
        File.WriteAllText(Application.persistentDataPath + "/framerate.txt", "");

    }

    void Start()
    {
        EventManager.OnGpsSimulationEnabled += GpsSimulationEnabled;
    }

    // Update is called once per frame
    void Update()
    {
        if (!enabled)
        {
            return;
        }
        
        string line =  Time.unscaledDeltaTime + ",\n";
        // write single line
        File.AppendAllText(fPath, line);        
    }
    
    void GpsSimulationEnabled()
    {
        enabled = true;
    }
    
    
}
