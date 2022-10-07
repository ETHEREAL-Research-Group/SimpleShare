using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

using TMPro;
using static TMPro.TMP_Compatibility;

public class QuizManager : MonoBehaviour, IPunObservable
{
    public string defaultText;

    public TextMeshPro textOutput;

    public GameObject buttonOne;

    public TextMeshPro buttonOneText;

    public GameObject buttonTwo;

    bool tryAgain;

    public void Start()
    {
        defaultText = "The University of Calgary is located in Edmonton?";

        tryAgain = false;
    }

    public void ButtonOne()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            if (!tryAgain)
            {
                tryAgain = true;

                textOutput.text = "Try again!";

                buttonTwo.SetActive(false);

                buttonOneText.text = "";
            }
            else
            {
                tryAgain = false;

                textOutput.text = defaultText;

                buttonTwo.SetActive(true);

                buttonOneText.text = "Yes";
            }
        }
    }

    public void ButtonTwo()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            textOutput.text = "Correct!";
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {

    }

    [PunRPC]
    public void updateTransformRPC(Vector3 pos, Quaternion rot)
    {
        GameObject anchorObject = GameObject.FindWithTag("CallibrationObject");

        if (anchorObject == null)
        {
            GameObject debug = GameObject.FindWithTag("DebugLog");
            TextMeshPro debugLog = debug.GetComponent<TextMeshPro>();

            debugLog.text += "anchorObject not found!\n";
        }

        gameObject.transform.position = anchorObject.transform.position + pos;
        gameObject.transform.rotation = anchorObject.transform.rotation * rot;
    }

    [PunRPC]
    public void changeButtonState(bool tryAgain)
    {
        if (tryAgain)
        {
            // Do something
        }
        else
        {
            // Do something else
        }
    }
}
