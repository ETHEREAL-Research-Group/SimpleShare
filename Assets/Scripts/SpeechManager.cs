using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using UnityEngine.Events;

public class SpeechManager : MonoBehaviour, IMixedRealitySpeechHandler
{
    public GameObject debugObjects;

    public TextMeshProUGUI debugLog;

    public bool debugIsOn;

    public void Start()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);

        debugIsOn = true;
    }

    public void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        switch (eventData.Command.Keyword.ToLower())
        {
            case "toggle debug mode":
                if (debugIsOn)
                {
                    debugObjects.SetActive(false);
                    debugIsOn = false;
                }
                else
                {
                    debugObjects.SetActive(true);
                    debugIsOn = true;
                }
                break;
            case "hello":
                debugLog.text = "hello\n";
                break;
            default:
                break;
        }
    }
}
