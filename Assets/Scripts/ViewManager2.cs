using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

using TMPro;

public class ViewManager2 : MonoBehaviour, IPunObservable
{
    // Current transform vectors of this shared object in primary coord space
    public Vector3 currentPosition;
    public Quaternion currentRotation;

    // Difference vector between primary and secondary coord spaces
    public Vector3 deltaPosition;
    public Quaternion deltaRotation;

    // Called by each client with a view of the object multiple times per frame.
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // If the photon view is owned by this client (i.e. master client)
        if (stream.IsWriting)
        {
            // Update the currentPosition variable with this game object's current position
            this.currentPosition = this.gameObject.transform.position - this.deltaPosition;
            this.currentRotation = this.gameObject.transform.rotation * Quaternion.Inverse(this.deltaRotation);

            Debug.Log(this.currentPosition);
            
            // Stream the position data to the other clients
            stream.SendNext(this.currentPosition);
            stream.SendNext(this.currentRotation);
        }
        // If the photon view is not owned by this client (i.e. secondary client)
        else
        {
            // Update the currentPosition variable with data from the stream
            this.currentPosition = (Vector3)stream.ReceiveNext();
            this.currentRotation = (Quaternion)stream.ReceiveNext();

            // Apply the callibration transformation for the secondary client
            this.gameObject.transform.position = this.currentPosition + this.deltaPosition;
            this.gameObject.transform.rotation = this.currentRotation * this.deltaRotation;
        }
    }

    // Saves the current callibration data (called by primary client for secondary client)
    [PunRPC]
    public void SetCallibration()
    {
        Debug.Log("SetCallibration() was called.");

        var unshared = GameObject.FindGameObjectsWithTag("UnsharedCube")[0];

        this.deltaPosition = unshared.transform.position - new Vector3(0.0f, 0.0f, 1.0f);
        this.deltaRotation = unshared.transform.rotation;
    }

    [PunRPC]
    public void RemoveUnsharedObject()
    {
        var unshared = GameObject.FindGameObjectsWithTag("UnsharedCube")[0];
        unshared.SetActive(false);
    }
}