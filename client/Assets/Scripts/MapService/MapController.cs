using System;
using System.IO;
using System.Text.RegularExpressions;
using Google.Maps;
using Google.Maps.Coord;
using Google.Maps.Event;
using Google.Maps.Examples.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// This example demonstrates a basic usage of the Maps SDK for Unity.
/// </summary>
/// <remarks>
/// By default, this script loads the Statue of Liberty. If a new lat/lng is set in the Unity
/// inspector before pressing start, that location will be loaded instead.
/// </remarks>
[RequireComponent(typeof(MapsService))]
public class MapController : MonoBehaviour
{
    [Tooltip("LatLng to load (must be set before hitting play).")]
    public LatLng latLng = new LatLng(30.65833, 104.06586);

    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(8000, 0, 8000));

    public MapLabeller RoadLabeller;

    public MapsService MapsService;

    public bool loaded = false;

    /// <summary>
    /// Use <see cref="MapsService"/> to load geometry.
    /// </summary>
    public void LoadMap()
    {
        loaded = false;
        // Get required MapsService component on this GameObject.
        MapsService mapsService = GetComponent<MapsService>();

        // Set real-world location to load.
        mapsService.InitFloatingOrigin(latLng);

        // Register a listener to be notified when the map is loaded.
        mapsService.Events.MapEvents.Loaded.AddListener(OnLoaded);

        // Load map with default options.
        mapsService.LoadMap(bounds, ExampleDefaults.DefaultGameObjectOptions);
        
        Debug.Log("load map at "+latLng.ToString()+"bound"+bounds.size);
    }

    /// <summary>
    /// Trigger Road Label
    /// </summary>
    public void TriggerRoadLabel(Toggle toggle)
    {
        RoadLabeller.enabled = toggle.isOn;
    }

    /// <summary>
    /// Example of OnLoaded event listener.
    /// </summary>
    /// <remarks>
    /// The communication between the game and the MapsSDK is done through APIs and event listeners.
    /// </remarks>
    public void OnLoaded(MapLoadedArgs args)
    {
        // The Map is loaded - you can start/resume gameplay from that point.
        // The new geometry is added under the GameObject that has MapsService as a component.
        loaded = true;
    }
}