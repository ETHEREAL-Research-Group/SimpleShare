using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;
using UnityEngine.InputSystem.Android;

[RequireComponent(typeof(PhotonView))]
public class SimpleShareSync : MonoBehaviour, IPunObservable
{
    private PhotonView photonView;

    private SimpleShare simpleShare;

    private Transform anchorTransform;

    public TextMeshProUGUI debugLog;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        anchorTransform = simpleShare.GetAnchorTransform();

        if (stream.IsWriting)
        {
            debugLog.text = "stream.IsWriting";

            Vector3 deltaPosition = anchorTransform.position - gameObject.transform.position;
            Quaternion deltaRotation = Quaternion.Inverse(gameObject.transform.rotation) * anchorTransform.rotation;

            stream.SendNext(deltaPosition);
            stream.SendNext(deltaRotation);
        }
        else
        {
            debugLog.text = "stream.IsReading";

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

            // Apply the movement to the object in the secondary client's coordinate system
            gameObject.transform.position = anchorTransform.position + deltaX + deltaY + deltaZ;
            gameObject.transform.rotation = anchorTransform.rotation * deltaRotation;
        }
    }

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
}
