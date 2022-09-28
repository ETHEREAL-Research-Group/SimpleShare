using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

public class NetSync : MonoBehaviour, IPunObservable
{
    // Tracks this object's positon/rotation for network clients
    public Vector3 networkLocalPosition;
    public Quaternion networkLocalRotation;

    // Tracks this object's starting positon/rotation
    public Vector3 startingLocalPosition;
    public Quaternion startingLocalRotation;

    // A reference to this object's Photon View component
    public PhotonView photonView;

    // The offset between the secondary client's view and the primary client's view
    public Vector3 offset;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // If this object belongs to this client, write its new position to the network
        if (stream.IsWriting)
        {
            stream.SendNext(this.transform.localPosition);
            stream.SendNext(this.transform.localRotation);
        }
        // If this object does not belong to this client, read its new position to the network
        else
        {
            networkLocalPosition = (Vector3)stream.ReceiveNext();
            networkLocalRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    public void Start()
    {
        // Set initial offset
        this.offset = new Vector3(0.0f, 0.09f, 0.0f);

        Transform trans = this.transform;
        
        // Set this object's initial position/rotation when it is instantiated
        startingLocalPosition = trans.localPosition;
        startingLocalRotation = trans.localRotation;

        // Set this object's initial position/rotation for network clients
        networkLocalPosition = startingLocalPosition;
        networkLocalRotation = startingLocalRotation;
    }

    public void Update()
    {
        // If this object does not belong to this client, update its position based
        // on the most recently streamed network data
        if (!photonView.IsMine)
        {
            Transform trans = this.transform;
            trans.localPosition = networkLocalPosition;
            trans.localRotation = networkLocalRotation;
        }
    }

    // Empty functions to prevent errors
    // TODO Remove these and their dependants
    [PunRPC]
    public void RemoveUnsharedCube()
    {

    }

    [PunRPC]
    public void SetMasterCallibrationTransform(Vector3 v, Quaternion q)
    {

    }
}
