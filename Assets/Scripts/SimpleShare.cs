using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Photon.Pun;
using Photon.Realtime;

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Input;

using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.Azure.SpatialAnchors;
using UnityEditorInternal;
using Microsoft.MixedReality.Toolkit;

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

    public bool debugIsOn;

    #endregion

    #region Unity Callbacks

    public void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void Start()
    {
        debugIsOn = true;

        spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);

        PUNSetup();
    }

    public void Update()
    {
        // Update shared object positions
        foreach (var sharedObject in sharedObjects)
        {
            if (sharedObject.GetComponent<PhotonView>().IsMine)
            {
                SetPosition(sharedObject);
            }
            else
            {
                GetPosition(sharedObject);
            }
        }

        // Update shared object states
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
        PhotonNetwork.JoinRandomRoom();
    }

    // Called when this client disconnects from PUN
    public override void OnDisconnected(DisconnectCause cause)
    {
        // Not implemented
        // Play sound?
        // Display message?
    }

    // Called when this client fails to join a random room (i.e. room has not been created yet)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        PhotonNetwork.CreateRoom(null, new RoomOptions());
    }

    // Called when this client joins a new room
    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnCallibrationObject();
        }
    }

    #endregion

    #region ASA Callbacks

    // Called by secondary client when it locates a spatial anchor. Creates an object in the scene
    // to represent the anchor's position.
    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
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

    // Connects this client to PUN
    public void PUNSetup()
    {
         // Connect to PUN server
         PhotonNetwork.ConnectUsingSettings();
    }
      
    // Called by the master cient to spawn a movable callibration object in the scene
    public void SpawnCallibrationObject()
    {
        Vector3 initialPosition = new Vector3(0.0f, 0.0f, 0.5f);
        Quaternion initialRotation = Quaternion.identity;

        callibrationObject = Instantiate(callibrationObjectPrefab, initialPosition, initialRotation);
    }

    // TODO Requires some form of input from user to toggle
    public void SaveCallibrationTransform()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Prevent anchor from being moved futher
            callibrationObject.GetComponent<ObjectManipulator>().enabled = false;
            callibrationObject.GetComponent<NearInteractionGrabbable>().enabled = false;

            MasterASASetup();
        }
    }

    // Starts an ASA session for the master client and creates an anchor at the callibration position.
    public async void MasterASASetup()
    {
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
                return;
            }

            // Keep track of the identifier for the new spatial anchor
            createdAnchorIDs.Add(cloudSpatialAnchor.Identifier);
            PhotonView.Get(this).RPC("setAnchorID", RpcTarget.Others, createdAnchorIDs);
        }
        catch (Exception exception)
        {
            string temp = "Failed to save spatial anchor " + exception.ToString() + "\n";
            return;
        }
    }

    // Starts an ASA session for the secondary client and finds the master client's spatial anchor.
    public async void SecondaryASASetup()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            await spatialAnchorManager.StartSessionAsync();

            if (createdAnchorIDs.Count > 0)
            {
                // Create a watcher for the spatial anchor with id
                AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
                anchorLocateCriteria.Identifiers = createdAnchorIDs.ToArray();
                CloudSpatialAnchorWatcher watcher = spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
            }
        }
    }

    // Used by master client to tell the secondary client to connect to ASA and start looking for an anchor.
    [PunRPC]
    public void SetAnchorID(List<string> IDs)
    {
        createdAnchorIDs = IDs;

        SecondaryASASetup();
    }

    // TODO Add Tag as string argument and use that to find and identify shared objects between clients

    public GameObject SpawnSharedObject(GameObject prefab, string tag, Vector3 pos)
    {
        GameObject newObject = PhotonNetwork.Instantiate(prefab.name, pos, Quaternion.identity, 0);



        sharedObjects.Add(newObject);
        
        return newObject;
    }

    public void GetPosition(GameObject sharedObject)
    {

    }

    public void SetPosition(GameObject sharedObject)
    {
        PhotonView.Get(this).RPC("SetPositionRPC", RpcTarget.Others, sharedObject.transform);
    }

    [PunRPC]
    public void SetPositionRPC(Transform trans)
    {

    }

    [PunRPC]
    public void SetDeltaTransform()
    {

    }


    public void ChangeOwnership()
    {

    }

}
