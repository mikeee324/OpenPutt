﻿using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Utils : UdonSharpBehaviour
    {

        public static void Log(string tag, string message, string tagColor = "green")
        {
#if UNITY_STANDALONE_WIN
            Debug.Log($"[<color={tagColor}>{tag}</color>] {message}");
#elif UNITY_EDITOR
            Debug.Log($"[<color={tagColor}>{tag}</color>] {message}");
#endif
        }

        public static void LogWarning(string tag, string message, string tagColor = "green")
        {
#if UNITY_STANDALONE_WIN
            Debug.LogWarning($"[<color={tagColor}>{tag}</color>] {message}");
#elif UNITY_EDITOR
            Debug.LogWarning($"[<color={tagColor}>{tag}</color>] {message}");
#endif
        }

        public static void LogError(string tag, string message, string tagColor = "green")
        {
#if UNITY_STANDALONE_WIN
            Debug.LogError($"[<color={tagColor}>{tag}</color>] {message}");
#elif UNITY_EDITOR
            Debug.LogError($"[<color={tagColor}>{tag}</color>] {message}");
#endif
        }

        public static void Log(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

#if UNITY_STANDALONE_WIN
            Debug.Log($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#elif UNITY_EDITOR
            Debug.Log($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#endif
        }

        public static void LogWarning(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

#if UNITY_STANDALONE_WIN
            Debug.LogWarning($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#elif UNITY_EDITOR
            Debug.LogWarning($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#endif
        }

        public static void LogError(UdonSharpBehaviour context, string message, string tagColor = "green")
        {
            if (context == null)
            {
                Debug.LogError("Log context is missing!");
                return;
            }

#if UNITY_STANDALONE_WIN
            Debug.LogError($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#elif UNITY_EDITOR
            Debug.LogError($"[{context.gameObject.name} (<color={tagColor}>{context.GetUdonTypeName()}</color>)] {message}", context.gameObject);
#endif
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

        /// <summary>
        /// Works out the closest position on a line relative to a position in the world. Useful for making an object follow a particular path.
        /// </summary>
        /// <param name="lineStartPosition"></param>
        /// <param name="lineEndPosition"></param>
        /// <param name="worldPosition"></param>
        /// <returns>A position in world space that represents the closest point on the line</returns>
        public static Vector3 ClosestPointOnLine(Vector3 lineStartPosition, Vector3 lineEndPosition, Vector3 worldPosition)
        {
            var vVector1 = worldPosition - lineStartPosition;
            var vVector2 = (lineEndPosition - lineStartPosition).normalized;

            var d = Vector3.Distance(lineStartPosition, lineEndPosition);
            var t = Vector3.Dot(vVector2, vVector1);

            if (t <= 0)
                return lineStartPosition;

            if (t >= d)
                return lineEndPosition;

            var vVector3 = vVector2 * t;

            return lineStartPosition + vVector3;
        }

        /// <summary>
        /// Shortcut for Utilities.IsValid(Networking.LocalPlayer);
        /// </summary>
        /// <returns>True if the local player is valid</returns>
        public static bool LocalPlayerIsValid() => Utilities.IsValid(Networking.LocalPlayer);

        /// <summary>
        /// Gets a UNIX-like timestamp but starts at 2023-01-01 00:00:00 UTC so float precision is better (maybe?)
        /// </summary>
        /// <returns>Number of seconds since 2023-01-01 00:00:00 UTC</returns>
        public static float GetUnixTimestamp() => (float)(System.DateTime.UtcNow - new System.DateTime(2023, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
    }

    public static class Extensions
    {
        public static bool LocalPlayerOwnsThisObject(this UdonSharpBehaviour behaviour) => behaviour.gameObject.LocalPlayerOwnsThisObject();
        public static bool LocalPlayerOwnsThisObject(this GameObject gameObject) => Utils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject);

        public static Vector2 xz(this Vector3 vv)
        {
            return new Vector2(vv.x, vv.z);
        }

        public static Vector3 RemoveHeight(this Vector3 vv)
        {
            return new Vector3(vv.x, 0, vv.z);
        }

        public static Vector3 GetDirectionTowards(this Vector3 start, Vector3 end, bool ignoreHeight)
        {
            if (ignoreHeight)
                return (end.RemoveHeight() - start.RemoveHeight()).normalized;
            else
                return (end - start).normalized;
        }

        [RecursiveMethod]
        public static ScoreboardPositioner[] SortByDistance(this ScoreboardPositioner[] array, Vector3 position, int leftIndex = 0, int rightIndex = -1)
        {
            if (array.Length == 0) return array;
            if (rightIndex == -1)
                rightIndex = array.Length - 1;

            var i = leftIndex;
            var j = rightIndex;
            var pivot = Vector3.Distance(array[leftIndex].transform.position, position);
            while (i <= j)
            {
                while (Vector3.Distance(array[i].transform.position, position) < pivot)
                {
                    i++;
                }

                while (Vector3.Distance(array[j].transform.position, position) > pivot)
                {
                    j--;
                }
                if (i <= j)
                {
                    ScoreboardPositioner temp = array[i];
                    array[i] = array[j];
                    array[j] = temp;
                    i++;
                    j--;
                }
            }

            if (leftIndex < j)
                array.SortByDistance(position, leftIndex, j);
            if (i < rightIndex)
                array.SortByDistance(position, i, rightIndex);

            return array;
        }

        /// <summary>
        /// Pushes an element into an array and pops an element off the other side of the array (For queues/stacks)
        /// <para>
        /// Based on: <see href="https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.insert?view=net-6.0">List&lt;T&gt;.Insert(Int32, T)</see>
        /// </para>
        /// </summary>
        /// <returns>Modified T[]</returns>
        /// <typeparam name="T">The type of elements in the array.</typeparam>
        /// <param name="array">Source T[] to modify.</param>
        /// <param name="item">The object to insert.</param>
        /// <param name="atStart">True=push onto index 0, false=push onto end of array</param>
        public static T[] Push<T>(this T[] array, T item, bool atStart = true)
        {
            int length = array.Length;

            T[] newArray = new T[length];

            newArray.SetValue(item, atStart ? 0 : array.Length - 1);

            if (atStart)
            {
                Array.Copy(array, 0, newArray, 1, length - 1);
            }
            else
            {
                Array.Copy(array, 1, newArray, 0, length - 1);
            }

            return newArray;
        }
    }
}