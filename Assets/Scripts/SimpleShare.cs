using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Photon.Pun;
using Photon.Realtime;

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Input;

using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.Azure.SpatialAnchors;
using UnityEngine.XR.ARFoundation;
using TMPro;
using System.Threading.Tasks;

[RequireComponent(typeof(ARSessionOrigin))]
[RequireComponent(typeof(ARAnchorManager))]
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(SpatialAnchorManager))]
public class SimpleShare : MonoBehaviourPunCallbacks, IMixedRealitySpeechHandler
{
    #region Fields

    private Transform anchorTransform;

    private Transform deltaTransform;

    private List<Vector3> axes;

    public SpatialAnchorManager spatialAnchorManager;

    public List<string> createdAnchorIDs = new List<String>();

    public List<GameObject> anchorGameObjects = new List<GameObject>();

    public GameObject debugObjects;

    public TextMeshProUGUI debugLog;

    public bool debugIsOn;

    #endregion

    #region Unity Callbacks

    public void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void Start()
    {
        debugLog.text += "Start() was called.\n";

        // Toggle debug mode off initially
        debugIsOn = false;

        // Subscribe to spatial anchor location callbacks
        spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

        // Subscribe to speech input callbacks
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);
    }

    public void Update()
    {
        // TODO Update this client's shared object positions

        // TODO Update this client's shared object states?
    }

    #endregion

    #region SpeechHandler Callbacks

    public void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        switch (eventData.Command.Keyword.ToLower())
        {
            case "toggle debug mode":
                if (debugIsOn)
                {
                    debugObjects.SetActive(false);
                    debugIsOn = false;
                }
                else
                {
                    debugObjects.SetActive(true);
                    debugIsOn = true;
                }
                break;
            default:
                break;
        }
    }

    #endregion

    #region PUN Callbacks

    // Called when this client connects to PUN server
    public override void OnConnectedToMaster()
    {
        debugLog.text += "OnConnectedToMaster() was called.\n";

        PhotonNetwork.JoinRandomRoom();
    }

    // Called when this client disconnects from PUN
    public override void OnDisconnected(DisconnectCause cause)
    {
        debugLog.text += "OnDisconnected() was called.\n";
        // Play sound?
    }

    // Called when this client fails to join a random room (i.e. room has not been created yet)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        debugLog.text += "OnJoinRandomFailed() was called.\n";

        PhotonNetwork.CreateRoom(null, new RoomOptions());
    }

    // Called when this client joins a new room
    public override void OnJoinedRoom()
    {
        debugLog.text += "OnJoinedRoom() was called.\n";

        if (PhotonNetwork.IsMasterClient)
        {
            CreateAnchors();
        }
    }

    #endregion

    #region ASA Callbacks

    // Called by secondary client when it locates a spatial anchor. Creates an object in the scene
    // to represent the anchor's position.
    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        debugLog.text += "SpatialAnchorManager_AnchorLocated() was called.\n";

        if (args.Status == LocateAnchorStatus.Located)
        {
            // Create a new game object where the spatial anchor was found (force Unity to use main thread)
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                // Read out spatial anchor data
                CloudSpatialAnchor cloudSpatialAnchor = args.Anchor;

                // Create a new game object to represent spatial anchor
                GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                anchorGameObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
                anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.blue;

                // Link to spatial anchor
                anchorGameObject.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);

                // Keep a reference to the anchor game object for the secondary client
                anchorGameObjects.Add(anchorGameObject);
            });
        }

        if (anchorGameObjects.Count > 2)
        {
            CreateTriangle();
        }
    }

    #endregion

    #region Methods

    // Connects this client to PUN
    public void Connect()
    {
        debugLog.text += "Connect() was called.\n";
        
        // Connect to PUN server
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // Creates 3 spatial anchors organized into a triangle to define a shared coordinate
    // system between the clients
    public async void CreateAnchors()
    {
        await spatialAnchorManager.StartSessionAsync();
        await CreateAnchor(Vector3.zero, Quaternion.identity);                  // Point A
        await CreateAnchor(new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity); // Point B
        await CreateAnchor(new Vector3(0.4f, 0.0f, 0.0f), Quaternion.identity); // Point C
    }

    private async Task CreateAnchor(Vector3 position, Quaternion rotation)
    {
        debugLog.text += "CreateAnchor() was called.\n";

        // Create a game object to represent the anchor
        GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
        anchorGameObject.transform.position = position;
        anchorGameObject.transform.rotation = rotation;
        anchorGameObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        // Save the transform of the anchor at the origin as the main anchor transform
        if (position == Vector3.zero)
        {
            anchorTransform = anchorGameObject.transform;
        }

        // Attach the spatial anchor to the game object
        CloudNativeAnchor cloudNativeAnchor = anchorGameObject.AddComponent<CloudNativeAnchor>();
        await cloudNativeAnchor.NativeToCloud();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
        cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(3);

        // Collect spatial data for the anchor
        if (!spatialAnchorManager.IsReadyForCreate)
        {
            debugLog.text += "Look around to generate spatial data.\n";
            // Wait until enough data has been collected
            while (!spatialAnchorManager.IsReadyForCreate) { }
        }

        // Create the spatial anchor in the cloud
        try
        {
            await spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

            // Check if the spatial anchor was saved successfully
            bool saveSucceeded = (cloudSpatialAnchor != null);
            if (!saveSucceeded)
            {
                debugLog.text += "cloudSpatialAnchor did not save successfully.\n";
                return;
            }

            // Keep track of the identifier for the new spatial anchor
            createdAnchorIDs.Add(cloudSpatialAnchor.Identifier);

            // Keep a reference to the anchor game object for the master client
            anchorGameObjects.Add(anchorGameObject);

            // Tell the secondary client about the new spatial anchor
            debugLog.text += "Calling SetAnchorID() on secondary client...\n";
            PhotonView.Get(this).RPC("SetAnchorID", RpcTarget.Others, createdAnchorIDs[0]);
        }
        catch (Exception exception)
        {
            debugLog.text += "Failed to save spatial anchor " + exception.ToString() + "\n";
            return;
        }
    }

    // Used by master client to tell the secondary client to connect to ASA and start looking for an anchor.
    [PunRPC]
    public void SetAnchorID(string ID)
    {
        debugLog.text += "SetAnchorID() was called.\n";

        createdAnchorIDs.Add(ID);

        LocateAnchors();
    }

    // Find the 3 spatial anchors on the secondary client
    private async void LocateAnchors()
    {
        // Ensure this is the secondary client
        if (PhotonNetwork.IsMasterClient) return;

        // Wait until all 3 anchor IDs have been received
        if (createdAnchorIDs.Count < 3) return;

        debugLog.text += "LocateAnchors() was called.\n";
        
        // Connect secondary client to ASA
        await spatialAnchorManager.StartSessionAsync();

        // Create watcher for all 3 anchor IDs passed from the master client
        AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
        anchorLocateCriteria.Identifiers = createdAnchorIDs.ToArray();
        CloudSpatialAnchorWatcher watcher = spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);

        debugLog.text += "Watching for anchors...\n";
    }

    private void CreateTriangle()
    {
        if (anchorGameObjects.Count != 3)
        {
            debugLog.text += "Error: 3 spatial anchors not found.\n";
            return;
        }

        // Get references to the 3 spatial anchors
        GameObject pointD = anchorGameObjects[0];
        GameObject pointE = anchorGameObjects[1];
        GameObject pointF = anchorGameObjects[2];

        // Find distances between all the spatial anchors
        float distanceDE = Vector3.Distance(pointD.transform.position, pointE.transform.position);
        float distanceDF = Vector3.Distance(pointD.transform.position, pointF.transform.position);
        float distanceEF = Vector3.Distance(pointE.transform.position, pointF.transform.position);

        // Names for points on reference trangles
        GameObject pointA = null;  //      B
        GameObject pointB = null;  //      | \
        GameObject pointC = null;  //      A - C

        // Determine which spatial anchor represents which point on the reference triangle
        if (distanceDE > distanceDF)
        {
            if (distanceDE > distanceEF) 
            {
                // distanceDE must represent the hypotenuse

                if (distanceEF > distanceDF) // distanceEF represents x-axis
                {
                    // distanceEF must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointE;

                    // the other point on the x-axis must be pointA
                    pointA = pointF;

                    // the last remaining point must be pointB
                    pointB = pointD;
                }
                else // distanceDF > distanceEF
                {
                    // distanceDF must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointD;

                    // the other point on the x-axis must be pointA
                    pointA = pointF;

                    // the last remaining point must be pointB
                    pointB = pointE;
                }
            }
        }
        else // distanceDF > distanceDE
        {
            if (distanceDF > distanceEF)
            {
                // distanceDF must represent the hypotenuse

                if (distanceEF > distanceDE)
                {
                    // distanceEF must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointF;

                    // the other point on the x-axis must be pointA
                    pointA = pointE;

                    // the last remaining point must be pointB
                    pointB = pointD;
                }
                else // distanceDE > distanceEF
                {
                    // distanceDE must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointD;

                    // the other point on the x-axis must be pointA
                    pointA = pointE;

                    // the last remaining point must be pointB
                    pointB = pointF;
                }
            }
        }

        // Determine unit vectors of each shared axis using reference triangle
        Vector3 unitYAxis = Vector3.Normalize(pointB.transform.position - pointA.transform.position);
        Vector3 unitXAxis = Vector3.Normalize(pointC.transform.position - pointA.transform.position);
        Vector3 unitZAxis = Vector3.Cross(unitYAxis, unitXAxis);

        // Package the axes into a list for future use
        List<Vector3> axes = new List<Vector3>();
        axes.Add(unitXAxis);
        axes.Add(unitYAxis);
        axes.Add(unitZAxis);

        // Save the transform of pointA as the main anchor transform
        // This point is equivalent to the origin on the master client's coordinate system
        anchorTransform = pointA.transform;
    }

    // TODO not implemented
    public Transform GetAnchorTransform()
    {
        if (anchorTransform != null)
        {
            return anchorTransform;
        }
        else
        {
            return null;
        }
    }

    public List<Vector3> GetAxes()
    {
        return axes;
    }

    #endregion
}
