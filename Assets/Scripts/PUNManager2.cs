using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using Photon.Realtime;

using TMPro;

public class PUNManager2 : MonoBehaviourPunCallbacks
{
    #region Public Fields

    public Camera camera;

    public GameObject unsharedPrefab;

    public GameObject unsharedObject;

    public GameObject sharedPrefab;

    public GameObject sharedObject;
    
    public Vector3 startingPosition;

    public Quaternion startingRotation;

    public Vector3 callibrationPosition;

    public Quaternion callibrationRotation;

    #endregion

    #region MonoBehaviour Callbacks

    public void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void Start()
    {
        this.startingPosition = camera.transform.position;
        this.startingRotation = camera.transform.rotation;
    }

    #endregion

    #region PUN Callbacks

    // Called when the client connects to the Master Server and is ready
    // for matchmaking and other tasks.
    public override void OnConnectedToMaster()
    {
        Debug.Log("PUN: OnConnectedToMaster() was called. Joining a room...");

        PhotonNetwork.JoinRandomRoom();
    }

    // Called after disconnecting from the Master Server.
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogFormat("PUN: OnDisconnected() was called by PUN with reason {0}.", cause);
    }

    // Called if the client fails to join a random room.
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("PUN: OnJoinRandomFailed() was called. Creating new room...");

        // Create a new room
        PhotonNetwork.CreateRoom(null, new RoomOptions());
    }

    // Called if the client successfully joins a room.
    public override void OnJoinedRoom()
    {
        Debug.Log("PUN: OnJoinedRoom() was called.");

        // Display in app
        var logger = GameObject.FindGameObjectsWithTag("Logger")[0];
        string temp = "Connected.";
        logger.GetComponent<TextMeshPro>().text = temp;

        this.unsharedObject = Instantiate(this.unsharedPrefab, new Vector3(0.0f, 0.0f, 1.0f), Quaternion.identity);
    }

    #endregion

    #region Public Methods

    // Connect to PUN.
    public void Connect()
    {
        // Check if already connected to Photon Network
        if (!PhotonNetwork.IsConnected)
        {
            // Connect to Photon Network if not connected already
            Debug.Log("Connecting to PUN.");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.Log("Already connected to PUN!");
        }
    }

    public void Callibrate()
    {
        // Get the callibration position from the unshared object
        this.callibrationPosition = this.unsharedObject.transform.position;
        this.callibrationRotation = this.unsharedObject.transform.rotation;

        // If this is the master client, send your callibration data to the secondary client and
        // create a new, shared object
        if (PhotonNetwork.IsMasterClient)
        {
            // Create the new shared object
            this.sharedObject = PhotonNetwork.Instantiate(this.sharedPrefab.name, this.callibrationPosition, this.callibrationRotation, 0);

            // Tell secondary client to save its current callibration data via the shared object
            this.sharedObject.GetComponent<PhotonView>().RPC("SetCallibration", RpcTarget.All);

            // Remove callibration objects from scene
            this.sharedObject.GetComponent<PhotonView>().RPC("RemoveUnsharedObject", RpcTarget.All);

            // Update the logger
            var logger = GameObject.FindGameObjectsWithTag("Logger")[0];
            string temp = "Callibrated.";
            logger.GetComponent<TextMeshPro>().text = temp;
        }
    }

    #endregion
}
