using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using Photon.Realtime;

using TMPro;
using Microsoft.Azure.SpatialAnchors.Unity;
using static TMPro.TMP_Compatibility;

public class PUNManager : MonoBehaviourPunCallbacks
{
    #region Fields

    // Slate object with text for displaying debug info
    public TextMeshProUGUI debugLog;

    // The prefab for the callibration object
    public GameObject callibrationObjectPrefab;

    // Object used to get a position for the spatial anchor
    public GameObject callibrationObject;

    // Controls calls to ASA
    public ASAScript ASAManager;

    // The prefab for the object shared between clients
    public GameObject sharedObjectPrefab;

    // Object shared between clients
    public GameObject sharedObject;

    // Handles Photon View calls
    public SharedObjectManager sharedObjectManager;

    public Vector3 anchorPosition;

    public Quaternion anchorRotation;

    public TextMeshPro anchorText;
    public TextMeshPro sharedText;
    public TextMeshPro deltaText;

    bool shared;

    #endregion

    #region MonoBehaviour Callbacks

    public void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        shared = false;
    }

    public void Start()
    {
        this.debugLog.text = "Application starting.\n";
    }

    public void Update()
    {
        if (shared)
        {
            Vector3 deltaPosition = sharedObject.transform.position - callibrationObject.transform.position;
            Quaternion deltaRotation = Quaternion.Inverse(callibrationObject.transform.rotation) * sharedObject.transform.rotation;

            anchorText.text = "(" + callibrationObject.transform.position.x + ", " + callibrationObject.transform.position.y + ", " + callibrationObject.transform.position.z + ")";
            sharedText.text = "(" + sharedObject.transform.position.x + ", " + sharedObject.transform.position.y + ", " + sharedObject.transform.position.z + ")";
            deltaText.text = "(" + deltaPosition.x + ", " + deltaPosition.y  + ", " + deltaPosition.z + ")";

            PhotonView.Get(sharedObject).RPC("setTransformRPC", RpcTarget.Others, deltaPosition, deltaRotation);
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            GameObject quizObject = GameObject.FindWithTag("Quiz");

            if (quizObject != null)
            {
                GameObject anchorObject = GameObject.FindWithTag("Anchor");

                if (anchorObject == null)
                {
                    debugLog.text += "anchorObject not found!\n";
                }

                Vector3 deltaPosition = quizObject.transform.position - anchorObject.transform.position;
                Quaternion deltaRotation = Quaternion.Inverse(anchorObject.transform.rotation) * quizObject.transform.rotation;

                PhotonView.Get(quizObject).RPC("updateTransformRPC", RpcTarget.Others, deltaPosition, deltaRotation);
            }
            else
            {
                debugLog.text += "quizObject not found!\n";
            }
        }
    }

    #endregion

    #region PUN Callbacks

    // Called when this client connects to PUN
    public override void OnConnectedToMaster()
    {
        this.debugLog.text += "OnConnectedToMaster() was called. Joining a room...\n";
        PhotonNetwork.JoinRandomRoom();
    }

    // Called when this client disconnects from PUN
    public override void OnDisconnected(DisconnectCause cause)
    {
        this.debugLog.text += "OnDisconnected() was called with reason: " + cause.ToString() + "\n";
    }

    // Called when this client fails to join a random room (i.e. room has not been created yet)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        this.debugLog.text += "OnJoinRandomFailed() was called. Creating a new room.\n";
        PhotonNetwork.CreateRoom(null, new RoomOptions());
    }

    // Called when this client joins a new room
    public override void OnJoinedRoom()
    {
        this.debugLog.text += "Room joined succesfully.\n";
        Debug.Log("OnJoinedRoom() was called.");

        if (PhotonNetwork.IsMasterClient)
        {
            CreateCallibrationObject();
        }
    }

    #endregion

    #region Methods

    // Method triggered by the Connect button in the scene
    public void Connect()
    {
        this.debugLog.text += "Connecting...\n";

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // Creates a movable game object (only called by the master client)
    public void CreateCallibrationObject()
    {
        // This object is moved around in the scene by the master client to designate a
        // location for the spatial anchor. Once the callibrationObject is placed, the
        // master client presses the Callibrate button to save its position (see ASAScript).
        callibrationObject = Instantiate(callibrationObjectPrefab, new Vector3(0.0f, 0.0f, 0.5f), Quaternion.identity);
    }

    // For debugging purposes only
    public void ShareTest()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            sharedObject = PhotonNetwork.Instantiate(sharedObjectPrefab.name, Vector3.zero, Quaternion.identity, 0);

            sharedObjectManager = sharedObject.GetComponent<SharedObjectManager>();

            string testId = "fnisdlfjdsl;fm,smmfdl;s";

            sharedObjectManager.setId(testId);
        }
    }

    // Method called by the Share button in the scene
    public void Share()
    {
        // Button only works for the master client if the ASA session has been started
        if (ASAManager.spatialAnchorManager.IsSessionStarted && PhotonNetwork.IsMasterClient)
        {
            debugLog.text += "Sharing spatial anchor ID with secondary client.\n";

            shared = true;

            if (sharedObject == null)
            {
                // Create a shared photon object for both clients at the callibrationObject's saved position
                sharedObject = PhotonNetwork.Instantiate(sharedObjectPrefab.name, callibrationObject.transform.position, callibrationObject.transform.rotation, 0);

                if (sharedObject != null)
                {
                    debugLog.text += "sharedObject instantiated by master client.\n";
                }

                // Pass spatial anchor data to the secondary client
                sharedObjectManager = sharedObject.GetComponent<SharedObjectManager>();

                if (sharedObjectManager != null)
                {
                    debugLog.text += "sharedObjectManager found.\n";
                }

                setId(ASAManager.createdAnchorIDs[0]);

                // Save anchor position data for the shared object for master client
                setAnchorTransform(callibrationObject.transform.position, callibrationObject.transform.rotation);

                // Tell the secondary client to make a quiz
                PhotonView.Get(sharedObject).RPC("spawnQuiz", RpcTarget.Others);
            }
            else
            {
                debugLog.text += "sharedObject is not null.\n";
            }
        }
        else
        {
            debugLog.text += "IsSessionStarted = " + ASAManager.spatialAnchorManager.IsSessionStarted + "\n";
            debugLog.text += "IsMasterClient = " + PhotonNetwork.IsMasterClient + "\n";
        }
    }

    public void setId(string id)
    {
        debugLog.text += "setID() was called.\n";

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView.Get(sharedObject).RPC("setAnchorIdRPC", RpcTarget.Others, id);
        }
    }

    public void setAnchorTransform(Vector3 position, Quaternion rotation)
    {
        debugLog.text += "setAnchorTransform() was called.\n";

        if (PhotonNetwork.IsMasterClient)
        {
            debugLog.text += "Setting anchor position for master client: (" + position.x + ", " + position.y + ", " + position.z + ")\n";
        }
        else
        {
            debugLog.text += "Setting anchor position for secondary client: (" + position.x + ", " + position.y + ", " + position.z + ")\n";
            debugLog.text += "How did we get here?\n";
        }

        anchorPosition = position;
        anchorRotation = rotation;
    }

    public void ResetButton()
    {
        sharedObject.transform.position = callibrationObject.transform.position;
        sharedObject.transform.rotation = callibrationObject.transform.rotation;
    }

    #endregion
}
