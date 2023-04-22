using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

[RequireComponent(typeof(SpatialAnchorManager))]
public class AzureSpatialAnchorsScript1 : MonoBehaviour
{
    #region Fields

    // Used to distinguish short taps and long taps
    private float[] _tappingTimer = { 0, 0 };

    // Main interface to anything Spatial Anchors related
    private SpatialAnchorManager _spatialAnchorManager = null;

    // Used to keep track of all GameObjects that represent a found or created anchor
    private List<GameObject> _foundOrCreatedAnchorGameObjects = new List<GameObject>();

    // Used to keep track of all the created anchor IDs
    private List<String> _createdAnchorIDs = new List<String>();

    public TextMeshProUGUI debugLog;

    #endregion

    #region Async Methods

    // Called when a user is air tapping for a short period of time (less than 1s)
    private async void ShortTap(Vector3 handPosition)
    {
        this.debugLog.text = "ShortTap() was called.";

        // Start a new Azure Spatial Anchors session so we can create and find anchors
        await _spatialAnchorManager.StartSessionAsync();

        // Create a new anchor
        await CreateAnchor(handPosition);
    }

    // Called when a user is air tapping for 2s or more
    private async void LongTap()
    {
        this.debugLog.text = "LongTap() was called.";

        if (_spatialAnchorManager.IsSessionStarted)
        {
            // Stop Azure session
            _spatialAnchorManager.DestroySession();

            // Remove all local instances of spatial anchors
            RemoveAllAnchorGameObjects();

            Debug.Log("ASA - Stopped session and removed all anchor objects.");
        }
        else
        {
            // Start session and search for all anchors previously created
            await _spatialAnchorManager.StartSessionAsync();
            LocateAnchor();
        }
        
    }

    // Creates an Azure Spatial Anchor at the given position rotated towards the user
    private async Task CreateAnchor(Vector3 position)
    {
        // Create Anchor GameObject. We will use ASA to save the position and the rotation of this GameObject.
        // If you can't get a head position vector use the origin instead (for Unity editor)
        if (!InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition))
        {
            headPosition = Vector3.zero;
        }

        // Determine tha rotation of the point reative to the head
        Quaternion orientationTowardsHead = Quaternion.LookRotation(position - headPosition, Vector3.up);

        // Create a new primitive to represent the spatial anchor in the scene
        GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
        anchorGameObject.transform.position = position;
        anchorGameObject.transform.rotation = orientationTowardsHead;
        anchorGameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Add and configure ASA components
        CloudNativeAnchor cloudNativeAnchor = anchorGameObject.AddComponent<CloudNativeAnchor>();

        // Await response from server
        await cloudNativeAnchor.NativeToCloud();

        // Set an expiration date for the newly created anchor
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
        cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(3);

        // Loops until enough environment data has been collected by the device
        while (!_spatialAnchorManager.IsReadyForCreate)
        {
            float createProgress = _spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"ASA - Move your device to capture more environment data: {createProgress:0%}");

            this.debugLog.text = "Move your device to capture more environment data.";
        }

        Debug.Log($"ASA - Saving cloud anchor... ");

        this.debugLog.text = "Saving cloud anchor.";

        try
        {
            // Now that the cloud spatial anchor has been prepared , we can try the actual save here
            await _spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

            // Determine if the anchor was created successfully
            bool saveSucceeded = (cloudSpatialAnchor != null);

            // If the anchor was not created successfully, abort
            if (!saveSucceeded)
            {
                Debug.LogError("ASA - Failed to save, but no exception was thrown.");
                return;
            }

            Debug.Log($"ASA - Saved cloud anchor with ID: {cloudSpatialAnchor.Identifier}");

            // Add the new anchor to our list of anchors
            _foundOrCreatedAnchorGameObjects.Add(anchorGameObject);

            // Add the new anchor's ID to our list of anchor IDs
            _createdAnchorIDs.Add(cloudSpatialAnchor.Identifier);

            // Change the color of the cube representing the anchor in space to green to denote a success
            anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.green;
        }
        catch (Exception exception)
        {
            Debug.Log("ASA - Failed to save anchor: " + exception.ToString());
            Debug.LogException(exception);

            this.debugLog.text = "Failed to save anchor: " + exception.ToString();
        }
    }

    // Deleting cloud anchor attached to the given GameObject and deleting the GameObject from the scene
    private async void DeleteAnchor(GameObject anchorGameObject)
    {
        // Get reference to ASA objects from game objects
        CloudNativeAnchor cloudNativeAnchor = anchorGameObject.GetComponent<CloudNativeAnchor>();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;

        Debug.Log($"ASA - Deleting cloud anchor: {cloudSpatialAnchor.Identifier}");

        // Request deletion of the Azure anchor
        await _spatialAnchorManager.DeleteAnchorAsync(cloudSpatialAnchor);

        // Remove the local reference
        _createdAnchorIDs.Remove(cloudSpatialAnchor.Identifier);
        _foundOrCreatedAnchorGameObjects.Remove(anchorGameObject);
        Destroy(anchorGameObject);

        Debug.Log($"ASA - Cloud anchor deleted!");
    }

    #endregion

    #region Methods

    private void RemoveAllAnchorGameObjects()
    {
        // Destroy all objects in the anchor list
        foreach (var anchorGameObject in _foundOrCreatedAnchorGameObjects)
        {
            Destroy(anchorGameObject);
        }

        // Clear the list
        _foundOrCreatedAnchorGameObjects.Clear();
    }

    // Looking for anchors with ID in our list
    private void LocateAnchor()
    {
        if (_createdAnchorIDs.Count > 0)
        {
            Debug.Log($"ASA - Creating watcher to look for {_createdAnchorIDs.Count} spatial anchors.");

            // Copy anchor IDs
            AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
            anchorLocateCriteria.Identifiers = _createdAnchorIDs.ToArray();

            // Create watcher to look for all stored anchor IDs in Azure
            _spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
            Debug.Log($"ASA - Watcher created!");
        }
    }

    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.Log($"ASA - Anchor recognized as a possible anchor {args.Identifier} {args.Status}");

        // If the spatial anchor is successfully found
        if (args.Status == LocateAnchorStatus.Located)
        {
            // Creating and adjusting GameObjects have to run on the main thread. We are using the UnityDispatcher to make sure this happens.
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                // Read out Cloud anchor values
                CloudSpatialAnchor cloudSpatialAnchor = args.Anchor;

                // Create GameObject to represent spatial anchor
                GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                anchorGameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
                anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.blue;

                // Link the new game object to Azure anchor 
                anchorGameObject.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);
                _foundOrCreatedAnchorGameObjects.Add(anchorGameObject);
            });
        }
    }

    #endregion

    #region Monobehaviour Callbacks

    public void Start()
    {
        // Gets a reference to this object's Spatial Anchor Manager component
        _spatialAnchorManager = GetComponent<SpatialAnchorManager>();

        // Prepare debug logging
        _spatialAnchorManager.LogDebug += (sender, args) => Debug.Log($"ASA - Debug: {args.Message}");
        _spatialAnchorManager.Error += (sender, args) => Debug.LogError($"ASA - Error: {args.ErrorMessage}");

        // Subscribe to AnchorLocated callbacks from SpatialAnchorManager
        _spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
    }

    public void Update()
    {
        // Check for any air taps from either hand (right then left)
        for (int i = 0; i < 2; i++)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode((i == 0) ? XRNode.RightHand : XRNode.LeftHand);

            // Check if the hand is currently air tapping
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool isTapping))
            {
                // If the hand is not air tapping currently
                if (!isTapping)
                {
                    // Stopped tapping before 1s
                    if (0f < _tappingTimer[i] && _tappingTimer[i] < 1f)
                    {
                        // User has been tapping for less than 1 sec. Get hand position and call ShortTap
                        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 handPosition))
                        {
                            ShortTap(handPosition);
                        }
                    }

                    // Reset the air tap timer
                    _tappingTimer[i] = 0;
                }
                else
                {
                    // Increment the air tap timer
                    _tappingTimer[i] += Time.deltaTime;

                    // If the hand has been air tapping for over 2s
                    if (_tappingTimer[i] >= 2f)
                    {
                        // User has been air tapping for at least 2s. Get hand position and call LongTap.
                        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 handPosition))
                        {
                            LongTap();
                        }

                        // Reset the timer, to avoid retriggering if user is still holding tap
                        _tappingTimer[i] = -float.MaxValue;
                    }
                }
            }
        }
    }

    #endregion
}
