#if UNITY_EDITOR
using dev.mikeee324.OpenPutt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Varneon.VUdon.ArrayExtensions;

public class OpenPuttBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder { get { return 0; } }

    private string TAG = "OpenPuttBuildProcessor";

    OpenPutt openPutt;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        openPutt = GameObject.FindObjectOfType<OpenPutt>();

        if (openPutt == null)
        {
            OpenPuttUtils.LogWarning(TAG, "Could not find an OpenPutt prefab in the scene.. doing nothing");
            return;
        }

        SetupOpenPutt();

        PopulateBallStartLineRendererReferences();

        AssignOpenPuttReferences();

        AssignEventListenerReferences();

        CheckReferences();
    }

    private void SetupOpenPutt()
    {
        // Automatically assign course numbers based on their position in the array so we don't get mixed up
        for (int i = 0; i < openPutt.courses.Length; i++)
        {
            openPutt.courses[i].holeNumber = i;
            openPutt.courses[i].openPutt = openPutt;
        }

        OpenPuttUtils.Log(TAG, $"SetupOpenPutt - Setup {openPutt.courses.Length} courses");
    }

    private void PopulateBallStartLineRendererReferences()
    {
        CourseStartPosition[] courseStartPositions = new CourseStartPosition[0];
        Collider[] courseStartColliders = new Collider[0];

        for (int courseID = 0; courseID < openPutt.courses.Length; courseID++)
        {
            CourseManager course = openPutt.courses[courseID];
            courseStartPositions = courseStartPositions.AddRange(course.ballSpawns);
            for (int spawnID = 0; spawnID < course.ballSpawns.Length; spawnID++)
                courseStartColliders = courseStartColliders.Add(course.ballSpawns[spawnID] != null ? course.ballSpawns[spawnID].myCollider : null);
        }

        GolfBallStartLineController[] ballLineRenderers = openPutt.GetComponentsInChildren<GolfBallStartLineController>(true);

        OpenPuttUtils.Log(TAG, $"PopulateBallStartLineRendererReferences - Found {courseStartPositions.Length} ball start positions. Assigning them to {ballLineRenderers.Length} ball start line renderers");

        for (int i = 0; i < ballLineRenderers.Length; i++)
        {
            ballLineRenderers[i].knownStartPositions = courseStartPositions;
            ballLineRenderers[i].knownStartColliders = courseStartColliders;
        }
    }

    /// <summary>
    /// Scans every MonoBehaviour in the scene for a field of type OpenPutt (public or private, at any level of
    /// its class hierarchy) and assigns it if it was left unset. This means any component that wants a reference
    /// to OpenPutt just needs a field of that type - it doesn't need to be wired up manually or hooked into this
    /// build processor.
    /// </summary>
    private void AssignOpenPuttReferences()
    {
        const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        MonoBehaviour[] allBehaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int assignedCount = 0;

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null || behaviour == openPutt)
                continue;

            for (Type type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(fieldFlags))
                {
                    if (field.FieldType != typeof(OpenPutt) || field.GetValue(behaviour) != null)
                        continue;

                    field.SetValue(behaviour, openPutt);
                    assignedCount++;
                    OpenPuttUtils.Log(TAG, behaviour.gameObject, $"AssignOpenPuttReferences - Assigned OpenPutt reference on {GetGameObjectPath(behaviour.gameObject)} ({type.Name}.{field.Name})");
                }
            }
        }

        if (assignedCount > 0)
            OpenPuttUtils.LogWarning(TAG, $"AssignOpenPuttReferences - Automatically assigned the OpenPutt reference on {assignedCount} field(s) because they were left empty. Please assign this manually where possible!");
    }

    /// <summary>
    /// Finds every OpenPuttEventListener in the scene and makes sure it's registered in OpenPutt's eventListeners array
    /// </summary>
    private void AssignEventListenerReferences()
    {
        OpenPuttEventListener[] listeners = GameObject.FindObjectsByType<OpenPuttEventListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        List<OpenPuttEventListener> registeredListeners = openPutt.eventListeners?.ToList() ?? new List<OpenPuttEventListener>();

        int registeredCount = 0;

        foreach (OpenPuttEventListener listener in listeners)
        {
            if (!registeredListeners.Contains(listener))
            {
                registeredListeners.Add(listener);
                registeredCount++;
                OpenPuttUtils.Log(TAG, $"AssignEventListenerReferences - Registered {GetGameObjectPath(listener.gameObject)} in OpenPutt's eventListeners");
            }
        }

        openPutt.eventListeners = registeredListeners.ToArray();

        if (registeredCount > 0)
            OpenPuttUtils.LogWarning(TAG, $"AssignEventListenerReferences - Automatically registered {registeredCount} event listener(s) in OpenPutt's eventListeners array because they were missing. Please assign this manually where possible!");
    }

    private void CheckReferences()
    {
        bool missingStuff = false;

        CourseStartPosition[] startPositions = GameObject.FindObjectsOfType<CourseStartPosition>();
        foreach (CourseStartPosition startPosition in startPositions)
        {
            if (startPosition.gameObject.activeInHierarchy && startPosition.courseManager == null)
            {
                missingStuff = true;
                OpenPuttUtils.LogError(startPosition, GetGameObjectPath(startPosition.gameObject) + " - Missing CourseManager Reference!");
            }
        }

        if (missingStuff)
            throw new BuildFailedException("Build failed! Please check logs to check for things that need fixing.");
    }

    /// <summary>
    /// Spits out the full path of a GameObject in the scene
    /// </summary>
    /// <param name="obj">The GameObject to get the full path for</param>
    /// <returns> The full path of the GameObject</returns>
    public static string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = string.Format("/{0}{1}", obj.name, path);
        }
        return path;
    }
}
#endif