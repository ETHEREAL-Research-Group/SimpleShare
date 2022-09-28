using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

using TMPro;

public class ViewManager : MonoBehaviour, IPunObservable
{
    // Callibration data for primary coord space
    public Vector3 primaryCallibrationPosition;
    public Quaternion primaryCallibrationRotation;

    // Callibration data for secondary coord space
    public Vector3 secondaryCallibrationPosition;
    public Quaternion secondaryCallibrationRotation;

    // Transformation matrix to convert from primary coord space to secondary coord space
    public Matrix4x4 transMatrix;

    // Difference vector between primary and secondary coord spaces
    public Vector3 deltaPosition;
    public Quaternion deltaRotation;

    // Current transform vectors of this shared object in primary coord space
    public Vector3 currentPosition;
    public Quaternion currentRotation;

    // Temporary vectors for calculations (saved and made public for debugging purposes)
    public Vector4 currentPositionV4;
    public Vector4 positionAfterTrans;
    public Quaternion rotationAfterTrans;
    public Vector3 newPosition;

    // World space coord
    public Vector3 worldPosition;

    public GameObject logger;

    public void Update()
    {
        this.worldPosition = this.gameObject.transform.position;

        this.logger = GameObject.FindGameObjectsWithTag("Logger")[0];

        string temp = "(" + this.worldPosition.x + ", " + this.worldPosition.y + ", " + this.worldPosition.z + ")";

        this.logger.GetComponent<TextMeshPro>().text = temp;
    }

    // Called by each client with a view of the object multiple times per frame.
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // If the photon view is owned by this client (i.e. master client)
        if (stream.IsWriting)
        {
            // Update the currentPosition variable with this game object's current position
            this.currentPosition = this.gameObject.transform.position;
            this.currentRotation = this.gameObject.transform.rotation;

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
            Vector3 currentScale = new Vector3(1.0f, 1.0f, 1.0f);

            // Perform coordinate system transform on new position data ----------------------

            // Convert to Vec4 for matrix multiplication
            this.currentPositionV4 = new Vector4(this.currentPosition.x, this.currentPosition.y, this.currentPosition.z, 1.0f);

            // Multiply transformation matrix and current position to get new position
            this.positionAfterTrans = this.transMatrix * this.currentPositionV4;

            // Save new position as Vec3 for use
            this.newPosition = new Vector3(this.positionAfterTrans.x, this.positionAfterTrans.y, this.positionAfterTrans.z);

            // Update this game object's position with the new position data. This will
            // move the game object on the secondary client's screen based on how the
            // master client is moving it.
            this.gameObject.transform.position = this.currentPosition; //this.newPosition;
            this.gameObject.transform.rotation = this.secondaryCallibrationRotation * this.currentRotation;
        }
    }

    // Remote procedure call - a method that is called from another client on
    // the network.
    [PunRPC]
    void RemoveUnsharedCube()
    {
        // Get all unshared callibration objects in this client's scene
        GameObject[] unshared = GameObject.FindGameObjectsWithTag("UnsharedCube");

        //Debug.LogFormat("Unshared GameObject found: {0}", unshared[0].name);

        // Save the position of this client's callibration object
        Vector3 pos = unshared[0].transform.position;
        Quaternion rot = unshared[0].transform.rotation;

        // Deactivate this client's callibration object
        unshared[0].gameObject.SetActive(false);

        // Save position as callibrationPosition if this is the master client,
        // otherwise save the position as secondaryCallibrationPosition.
        if (PhotonNetwork.IsMasterClient)
        {
            primaryCallibrationPosition = pos;
            primaryCallibrationRotation = rot;
        }
        else
        {
            secondaryCallibrationPosition = pos;
            secondaryCallibrationRotation = rot;
        }
    }

    [PunRPC]
    void SetMasterCallibrationTransform(Vector3 pos, Quaternion rot)
    {
        // Only updated for secondary client
        if (!PhotonNetwork.IsMasterClient)
        {
            primaryCallibrationPosition = pos;
            primaryCallibrationRotation = rot;

            // Translation
            this.deltaPosition = secondaryCallibrationPosition - primaryCallibrationPosition;
            Matrix4x4 deltaPos = Matrix4x4.Translate(this.deltaPosition);

            // Rotation
            Matrix4x4 primaryRot = Matrix4x4.Rotate(primaryCallibrationRotation);
            Matrix4x4 secondaryRot = Matrix4x4.Rotate(secondaryCallibrationRotation);
            Matrix4x4 deltaRot = primaryRot * secondaryRot;

            // Scale
            Vector3 scale = new Vector3(1.0f, 1.0f, 1.0f);
            Matrix4x4 deltaScale = Matrix4x4.Scale(scale);

            // Calculate a transformation matrix for coord system transform.
            Matrix4x4 TRS = deltaPos * deltaRot * deltaScale;

            //this.transMatrix = Matrix4x4.TRS(deltaPosition, deltaRot, scale);
            this.transMatrix = TRS;
        }
    }
}
