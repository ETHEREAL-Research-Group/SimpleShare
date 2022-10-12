using Photon.Pun;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using TMPro;

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
            Vector3 deltaPosition = anchorTransform.position - gameObject.transform.position;
            Quaternion deltaRotation = Quaternion.Inverse(gameObject.transform.rotation) * anchorTransform.rotation;

            debugLog.text = "anchorTransform.position = (" + anchorTransform.position.x + ", " + anchorTransform.position.y + ", " + anchorTransform.position.z + ")\n"; 
            debugLog.text += "gameObject.transform.position = (" + gameObject.transform.position.x + ", " + gameObject.transform.position.y + ", " + gameObject.transform.position.z + ")\n"; 

            stream.SendNext(deltaPosition);
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
