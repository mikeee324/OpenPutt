using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Random = UnityEngine.Random;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttUtils : UdonSharpBehaviour
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
            if (!Utilities.IsValid(context))
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
            if (!Utilities.IsValid(context))
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
            if (!Utilities.IsValid(context))
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
            if (!Utilities.IsValid(objectWithBoxCollider))
            {
                LogError(tag: "Utils", message: nameof(PlayerPresentInBoxCollider) + " tried to run but had no box collider.", tagColor: "red");
                return false;
            }

            var vrcPlayers = new VRCPlayerApi[100]; //This is to check if players are present.
            VRCPlayerApi.GetPlayers(vrcPlayers);
            foreach (var player in vrcPlayers)
            {
                if (!Utilities.IsValid(player)) continue;
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
        public static float GetUnixTimestamp() => (float)(DateTime.UtcNow - new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    public static class Extensions
    {
        /// <summary>
        /// Gets the OpenPutt PlayerManager for a particular player (Contains scores and other player-specific info)
        /// </summary>
        /// <returns>
        /// The PlayerManager for this player
        /// </returns>
        public static PlayerManager GetOpenPuttPlayerManager(this VRCPlayerApi player)
        {
            var objects = Networking.GetPlayerObjects(player);
            for (int i = 0; i < objects.Length; i++)
            {
                if (!Utilities.IsValid(objects[i])) continue;
                PlayerManager foundScript = objects[i].GetComponentInChildren<PlayerManager>();
                if (Utilities.IsValid(foundScript)) return foundScript;
            }
            return null;
        }

        /// <summary>
        /// Checks if a float is within the deadzone near 0
        /// </summary>
        /// <param name="f"></param>
        /// <param name="deadzone">The deadzone (Default: 0.05f)</param>
        /// <returns></returns>
        public static bool IsNearZero(this float f, float deadzone = .05f) => Mathf.Abs(f) <= Mathf.Abs(deadzone);

        public static bool IsNear(this float a, float b, float deadzone = .05f)
        {
            if (float.IsNaN(a) || float.IsNaN(b))
                return false;
            if (float.IsInfinity(a) || float.IsInfinity(b))
                return a == b;
            return Math.Abs(a - b) < deadzone;
        }

        public static bool LocalPlayerOwnsThisObject(this UdonSharpBehaviour behaviour) => behaviour.gameObject.LocalPlayerOwnsThisObject();
        public static bool LocalPlayerOwnsThisObject(this GameObject gameObject) => OpenPuttUtils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject);

        /// <summary>
        /// Returns a sanitized Vector3 with NaN and infinity values replaced with 0
        /// </summary>
        public static Vector3 Sanitized(this Vector3 vector)
        {
            return new Vector3(float.IsNaN(vector.x) || float.IsInfinity(vector.x) ? 0f : vector.x, float.IsNaN(vector.y) || float.IsInfinity(vector.y) ? 0f : vector.y, float.IsNaN(vector.z) || float.IsInfinity(vector.z) ? 0f : vector.z);
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns>A random entry from the array</returns>
        public static T GetRandom<T>(this T[] array) => array[Random.Range(0, array.Length)];

        public static Vector3 FixNaNs(Vector3 vector)
        {
            if (float.IsNaN(vector.x)) vector.x = 0;
            if (float.IsNaN(vector.y)) vector.y = 0;
            if (float.IsNaN(vector.z)) vector.z = 0;
            return vector;
        }

        public static float FixNans(float value)
        {
            if (float.IsNaN(value)) value = 0;
            if (float.IsInfinity(value)) value = 0;
            return value;
        }

        public static Vector3 GetDirectionTowards(this Transform start, Transform end, Vector3 gravityDirection, bool ignoreHeight)
        {
            return GetDirectionTowards(start.position, end.position, gravityDirection, ignoreHeight);
        }

        public static Vector3 GetDirectionTowards(this Vector3 start, Vector3 end, Vector3 gravityDirection, bool ignoreHeight)
        {
            if (ignoreHeight)
                return (end - start).FlattenDirection(gravityDirection);

            return (end - start).normalized;
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
            var length = array.Length;

            var newArray = new T[length];

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

        public static long GetUnixTimestamp(this DateTime dateTime)
        {
            var utcDateTime = dateTime.ToUniversalTime();
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (utcDateTime.Ticks - unixEpoch.Ticks) / TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// Flattens a direction vector at a 90 degree angle to the "up" vector. Used to flatten ball hit directions even when gravity is upside down etc
        /// </summary>
        /// <returns></returns>
        public static Vector3 FlattenDirection(this Vector3 inputDirection, Vector3 up) => (inputDirection - Vector3.Dot(inputDirection, up) * up).normalized;

        public static Vector3 BiasedDirection(this Vector3 direction1, Vector3 direction2, float bias)
        {
            // Ensure vectors are normalized
            if (direction1 == Vector3.zero || direction2 == Vector3.zero)
            {
                return Vector3.zero;
            }

            direction1.Normalize();
            direction2.Normalize();

            // Check if the vectors are nearly opposite
            var dot = Vector3.Dot(direction1, direction2);
            if (Mathf.Abs(dot + 1.0f) < 0.0001f)
            {
                // Handle nearly opposite vectors by returning one of them, based on the bias
                return bias < 0.5f ? direction1 : direction2;
            }

            // Compute the biased direction vector
            var biasedDirection = (direction1 * (1 - bias) + direction2 * bias);

            // Ensure the result is a valid direction vector
            if (biasedDirection == Vector3.zero)
                return Vector3.zero;

            return biasedDirection.normalized;
        }

        /// <summary>
        /// Returns a string name for this club type
        /// </summary>
        /// <param name="clubType"></param>
        /// <returns></returns>
        public static string GetName(this GolfClubType clubType)
        {
            switch (clubType)
            {
                case GolfClubType.Putter:
                    return "Putter";
                case GolfClubType.Driver:
                    return "Driver";
                case GolfClubType.Wood3:
                    return "Wood 3";
                case GolfClubType.Wood5:
                    return "Wood 5";
                case GolfClubType.Iron4:
                    return "Iron 4";
                case GolfClubType.Iron5:
                    return "Iron 5";
                case GolfClubType.Iron6:
                    return "Iron 6";
                case GolfClubType.Iron7:
                    return "Iron 7";
                case GolfClubType.Iron8:
                    return "Iron 8";
                case GolfClubType.Iron9:
                    return "Iron 9";
                case GolfClubType.PitchingWedge:
                    return "Pitching Wedge";
                case GolfClubType.GapWedge:
                    return "Gap Wedge";
                case GolfClubType.SandWedge:
                    return "Sand Wedge";
                case GolfClubType.LobWedge:
                    return "Lob Wedge";
                case GolfClubType.Hybrid:
                    return "Hybrid";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Gets the typical loft angle in degrees for a club type
        /// </summary>
        /// <param name="clubType">The golf club type</param>
        /// <returns>The typical loft angle in degrees</returns>
        public static float GetTypicalLoft(this GolfClubType clubType)
        {
            switch (clubType)
            {
                case GolfClubType.Driver: return 12.0f; // Can range from 8 to 12+
                case GolfClubType.Wood3: return 15.0f;
                case GolfClubType.Wood5: return 18.0f;
                case GolfClubType.Iron4: return 24.0f;
                case GolfClubType.Iron5: return 27.0f;
                case GolfClubType.Iron6: return 30.0f;
                case GolfClubType.Iron7: return 34.0f;
                case GolfClubType.Iron8: return 38.0f;
                case GolfClubType.Iron9: return 42.0f;

                case GolfClubType.PitchingWedge: return 46.0f; // PW
                case GolfClubType.GapWedge: return 50.0f;      // GW/AW
                case GolfClubType.SandWedge: return 56.0f;     // SW
                case GolfClubType.LobWedge: return 60.0f;      // LW (can be higher)

                case GolfClubType.Hybrid: return 20.0f; // Varies greatly depending on iron equivalent

                case GolfClubType.Putter: return 3.0f; // Putters have very low loft
                default: return 0.0f;
            }
        }

        /// <summary>
        /// Gets the typical maximum hit speed (m/s) for a club type.
        /// These values are gameplay-tuned and can be adjusted later or
        /// exposed as per-club overrides in `GolfClub`.
        /// </summary>
        public static float GetTypicalMaxSpeed(this GolfClubType clubType)
        {
            switch (clubType)
            {
                case GolfClubType.Driver: return 80f;
                case GolfClubType.Wood3: return 75f;
                case GolfClubType.Wood5: return 69f;
                case GolfClubType.Iron4: return 64f;
                case GolfClubType.Iron5: return 59f;
                case GolfClubType.Iron6: return 53f;
                case GolfClubType.Iron7: return 48f;
                case GolfClubType.Iron8: return 43f;
                case GolfClubType.Iron9: return 37f;
                case GolfClubType.PitchingWedge: return 32f;
                case GolfClubType.GapWedge: return 32f;
                case GolfClubType.SandWedge: return 27f;
                case GolfClubType.LobWedge: return 27f;
                case GolfClubType.Hybrid: return 59f;
                case GolfClubType.Putter: return 15f;
                default: return 80f;
            }
        }
    }
}