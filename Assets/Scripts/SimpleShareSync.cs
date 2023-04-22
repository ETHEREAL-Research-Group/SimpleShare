using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

[RequireComponent(typeof(PhotonView))]
public class SimpleShareSync : MonoBehaviour, IPunObservable
{
    private PhotonView photonView;

    private Transform anchorTransform;

    public TextMeshProUGUI debugLog;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (anchorTransform != null)
        {
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

                gameObject.transform.position = anchorTransform.position + deltaPosition;
                gameObject.transform.rotation = anchorTransform.rotation * deltaRotation;

                if (gameObject.transform.position == deltaPosition || gameObject.transform.rotation == deltaRotation)
                {
                    anchorTransform = GameObject.FindWithTag("SimpleShare").GetComponent<SimpleShare>().GetAnchorTransform();
                    gameObject.transform.position = anchorTransform.position + deltaPosition;
                    gameObject.transform.rotation = anchorTransform.rotation * deltaRotation;
                }
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        anchorTransform = null;
        photonView = PhotonView.Get(this);
    }

    // Update is called once per frame
    void Update()
    {
        if (anchorTransform == null)
        {
            debugLog.text = "anchorTransform == null";
            anchorTransform = GameObject.FindWithTag("SimpleShare").GetComponent<SimpleShare>().GetAnchorTransform();

            if (anchorTransform != null)
            {
                debugLog.text = "anchorTransform != null";
            }
        }
    }
}
