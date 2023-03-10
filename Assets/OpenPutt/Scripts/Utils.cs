using System;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Utils : UdonSharpBehaviour
    {

        public static void Log(string tag, string message, string tagColor = "green")
        {
            Debug.Log($"[<color={tagColor}>{tag}</color>] {message}");
        }

        public static void LogWarning(string tag, string message, string tagColor = "green")
        {
            Debug.LogWarning($"[<color={tagColor}>{tag}</color>] {message}");
        }

        public static void LogError(string tag, string message, string tagColor = "green")
        {
            Debug.LogError($"[<color={tagColor}>{tag}</color>] {message}");
        }

        public static void Log(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

            Debug.Log($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}");
        }

        public static void LogWarning(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

            Debug.LogWarning($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}");
        }
        public static void LogError(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

            Debug.LogError($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}");
        }

        /// <summary>
        /// Checks if the player isn't already the owner of the object, if not it applies ownership of the object
        /// </summary>
        /// <param name="player">The player who wants ownership</param>
        /// <param name="nowOwns">The object to set ownership on</param>
        public static void SetOwner(VRCPlayerApi player, GameObject nowOwns)
        {
            if (!Networking.IsOwner(player, nowOwns))
                Networking.SetOwner(player, nowOwns);
        }

        /// <summary>
        /// This will check if there are any players in the selected objects box collider.
        /// </summary>
        /// <param name="objectWithBoxCollider">This is the gameObject that you wanna check. It needs a box collider.</param>
        /// <returns>true if a person is in it. False if there is no box collider or no person in it.</returns>
        public static bool PlayerPresentInBoxCollider(GameObject objectWithBoxCollider)
        {
            if (objectWithBoxCollider == null)
            {
                LogError(tag: "Utils", message: nameof(PlayerPresentInBoxCollider) + " tried to run but had no box collider.", tagColor: "red");
                return false;
            }
            VRCPlayerApi[] vrcPlayers = new VRCPlayerApi[100]; //This is to check if players are present.
            VRCPlayerApi.GetPlayers(vrcPlayers);
            foreach (VRCPlayerApi player in vrcPlayers)
            {
                if (player == null) continue;
                if (objectWithBoxCollider.GetComponent<BoxCollider>().bounds.Contains(player.GetPosition()))
                {
                    return true;
                }
            }
            return false;
        }

        public static float GetUnixTimestamp()
        {
            System.DateTime offsetDateTime = new System.DateTime(2022, 6, 13, 0, 0, 0, System.DateTimeKind.Utc);
            return (float)(System.DateTime.UtcNow - offsetDateTime).TotalSeconds;
        }
        public static Vector3 ClosestPointOnLine(Vector3 vA, Vector3 vB, Vector3 vPoint)
        {
            var vVector1 = vPoint - vA;
            var vVector2 = (vB - vA).normalized;

            var d = Vector3.Distance(vA, vB);
            var t = Vector3.Dot(vVector2, vVector1);

            if (t <= 0)
                return vA;

            if (t >= d)
                return vB;

            var vVector3 = vVector2 * t;

            var vClosestPoint = vA + vVector3;

            return vClosestPoint;
        }

        public static bool LocalPlayerIsValid()
        {
            return Networking.LocalPlayer != null && Networking.LocalPlayer.IsValid();
        }
    }

    public static class Extensions
    {
        public static T[] Prepend<T>(this T[] array, T item)
        {
            T[] newArray = new T[array.Length + 1];
            newArray[0] = item;
            Array.Copy(array, 0, newArray, 1, array.Length);
            return newArray;
        }

        public static T[] Add<T>(this T[] array, T item)
        {
            T[] newArray = new T[array.Length + 1];
            newArray[array.Length] = item;
            Array.Copy(array, newArray, array.Length);
            return newArray;
        }

        public static T[] Remove<T>(this T[] array, T item)
        {
            int index = Array.IndexOf(array, item);
            if (index == -1)
                return array;

            T[] newArray = new T[array.Length - 1];
            Array.Copy(array, newArray, index);
            Array.Copy(array, index + 1, newArray, index, array.Length - index - 1);
            return newArray;
        }

        public static bool Contains<T>(this T[] array, T item) => Array.IndexOf(array, item) != -1;
    }
}