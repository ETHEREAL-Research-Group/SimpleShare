using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using Photon.Realtime;

using TMPro;

public class SharedObjectManager : MonoBehaviour, IPunObservable
{
    #region Fields

    public TextMeshProUGUI debugLog;

    public GameObject anchorObject;

    public Vector3 anchorPosition;

    public Quaternion anchorRotation;

    public Vector3 deltaPosition;

    public Quaternion deltaRotation;

    public TextMeshPro text;

    public TextMeshPro anchorText;
    public TextMeshPro sharedText;
    public TextMeshPro deltaText;

    #endregion

    void Update()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Never called on master client???

            //deltaPosition = gameObject.transform.position - anchorPosition;
            //deltaRotation = Quaternion.Inverse(anchorRotation) * gameObject.transform.rotation;

            //PhotonView.Get(this).RPC("setTransformRPC", RpcTarget.Others, deltaPosition, deltaRotation);

            //debugLog.text += "Master client calling Update() on SharedObject.\n";
        }
        else
        {
            GameObject ASAManager = GameObject.FindGameObjectWithTag("ASAManager");
            ASAScript script = ASAManager.GetComponent<ASAScript>();

            anchorPosition = script.anchorGameObject.transform.position;
            anchorRotation = script.anchorGameObject.transform.rotation;

            gameObject.transform.position = anchorPosition + deltaPosition;
            gameObject.transform.rotation = anchorRotation * deltaRotation;
        }

        // Only being called by secondary client ? (added test case)
        // Shared != anchor initially (added fix)
        // Not sure if delta values are correct or not (further testing req'd)
        anchorText = GameObject.FindGameObjectWithTag("Anchor").GetComponent<TextMeshPro>();
        sharedText = GameObject.FindGameObjectWithTag("Shared").GetComponent<TextMeshPro>();
        deltaText = GameObject.FindGameObjectWithTag("Delta").GetComponent<TextMeshPro>();

        anchorText.text = "(" + anchorPosition.x + ", " + anchorPosition.y + ", " + anchorPosition.z + ")";
        sharedText.text = "(" + transform.position.x + ", " + transform.position.y + ", " + transform.position.z + ")";
        deltaText.text = "(" + deltaPosition.x + ", " + deltaPosition.y + ", " + deltaPosition.z + ")";
    }

    #region IPunObservable Callbacks

    // Streams data for this shared game object between clients
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        
    }

    #endregion

    #region Methods

    // Sets the position/rotation wrt the spatial anchor for the secondary client
    // every frame based on the change in position of the master client's shared
    // object wrt the spatial anchor
    [PunRPC]
    public void setTransformRPC(Vector3 pos, Quaternion rot)
    {
        deltaPosition = pos;
        deltaRotation = rot;
    }

    // Called by master client to pass spatial anchor ID to secondary client view RPC
    public void setId(string id)
    {
        debugLog.text += "setID() was called.\n";

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView.Get(this).RPC("setAnchorIdRPC", RpcTarget.Others, id);
        }
    }

    // Used by the secondary client to call the locate method to find the spatial anchor in ASA
    [PunRPC]
    public void setAnchorIdRPC(string id)
    {
        // Get reference to debugLog and update it
        debugLog = GameObject.FindGameObjectWithTag("DebugLog").GetComponent<TextMeshProUGUI>();
        debugLog.text += "Secondary client received ID: " + id + "\n";

        // Call the spatial anchor locate method of ASAScript
        GameObject ASAManager = GameObject.FindGameObjectWithTag("ASAManager");
        ASAScript script = ASAManager.GetComponent<ASAScript>();
        script.Locate(id);
    }

    // Used to save the spatial anchor's position/rotation for future reference
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
        }

        anchorPosition = position;
        anchorRotation = rotation;
    }

    // Called by the secondary client to spawn the quiz prefab
    [PunRPC]
    public void spawnQuiz()
    {
        PhotonNetwork.Instantiate("Quiz", new Vector3(0.0f, 0.0f, 2.0f), Quaternion.identity);
    }

    #endregion
}
