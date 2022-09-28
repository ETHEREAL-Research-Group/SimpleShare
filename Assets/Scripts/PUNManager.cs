using UnityEngine;

using Photon.Pun;
using Photon.Realtime;
using TMPro;

namespace SimpleShare
{
    public class PUNManager : MonoBehaviourPunCallbacks
    {
        #region Public Fields

        // A reference to the Unshared Cube prefab
        public GameObject unsharedCubePrefab;

        // A reference to an instantiated Unshared Cube
        public GameObject callibrationCube;

        public Vector3 callibrationPosition;

        public Quaternion callibrationRotation;

        // A reference to the Shared Cube prefab
        public GameObject sharedCubePrefab;

        // A reference to an instantiated Shared Cube
        public GameObject sharedCube;

        public GameObject testCube;

        #endregion

        #region MonoBehaviour Callbacks

        public void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
        }

        #endregion

        #region PUN Callbacks

        // Called when the client connects to the Master Server and is ready
        // for matchmaking and other tasks.
        public override void OnConnectedToMaster()
        {
            //Debug.Log("PUN: OnConnectedToMaster() was called.");

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
            //Debug.Log("PUN: OnJoinRandomFailed() was called.");

            // Create a new room
            PhotonNetwork.CreateRoom(null, new RoomOptions());
        }

        // Called if the client successfully joins a room.
        public override void OnJoinedRoom()
        {
            Debug.Log("PUN: OnJoinedRoom() was called.");

            var logger = GameObject.FindGameObjectsWithTag("Logger")[0];

            string temp = "Connected.";

            logger.GetComponent<TextMeshPro>().text = temp;


            // Instantiate a callibration cube for this client.
            //this.callibrationCube = Instantiate(this.unsharedCubePrefab, new Vector3(0.0f, 0.0f, 1.0f), Quaternion.identity);
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
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                Debug.Log("Already connected to PUN!");
            }
        }

        // Disconnect from PUN.
        public void Disconnect()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
        }

        // Get PUN connection ping.
        public void Test()
        {
            int ping = PhotonNetwork.GetPing();

            if (ping != null)
            {
                Debug.LogFormat("Ping = {0}", ping);
            }
            else
            {
                Debug.Log("Not connected to PUN.");
            }
        }

        public void Callibrate()
        {
            // This button only works for the master client
            if (PhotonNetwork.IsMasterClient) // && this.callibrationCube.active)
            {
                // Save the current position of this client's callibration cube
                //this.callibrationPosition = this.callibrationCube.transform.position;

                // Save the current rotation of this client's callibration cube
                //this.callibrationRotation = this.callibrationCube.transform.rotation;

                // Generate a new shared cube for both clients at this client's callibration positon
                //this.sharedCube = PhotonNetwork.Instantiate(this.sharedCubePrefab.name, this.callibrationPosition, this.callibrationRotation, 0);
                this.sharedCube = PhotonNetwork.Instantiate(this.testCube.name, new Vector3(0.0f, 0.0f, 1.0f), Quaternion.identity, 0);

                // Call for all client's connected to remove their callibration cubes from the scene (no longer needed)
                // This method belongs to the ViewManager on the SharedCube game object as an RPC requires a PhotonView
                // component to execute across the newtwork.
                this.sharedCube.GetComponent<PhotonView>().RPC("RemoveUnsharedCube", RpcTarget.All);

                // Give the secondary client this client's callibration position
                this.sharedCube.GetComponent<PhotonView>().RPC("SetMasterCallibrationTransform", RpcTarget.All, this.callibrationPosition, this.callibrationRotation);
            }
        }

        public void Reset()
        {

        }

        #endregion
    }
}