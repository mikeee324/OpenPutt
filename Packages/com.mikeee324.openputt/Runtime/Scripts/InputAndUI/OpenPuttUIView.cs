using UdonSharp;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttUIView : UdonSharpBehaviour
    {
        public OpenPuttUIController uiController;

        public TextMeshProUGUI courseName;
        public TextMeshProUGUI parScore;
        public TextMeshProUGUI relativeScore;
        public TextMeshProUGUI totalScore;
        public TextMeshProUGUI currentClub;
        public GameObject strokePowerRoot;
        public Transform strokePowerIndicator;
        public Gradient powerGradient;
        public GameObject fetchBallButton;
        public GameObject toggleCameraButton;
        public GameObject shootButton;
        public GameObject cycleClubNextButton;
        public GameObject cycleClubPreviousButton;
        public TextMeshProUGUI cycleClubNextText;
        public TextMeshProUGUI cycleClubPreviousText;
        public GameObject leftShoulderButton;
        public TextMeshProUGUI leftShoulderText;
        public GameObject rightShoulderButton;
        public TextMeshProUGUI rightShoulderText;

        public Image leftShoulderClubIcon;
        public Image rightShoulderClubIcon;
        public Image leftShoulderBallIcon;
        public Image rightShoulderBallIcon;

        public GameObject portableMenuButton;

        public bool isShooting = false;
        private float targetPower = 0f;
        [Tooltip("Speed at which the power indicator scales smoothly")]
        public float powerLerpSpeed = 5f;

        void Start()
        {
        }

        void Update()
        {
            if (Utilities.IsValid(strokePowerIndicator) && (strokePowerRoot.activeSelf || isShooting))
            {
                float currentX = strokePowerIndicator.localScale.x;
                float newX = Mathf.Lerp(currentX, targetPower, Time.deltaTime * powerLerpSpeed);
                strokePowerIndicator.localScale = new Vector3(newX, 1f, 1f);

                bool newState = isShooting || newX > 0.1f;
                if (newState != strokePowerRoot.activeSelf)
                    strokePowerRoot.SetActive(newState);

                var image = strokePowerIndicator.GetComponent<Image>();
                if (Utilities.IsValid(image) && targetPower >= 0f)
                    image.color = powerGradient.Evaluate(newX);
            }
        }

        public void OnFetchBallPressed()
        {
            if (Utilities.IsValid(uiController)) uiController.OnFetchBallPressed();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void OnToggleCameraPressed()
        {
            if (Utilities.IsValid(uiController)) uiController.OnToggleCameraPressed();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void OnShootStart()
        {
            targetPower = 0;
            if (Utilities.IsValid(strokePowerIndicator))
                strokePowerIndicator.localScale = new Vector3(targetPower, 1f, 1f);
            if (Utilities.IsValid(uiController)) uiController.OnShootStart();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void OnShootEnd()
        {
            if (Utilities.IsValid(uiController)) uiController.OnShootEnd();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void OnCycleClubNext()
        {
            if (Utilities.IsValid(uiController)) uiController.OnCycleClubNext();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void OnCycleClubPrevious()
        {
            if (Utilities.IsValid(uiController)) uiController.OnCycleClubPrevious();
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void SetUIVisible(bool visible)
        {
            if (Utilities.IsValid(uiController)) uiController.SetUIVisible(visible);
            else Debug.LogWarning("OpenPuttUIView: uiController is not assigned.");
        }

        public void SetPowerIndicator(float power)
        {
            targetPower = power;
        }

        public void UpdateClubDisplay(GolfClubType clubType)
        {
            if (Utilities.IsValid(currentClub))
            {
                currentClub.text = clubType.GetName();
            }
        }
    }
}