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

[RequireComponent(typeof(ARSessionOrigin))]
[RequireComponent(typeof(ARAnchorManager))]
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(SpatialAnchorManager))]
public class SimpleShare : MonoBehaviourPunCallbacks, IMixedRealitySpeechHandler
{
    #region Fields

    public GameObject callibrationObjectPrefab;

    private GameObject callibrationObject;

    public GameObject anchorObjectPrefab;

    private GameObject anchorObject;

    private Transform deltaTransform;

    public SpatialAnchorManager spatialAnchorManager;

    public List<string> createdAnchorIDs = new List<String>();

    public List<GameObject> sharedObjects = new List<GameObject>();

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
        anchorObject = null;
        callibrationObject = null;

        debugLog.text += "Start() was called.\n";

        // Toggle debug mode off initially
        debugIsOn = false;

        // Subscribe to spatial anchor location callbacks
        spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

        // Subscribe to speech input callbacks
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);

        // Connect to PUN server
        //PUNSetup();
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
            SetAnchorTransform();
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
                anchorObject = Instantiate(anchorObjectPrefab, Vector3.zero, Quaternion.identity);

                // Link to spatial anchor
                anchorObject.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);
            });
        }
    }

    #endregion

    #region Methods

    // Connects this client to PUN
    public void PUNSetup()
    {
        debugLog.text += "PUNSetup() was called.\n";
        
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

    // TODO Requires some form of input from user to toggle
    // Or do we even need the user to move a callibration object?
    // We could just create callibration objects automatically in front of the user
    // Feel like this is kind of a hold-over the manual callibration trials
    private void SetAnchorTransform()
    {
        debugLog.text += "SetAnchorTransform() was called.\n";

        // Create anchor object
        Vector3 initialPosition = Vector3.zero;
        Quaternion initialRotation = Quaternion.identity;
        callibrationObject = Instantiate(callibrationObjectPrefab, initialPosition, initialRotation);

        // Prevent anchor from being moved futher
        callibrationObject.GetComponent<ObjectManipulator>().enabled = false;
        callibrationObject.GetComponent<NearInteractionGrabbable>().enabled = false;

        //MasterASASetup();
    }

    // Starts an ASA session for the master client and creates an anchor at the callibration position.
    // TODO Create 3 anchors instead of 1 oriented in a triangle
    public async void MasterASASetup()
    {
        debugLog.text += "MasterASASetup() was called.\n";

        await spatialAnchorManager.StartSessionAsync();

        CloudNativeAnchor cloudNativeAnchor = callibrationObject.AddComponent<CloudNativeAnchor>();
        await cloudNativeAnchor.NativeToCloud();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
        cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(3);

        // Collect environment data (if necessary)
        if (!spatialAnchorManager.IsReadyForCreate)
        {
            // Track and update collection progress (if necessary)
            while (!spatialAnchorManager.IsReadyForCreate) 
            {
                
            }
        }

        // Create the ASA
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

        SecondaryASASetup();
    }

    // Starts an ASA session for the secondary client and finds the master client's spatial anchor.
    private async void SecondaryASASetup()
    {
        debugLog.text += "SecondaryASASetup() was called.\n";

        if (!PhotonNetwork.IsMasterClient)
        {
            await spatialAnchorManager.StartSessionAsync();

            if (createdAnchorIDs.Count > 0)
            {
                // Create a watcher for the spatial anchor with id
                AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
                anchorLocateCriteria.Identifiers = createdAnchorIDs.ToArray();
                CloudSpatialAnchorWatcher watcher = spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);

                debugLog.text += "Locating spatial anchor...\n";
            }
        }
    }

    public Transform GetAnchorTransform()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (callibrationObject == null)
            {
                return null;
            }
            else
            {
                return callibrationObject.transform;
            }
        }
        else
        {
            if (anchorObject == null)
            {
                return null;
            }
            else
            {
                return anchorObject.transform;
            }
        }
    }

    #endregion
}
