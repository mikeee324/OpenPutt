#if UNITY_EDITOR
using Cyan.PlayerObjectPool;
using mikeee324.OpenPutt;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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
            Utils.LogWarning(TAG, "Could not find an OpenPutt prefab in the scene.. doing nothing");
            return;
        }

        SetupOpenPutt();

        PopulateBallStartLineRendererReferences();
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
}
#endif