using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Downloading data from url
/// </summary>
/// <author>Michal Mráz</author>
public static class Downloading
{
    public static IEnumerator downloadDataFromUrl(string url, Action<string> callback)
    {
        int maxRetries = 10;
        int triesCounter = 0;
        bool success = false;

        // we try sending the request multiple times if there is a network error or http error
        while (!success && triesCounter < maxRetries)
        {
            triesCounter++;
        
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest(); // wait until the request is finished

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log("terrain request error");
                Debug.Log(request.error);

                // Wait for 10 seconds before trying again
                yield return new WaitForSeconds(10);
                // try sending the request again
                continue;
            }

            success = true;

            // accurate measurement of the downloaded data size:
            // int byteCount = request.downloadHandler.data.Length;
            // Debug.Log("Downloaded " + byteCount + " bytes");
            
            string result = request.downloadHandler.text;
            callback(result);
        }
    }
    
}
