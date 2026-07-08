using dev.mikeee324.OpenPutt;
using UdonSharp;
using Unity.XR.Oculus;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDK3.Rendering;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

public class OpenPuttNotifications : OpenPuttEventListener
{
    public Canvas _desktopCanvas;
    public Canvas _vrCanvas;
    public HeadFollower _headFollower;
    [Range(1,20)]public float _vrSmoothing = 10f;
    public bool _notificationToggle = true;
    public bool _othersNotificationsToggle = true;
    public bool _playerIsInVR = true;
    public GameObject _calloutBoxPrefab;
    public Vector3 _vrTargetPos, _desktopTargetPos, _vrStartPos, _desktopStartPos;
    private AudioClip _notificationSound;
    public AudioClip aceSound, condorSound, albatrossSound, eagleSound, birdieSound, parSound, bogeySound, doubleBogeySound, tripleBogeySound, strokeLimitSound;
    public OpenPutt openPutt;
    
    void Start()
    {
        if (!Utilities.IsValid(openPutt))
            return;

        // If this object isn't already registered as an event listener, register it here automatically
        openPutt._RegisterEventListener(this);
    }

    public override void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar,
        int totalHits)
    {

        if (totalHits == 0)
        {
            Debug.LogError($"The player  {player.displayName} finished {course} with no hits. This isn't good...");
            return;
        }
        
        if (totalHits == 1)
        {
            Callout(Callouts.Ace, player.playerId);
            return;
        }
        
        switch (scoreRelativeToPar)
        {
            case -4:
                Callout(Callouts.Condor, player.playerId);
                break;
            case -3:
                Callout(Callouts.Albatross, player.playerId);
                break;
            case -2:
                Callout(Callouts.Eagle, player.playerId);
                break;
            case -1:
                Callout(Callouts.Birdie, player.playerId);
                break;
            case 0:
                Callout(Callouts.Par, player.playerId);
                break;
            case 1:
                Callout(Callouts.Bogey, player.playerId);
                break;
            case 2:
                Callout(Callouts.DoubleBogey, player.playerId);
                break;
        }
    }
    
    public override void OnPlayerHitCourseMaxScore(VRCPlayerApi player, CourseManager course)
    {
        Callout(Callouts.StrokeLimit,  Networking.LocalPlayer.playerId);
    }


    //This is just to check if the player is in vr once.
    public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
    {
        if(player != Networking.LocalPlayer) return;
        _playerIsInVR = player.IsUserInVR();
        
        if (_playerIsInVR)
        {
            // _vrCanvas.gameObject.SetActive(true);
            // _desktopCanvas.gameObject.SetActive(false);
            _headFollower._lerpSpeed = _vrSmoothing;
        }
        else
        {
            _headFollower._lerpSpeed = 1000;
            // _vrCanvas.gameObject.SetActive(false);
            // _desktopCanvas.gameObject.SetActive(true);
            // _headFollower.enabled = false;
            // _headFollower.transform.localPosition = Vector3.zero;
        }
    }

    public void SendCallout(Callouts callout)
    {
        //You can't pass a playerobject through networking. So we recreate it after the network.
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Callout), (int)callout, Networking.LocalPlayer.playerId);
    }

    [NetworkCallable]
    public void Callout(Callouts callout, int playerId)
    {
        VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
        if (player == null) return;
        
        if (_notificationToggle == false) return;
        if (_othersNotificationsToggle == false && player != Networking.LocalPlayer) return;
		
        string calloutText = "This is an error!";

        switch (callout)
        {
            case Callouts.Ace:
                _notificationSound = aceSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "ACE!";
                }
                else
                {
                    calloutText = $"{player.displayName} scored an ACE!";
                }
                break;
            case Callouts.Condor:
                _notificationSound = condorSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Condor!";
						
                }
                else
                {
                    calloutText = $"{player.displayName} scored a Condor!";
                }

                break;
            case Callouts.Albatross:
                _notificationSound = albatrossSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Albatross!";
                }
                else
                {
                    calloutText = $"{player.displayName} scored an Albatross!";
                }

                break;
            case Callouts.Eagle:
                _notificationSound = eagleSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Eagle!";}
                else
                {
                    calloutText = $"{player.displayName} scored an Eagle!";
                }

                break;
            case Callouts.Birdie:
                _notificationSound = birdieSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Birdie!";}
                else
                {
                    calloutText = $"{player.displayName} scored a Birdie!";
                }

                break;
            case Callouts.Par:
                _notificationSound = parSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Par!";}
                else
                {
                    calloutText = $"{player.displayName} scored a Par!";
                }

                break;
            case Callouts.Bogey:
                _notificationSound = bogeySound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Bogey!";}
                else
                {
                    calloutText = $"{player.displayName} scored a Bogey.";
                }

                break;
            case Callouts.DoubleBogey:
                _notificationSound = doubleBogeySound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Double Bogey!";}
                else
                {
                    calloutText = $"{player.displayName} scored a Double Bogey.";
                }

                break;
            case Callouts.TripleBogey:
                _notificationSound = tripleBogeySound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Triple Bogey!";}
                else
                {
                    calloutText = $"{player.displayName} scored a Triple Bogey.";
                }

                break;
            case Callouts.StrokeLimit:
                _notificationSound = strokeLimitSound;
                if (player == Networking.LocalPlayer)
                {
                    calloutText = "Stroke Limit!";}
                else
                {
                    calloutText = $"{player.displayName} hit the Stroke Limit.";
                }

                break;
        }
        
        InstantiateCalloutBox(calloutText, _notificationSound);
        
    }

    public void InstantiateCalloutBox(string callText, AudioClip callAudio = null)
    {
        GameObject lastCallout = Instantiate(_calloutBoxPrefab, _playerIsInVR ? _vrCanvas.transform : _desktopCanvas.transform);
        var calloutScript = lastCallout.GetComponent<CalloutBox>();
		
        calloutScript.targetPos = _playerIsInVR ? _vrTargetPos : _desktopTargetPos;
        calloutScript.SetCalloutText(callText, callAudio
            , targetPosition: _playerIsInVR ? _vrTargetPos : _desktopTargetPos
            , startPosition: _playerIsInVR ? _vrStartPos : _desktopStartPos);
    }
	
	
    public Vector2 GetScreenResolution() //I don't think this is needed.
    {
		
        float width = VRCCameraSettings.ScreenCamera.PixelWidth;
        float height = VRCCameraSettings.ScreenCamera.PixelHeight;

        return new Vector2(width, height);
    }


    public void SendTestNotification()
    {
        //You can't pass a playerobject through networking. So we recreate it after the network.
        // SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Callout), (int)testCallout, Networking.LocalPlayer.playerId);
        int randomIndex = Random.Range(0, (int)Callouts.StrokeLimit + 1);
        Callouts randomResult = (Callouts)randomIndex;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Callout), (int)randomResult, Networking.LocalPlayer.playerId);
    }
}


public enum Callouts
{
    Ace,
    Condor,
    Albatross,
    Eagle,
    Birdie,
    Par,
    Bogey,
    DoubleBogey,
    TripleBogey,
    StrokeLimit
}