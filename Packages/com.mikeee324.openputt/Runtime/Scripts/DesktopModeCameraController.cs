
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(99)]
    public class DesktopModeCameraController : UdonSharpBehaviour
    {
        #region References
        [Header("References")]
        public OpenPutt openPutt;
        public DesktopModeController desktopModeController;

        // The target we are following
        public Rigidbody target;
        #endregion

        #region Public Settings
        [Header("Settings")]
        // The distance in the x-z plane to the target
        public float distance = 10.0f;
        // the height we want the camera to be above the target
        public float minHeight = 0.2f;
        public float maxHeight = 2f;
        // How much we 
        public float heightDamping = 2.0f;
        public float rotationDamping = 1.0f;

        [Range(0, 5), Tooltip("How fast the camera moves horizontally")]
        public float cameraXSpeed = 1.5f;
        [Range(0, 5), Tooltip("How fast the camera moves vertically")]
        public float cameraYSpeed = .5f;
        #endregion

        #region Properties
        public bool LockCamera
        {
            get => cameraInputLocked;
            set => cameraInputLocked = value;
        }
        #endregion

        #region Private Vars
        private float currentXAngle = 0;
        private float currentCameraHeight = -1;

        private bool cameraInputLocked = false;
        #endregion

        private void OnEnable()
        {
            if (target == null)
                return;

            var currentRotation = Quaternion.Euler(0, currentXAngle, 0);
            transform.position = target.position;
            transform.position -= currentRotation * Vector3.forward * distance;

            if (currentCameraHeight == -1)
                currentCameraHeight = (maxHeight - minHeight) * 0.5f;

            // Set the height of the camera
            float wantedHeight = target.position.y + currentCameraHeight;
            transform.position = new Vector3(transform.position.x, wantedHeight, transform.position.z);
            transform.LookAt(target.transform);
        }

        void Start()
        {

        }

        void LateUpdate()
        {
            // Early out if we don't have a target
            if (!target) return;

            if (!cameraInputLocked)
            {
                float h = cameraXSpeed * Input.GetAxis("Mouse X");
                float v = cameraYSpeed * Input.GetAxis("Mouse Y");

                currentXAngle += h;
                currentCameraHeight -= v;
                currentCameraHeight = Mathf.Clamp(currentCameraHeight, minHeight, 3f);
            }

            // Calculate the current rotation angles
            float wantedRotationAngle = currentXAngle;
            float wantedHeight = target.position.y + currentCameraHeight;

            float currentRotationAngle = transform.eulerAngles.y;
            float currentHeight = transform.position.y;

            // Damp the rotation around the y-axis
            if (rotationDamping > 0)
                currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime);
            else
                currentRotationAngle = wantedRotationAngle;

            // Damp the height
            currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);

            // Convert the angle into a rotation
            var currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);

            // Set the position of the camera on the x-z plane to:
            // distance meters behind the target
            transform.position = target.position;
            transform.position -= currentRotation * Vector3.forward * distance;

            // Set the height of the camera
            transform.position = new Vector3(transform.position.x, currentHeight, transform.position.z);

            // Always look at the target
            transform.LookAt(target.transform);
        }
    }
}