using Photon.Pun;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using TMPro;

[RequireComponent(typeof(PhotonView))]
public class SimpleShareSync : MonoBehaviour, IPunObservable
{
    #region Fields

    // Tracks the PhotonView component on this synchronized object
    private PhotonView photonView;

    // The master SimpleShare script in the scene
    private SimpleShare simpleShare;

    // The shared anchor transform (origin)
    private Transform anchorTransform;

    // Used to print debug messages for the HoloLens2 headset
    public TextMeshProUGUI debugLog;

    #endregion

    #region IPunObservable Callbacks

    // Sends transform data about this synchronized object to other clients on the network
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        anchorTransform = simpleShare.GetAnchorTransform();

        if (stream.IsWriting)
        {
            Vector3 deltaPosition = anchorTransform.position - gameObject.transform.position;
            Quaternion deltaRotation = Quaternion.Inverse(anchorTransform.rotation) * gameObject.transform.rotation;

            float scalarX = deltaPosition.x;
            float scalarY = deltaPosition.y;
            float scalarZ = deltaPosition.z;

            List<Vector3> axes = simpleShare.GetAxes();
            Vector3 deltaX = scalarX * axes[0];
            Vector3 deltaY = scalarY * axes[1];
            Vector3 deltaZ = scalarZ * axes[2];
            Vector3 totalDelta = deltaX + deltaY + deltaZ;

            debugLog.text = "anchorTransform.position = (" + anchorTransform.position.x + ", " + anchorTransform.position.y + ", " + anchorTransform.position.z + ")\n";
            debugLog.text += "axes[0] = (" + axes[0].x + ", " + axes[0].y + ", " + axes[0].z + ")\n";
            debugLog.text += "axes[1] = (" + axes[1].x + ", " + axes[1].y + ", " + axes[1].z + ")\n";
            debugLog.text += "axes[2] = (" + axes[2].x + ", " + axes[2].y + ", " + axes[2].z + ")\n";
            //debugLog.text += "gameObject.transform.position = (" + gameObject.transform.position.x + ", " + gameObject.transform.position.y + ", " + gameObject.transform.position.z + ")\n"; 

            stream.SendNext(totalDelta);
            stream.SendNext(deltaRotation);
        }
        else
        {
            Vector3 deltaPosition = (Vector3)stream.ReceiveNext();
            Quaternion deltaRotation = (Quaternion)stream.ReceiveNext();

            // Convert movement from master client into a scalar value wrt each axis
            float scalarX = deltaPosition.x;
            float scalarY = deltaPosition.y;
            float scalarZ = deltaPosition.z;

            // Multiply the scalar values from the master client with the secondary client's
            // unit vectors for their synchronized coordinate system
            List<Vector3> axes = simpleShare.GetAxes();
            Vector3 deltaX = scalarX * axes[0];
            Vector3 deltaY = scalarY * axes[1];
            Vector3 deltaZ = scalarZ * axes[2];

            /*
            debugLog.text = "scalarX = " + scalarX + "\n";
            debugLog.text += "scalarY = " + scalarY + "\n";
            debugLog.text += "scalarZ = " + scalarZ + "\n";

            debugLog.text += "axes[0] = " + axes[0] + "\n";
            debugLog.text += "axes[1] = " + axes[1] + "\n";
            debugLog.text += "axes[2] = " + axes[2] + "\n";

            debugLog.text += "anchorTransform.position = (" + anchorTransform.position.x + ", " + anchorTransform.position.y + ", " + anchorTransform.position.z + ")\n";
            */

            // Apply the movement to the object in the secondary client's coordinate system
            gameObject.transform.position = anchorTransform.position + deltaX + deltaY + deltaZ;
            gameObject.transform.rotation = anchorTransform.rotation * deltaRotation;
        }
    }

    #endregion

    #region Unity Callbacks

    // Start is called before the first frame update
    void Start()
    {
        simpleShare = null;
        anchorTransform = null;
        photonView = PhotonView.Get(this);
    }

    // Update is called once per frame
    void Update()
    {
        if (simpleShare == null)
        {
            simpleShare = GameObject.FindWithTag("SimpleShare").GetComponent<SimpleShare>();
        }
    }

    #endregion

    #region Methods

    // Resets the position of this synchronized object to the shared anchor position
    public void ResetPosition()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.rotation = Quaternion.identity;
        }
        else
        {
            gameObject.transform.position = anchorTransform.position;
            gameObject.transform.rotation = anchorTransform.rotation;
        }
    }

    // If this client is attempting to move a synchronized object and is not it's owner,
    // then take ownership of the object
    public void TakeOwnership()
    {
        if (!photonView.IsMine)
        {
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);

            if (photonView.IsMine)
            {
                debugLog.text += "Ownership taken succesfully.\n";
            }
        }
    }

    #endregion
}
