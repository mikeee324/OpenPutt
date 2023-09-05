#if UNITY_EDITOR
using Cyan.PlayerObjectPool;
using mikeee324.OpenPutt;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Components;

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
            Utils.LogWarning(TAG, "Could not find an OpenPutt prefab in the scene.. doing nothing");
            return;
        }

        SetupOpenPutt();

        PopulateBallStartLineRendererReferences();

        CheckReferences();
    }

    private void SetupOpenPutt()
    {
        if (openPutt.objectAssigner == null)
            openPutt.objectAssigner = GameObject.FindObjectOfType<CyanPlayerObjectAssigner>();

        if (openPutt.objectPool == null)
            openPutt.objectPool = GameObject.FindObjectOfType<CyanPlayerObjectPool>();

        // Populate all the player managers
        openPutt.allPlayerManagers = new PlayerManager[openPutt.MaxPlayerCount];
        for (int i = 0; i < openPutt.allPlayerManagers.Length; i++)
        {
            if (i < openPutt.objectAssigner.transform.childCount)
            {
                openPutt.allPlayerManagers[i] = openPutt.objectAssigner.transform.GetChild(i).GetComponent<PlayerManager>();
                openPutt.allPlayerManagers[i].openPutt = openPutt;
                openPutt.allPlayerManagers[i].golfClubHead.openPutt = openPutt;
            }
        }

        Utils.Log(TAG, $"SetupOpenPutt - Setup {openPutt.allPlayerManagers.Length} PlayerManagers");

        // Automatically assign course numbers based on their position in the array so we don't get mixed up
        for (int i = 0; i < openPutt.courses.Length; i++)
        {
            openPutt.courses[i].holeNumber = i;
            openPutt.courses[i].openPutt = openPutt;
        }

        VRCSceneDescriptor sceneDescriptor = GameObject.FindObjectOfType<VRCSceneDescriptor>();
        if (sceneDescriptor != null)
        {
            foreach (Scoreboard scoreboard in openPutt.scoreboardManager.scoreboards)
                scoreboard.myCanvas.worldCamera = sceneDescriptor.ReferenceCamera.GetComponent<Camera>();

            foreach (Scoreboard scoreboard in openPutt.scoreboardManager.staticScoreboards)
                scoreboard.myCanvas.worldCamera = sceneDescriptor.ReferenceCamera.GetComponent<Camera>();
        }

        Utils.Log(TAG, $"SetupOpenPutt - Setup {openPutt.courses.Length} courses");
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

        Utils.Log(TAG, $"PopulateBallStartLineRendererReferences - Found {courseStartPositions.Length} ball start positions. Assigning them to {ballLineRenderers.Length} ball start line renderers");

        for (int i = 0; i < ballLineRenderers.Length; i++)
        {
            ballLineRenderers[i].knownStartPositions = courseStartPositions;
            ballLineRenderers[i].knownStartColliders = courseStartColliders;
        }
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
                Utils.LogError(startPosition, GetGameObjectPath(startPosition.gameObject) + " - Missing CourseManager Reference!");
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