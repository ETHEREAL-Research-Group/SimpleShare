using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TMPro;

using UnityEngine;

using Photon.Pun;

public class ASAScript : MonoBehaviour
{
    #region Fields

    // Displays debug information in the scene
    public TextMeshProUGUI debugLog;

    // Object used to represent spatial anchor position
    public GameObject callibrationObject;

    // Used by ASA
    public SpatialAnchorManager spatialAnchorManager;

    // A semi-transparent material
    public Material lowVisMaterial;

    // A list of game objects representing spatial anchors
    //public List<GameObject> foundOrCreatedAnchorGameObjects = new List<GameObject>();

    // A list of spatial anchor IDs
    public List<String> createdAnchorIDs = new List<String>();

    // Represents the saved position of the spatial anchor in game space
    public GameObject anchorGameObject;

    public GameObject anchorObjectPrefab;

    public GameObject sharedObjectPrefab;

    #endregion

    #region MonoBehaviour Callbacks

    void Start()
    {
        // Subscribe to callbacks about locating spatial anchors
        spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

        debugLog.text += "Subscribed to ASA locating.\n";
    }

    #endregion

    #region Methods

    // Saves callibration object's current position/rotation as a spatial anchor
    public async void Callibrate()
    {
        // Button only works for master client
        if (PhotonNetwork.IsMasterClient)
        {
            // Get a reference to the callibration object
            callibrationObject = GameObject.FindGameObjectWithTag("CallibrationObject");

            // Only start a new ASA session if there is no current session
            if (!spatialAnchorManager.IsSessionStarted)
            {
                // Disable further movement of callibration object
                ObjectManipulator manipulatorScript = callibrationObject.GetComponent<ObjectManipulator>();
                manipulatorScript.enabled = false;

                NearInteractionGrabbable grabbableScript = callibrationObject.GetComponent<NearInteractionGrabbable>();
                grabbableScript.enabled = false;

                // Change appearance of the callibrationObject to denote the change
                callibrationObject.GetComponent<MeshRenderer>().material = lowVisMaterial;
                callibrationObject.transform.localScale = Vector3.one * 0.05f;

                debugLog.text += "Anchor position saved. ASA session starting...\n";

                // Start a new ASA session
                await spatialAnchorManager.StartSessionAsync();

                // Create a new spatial anchor at the callibration object's current position
                await CreateAnchor();

                // Get current position of the callibration object
                Vector3 pos = callibrationObject.transform.position;
                debugLog.text += "Anchor created at position: (" + pos.x + ", " + pos.y + ", " + pos.z + ")\n";
            }
        }
    }

    // Creates a spatial anchor in ASA
    private async Task CreateAnchor()
    {
        // Add and configure ASA components
        CloudNativeAnchor cloudNativeAnchor = callibrationObject.AddComponent<CloudNativeAnchor>();
        await cloudNativeAnchor.NativeToCloud();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
        cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(3);

        // Collect environment data (if necessary)
        if (!spatialAnchorManager.IsReadyForCreate)
        {
            debugLog.text += "Move your device to capture more environment data...\n";

            // Track and update collection progress (if necessary)
            float createProgress = spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            while (!spatialAnchorManager.IsReadyForCreate)
            {
                float checkProgress = spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;

                if (checkProgress > createProgress + 1.0f)
                {
                    createProgress = checkProgress;
                    debugLog.text += "Progress: " + createProgress + "%\n";
                }
            }

            debugLog.text += "Environmental data capture complete.\n";
        }

        // Create the ASA
        try
        {
            await spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

            // Check if the spatial anchor was saved successfully
            bool saveSucceeded = (cloudSpatialAnchor != null);
            if (!saveSucceeded)
            {
                debugLog.text += "Failed to save spatial anchor, but no exception thrown.\n";
                return;
            }

            debugLog.text += "Saved spatial anchor with ID: " + cloudSpatialAnchor.Identifier + "\n";

            // Keep track of the identifier for the new spatial anchor
            createdAnchorIDs.Add(cloudSpatialAnchor.Identifier);
        }
        catch (Exception exception)
        {
            debugLog.text += "Failed to save spatial anchor " + exception.ToString() + "\n";
        }
    }

    // Starts an ASA session for the secondary client
    public async void Locate(string id)
    {
        if (spatialAnchorManager.IsSessionStarted)
        {
            debugLog.text += "Ending current ASA session...\n";
            spatialAnchorManager.DestroySession();
        }
        
        debugLog.text += "Starting a new ASA session...\n";

        await spatialAnchorManager.StartSessionAsync();

        LocateAnchor(id);
    }

    // Finds the spatial anchor with id for the secondary client
    private void LocateAnchor(string id)
    {
        if (id != null)
        {
            debugLog.text += "Finding anchor with ID: " + id + "...\n";

            // TODO unnecessary
            List<string> ids = new List<string>();
            ids.Add(id);

            // Create a watcher for the spatial anchor with id
            AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
            anchorLocateCriteria.Identifiers = ids.ToArray();
            //debugLog.text += "Identifiers: " + anchorLocateCriteria.Identifiers.ToString() + "\n";
            CloudSpatialAnchorWatcher watcher = spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);

            if (watcher != null)
            {
                debugLog.text += "Watcher created succesfully. Watching..\n";
            }
        }
        else
        {
            debugLog.text += "ID is null.\n";
        }
    }

    // Callback fired if the spatial anchor manager discovers a spatial anchor in the nearby vicinity
    // (should only be called by the secondary client)
    public void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        debugLog.text += "SpatialAnchorManager_AnchorLocated() was called with ";
        debugLog.text += "args.Status: " + args.Status + "\n";

        if (args.Status == LocateAnchorStatus.Located)
        {
            // Create a new game object where the spatial anchor was found (force Unity to use main thread)
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                // Read out spatial anchor data
                CloudSpatialAnchor cloudSpatialAnchor = args.Anchor;

                // Create a new game object to represent spatial anchor
                //anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                anchorGameObject = Instantiate(anchorObjectPrefab, Vector3.zero, Quaternion.identity);
                //anchorGameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                //anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
                //anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.blue;

                debugLog.text += "Spatial anchor game object created.\n";

                // Link to spatial anchor
                anchorGameObject.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);
            });

            // Update the position and rotation of the shared game object for the secondary client to the
            // spatial anchor's position
            GameObject sharedObject = GameObject.FindGameObjectWithTag("SharedObject");

            if (sharedObject != null)
            {
                debugLog.text += "sharedObject was found.\n";
            }
            else
            {
                debugLog.text += "sharedObject was not found.\n";
            }

            SharedObjectManager sharedObjectManager = sharedObject.GetComponent<SharedObjectManager>();
            sharedObjectManager.setAnchorTransform(anchorGameObject.transform.position, anchorGameObject.transform.rotation);
            debugLog.text += "Secondary client created a spatial anchor at positon: ("
                          + anchorGameObject.transform.position.x
                          + ", "
                          + anchorGameObject.transform.position.y
                          + ", "
                          + anchorGameObject.transform.position.z
                          + ")";
        }
    }

    #endregion 
}
