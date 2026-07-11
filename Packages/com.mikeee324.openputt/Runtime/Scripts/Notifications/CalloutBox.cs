using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

public class CalloutBox : UdonSharpBehaviour
{
    
    public GameObject targetObject;
    private VRCTweenHandle _positionTweenHandle;
    private VRCTweenHandle _scaleTweenHandle;
    public Vector3 startPos;
    public Vector3 targetPos;
    public float speed = 2f;
    public float delay = 2f;
    public VRCTweenEase inTweenType = VRCTweenEase.OutElastic;
    public VRCTweenEase outTweenType = VRCTweenEase.InElastic;
    public TMP_Text _calloutTextField;
    public AudioSource _notificationSound;
    
    
    void Start()
    {
  
    }

    public void SetCalloutText(string callText, AudioClip sound, Vector3 targetPosition, Vector3 startPosition)
    {
        //Prep the things.
        targetObject = gameObject;
        startPos = startPosition;
        transform.localPosition = startPos;
        transform.localScale = Vector3.zero;

        //Set the things
        _calloutTextField.text = callText;
        _notificationSound.clip = sound;
        targetPos = targetPosition;
        
        //Start the things
        _positionTweenHandle = targetObject.TweenLocalPosition(targetPos, speed, inTweenType);
        _scaleTweenHandle = targetObject.TweenScale(Vector3.one, speed, inTweenType).OnComplete(this, nameof(OnTweened));
    }

    public void OnTweened()
    {
        SendCustomEventDelayedSeconds(nameof(ReturnTween), delay);
        PlayAudio();
    }

    public void ReturnTween()
    {
        _positionTweenHandle = targetObject.TweenLocalPosition(startPos, speed, outTweenType);
        _scaleTweenHandle = targetObject.TweenScale(Vector3.zero, speed, outTweenType).OnComplete(this, nameof(OnEvenMoreTweened));
    }
    
    public void OnEvenMoreTweened() //I use stupid names. Fight me.
    {
        Debug.Log("Destroying callout box.");
        Destroy(gameObject);
    }
    
    void OnDestroy()
    {
        // Clean up tweens when the object is destroyed.
        if (_positionTweenHandle != null)
            _positionTweenHandle.Kill();

        if (_scaleTweenHandle != null)
            _scaleTweenHandle.Kill();
    }
    
    public void PlayAudio()
    {
        if (Utilities.IsValid(_notificationSound))
        {
            _notificationSound.PlayOneShot(_notificationSound.clip);
        }
    }
}
