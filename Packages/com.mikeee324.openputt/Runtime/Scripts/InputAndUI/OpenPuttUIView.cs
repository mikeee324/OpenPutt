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
        [OpenPuttDescription("Holds the buttons, score text and power bar for one platform's UI (desktop, mobile, or VR). Assign one of these per UI variant, wiring up each element to the matching object in that variant's Canvas.")]
        [OpenPuttFoldoutGroup("References")]
        public OpenPuttUIController uiController;

        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI courseName;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI parScore;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI relativeScore;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI totalScore;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI currentClub;
        [OpenPuttFoldoutGroup("References")]
        public GameObject strokePowerRoot;
        [OpenPuttFoldoutGroup("References")]
        public Transform strokePowerIndicator;
        [OpenPuttFoldoutGroup("References")]
        public Gradient powerGradient;
        [OpenPuttFoldoutGroup("References")]
        public GameObject fetchBallButton;
        [OpenPuttFoldoutGroup("References")]
        public GameObject toggleCameraButton;
        [OpenPuttFoldoutGroup("References")]
        public GameObject shootButton;
        [OpenPuttFoldoutGroup("References")]
        public GameObject cycleClubNextButton;
        [OpenPuttFoldoutGroup("References")]
        public GameObject cycleClubPreviousButton;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI cycleClubNextText;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI cycleClubPreviousText;
        [OpenPuttFoldoutGroup("References")]
        public GameObject leftShoulderButton;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI leftShoulderText;
        [OpenPuttFoldoutGroup("References")]
        public GameObject rightShoulderButton;
        [OpenPuttFoldoutGroup("References")]
        public TextMeshProUGUI rightShoulderText;

        [OpenPuttFoldoutGroup("References")]
        public Image leftShoulderClubIcon;
        [OpenPuttFoldoutGroup("References")]
        public Image rightShoulderClubIcon;
        [OpenPuttFoldoutGroup("References")]
        public Image leftShoulderBallIcon;
        [OpenPuttFoldoutGroup("References")]
        public Image rightShoulderBallIcon;

        [OpenPuttFoldoutGroup("References")]
        public GameObject portableMenuButton;

        [OpenPuttFoldoutGroup("References")]
        public GameObject fetchBallPanel;

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