using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sizes the navmesh agent the monster bakes with to the monster, and rebakes the Game scene.
///
/// <para>The Humanoid agent was baked at radius 0.4 and height 2. A 0.4 radius eroded from both
/// jambs of a doorway about as wide as its leaf leaves a strip one or two voxels across, and on
/// most doors it did not survive: the logs showed two dozen doorways cut out of the navmesh,
/// held together only by the doors' fallback links. The monster's capsule is 0.37 wide and 1.7
/// tall, so 0.3 and 1.8 still keep it off the walls while leaving the doorways a real strip.</para>
///
/// <para>Goes through the same serialized settings the Navigation window edits, so the editor
/// saves them itself. Writing ProjectSettings/NavMeshAreas.asset on disk while the editor is open
/// would be ignored until a restart and then overwritten.</para>
///
/// <para>Runs once by itself, on the first reload where the agent still has the old values. The
/// menu item re-runs it.</para>
/// </summary>
[InitializeOnLoad]
public static class NavMeshAgentSetup
{
    private const float OldRadius = 0.4f;
    private const float OldHeight = 2f;

    private const float Radius = 0.3f;
    private const float Height = 1.8f;

    private const string GameScenePath = "Assets/Scenes/Game.unity";

    // A rebake that has to wait for the surfaces to finish is not worth giving up on quickly, but
    // it must not keep the scene open forever either.
    private const double BakeTimeoutSeconds = 300;

    private const string MenuPath = "Tools/TCC/NavMesh/Aplicar agente do monstro (raio 0.3, altura 1.8) e rebake";

    static NavMeshAgentSetup()
    {
        EditorApplication.delayCall += RunOnceIfStillOnOldValues;
    }

    [MenuItem(MenuPath)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("NavMeshAgentSetup: saia do Play Mode antes de aplicar o agente e refazer o bake.");
            return;
        }

        ApplyAgentSettings();
        RebakeGameScene();
    }

    private static void RunOnceIfStillOnOldValues()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        SerializedProperty agent = FindHumanoidAgent(out _);
        if (agent == null) return;

        float radius = agent.FindPropertyRelative("agentRadius").floatValue;
        float height = agent.FindPropertyRelative("agentHeight").floatValue;

        // Anything but the exact old pair is a choice someone made — leave it alone.
        if (!Mathf.Approximately(radius, OldRadius) || !Mathf.Approximately(height, OldHeight)) return;

        Run();
    }

    private static void ApplyAgentSettings()
    {
        SerializedProperty agent = FindHumanoidAgent(out SerializedObject settings);

        if (agent == null)
        {
            Debug.LogError("NavMeshAgentSetup: agente de NavMesh com id 0 (Humanoid) não encontrado nas configurações do projeto.");
            return;
        }

        agent.FindPropertyRelative("agentRadius").floatValue = Radius;
        agent.FindPropertyRelative("agentHeight").floatValue = Height;

        settings.ApplyModifiedPropertiesWithoutUndo();

        // Only these settings: SaveAssets would also write whatever else the user has unsaved.
        AssetDatabase.SaveAssetIfDirty(settings.targetObject);

        Debug.Log($"NavMeshAgentSetup: agente Humanoid ajustado para raio {Radius} e altura {Height}.");
    }

    private static SerializedProperty FindHumanoidAgent(out SerializedObject settings)
    {
        settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));

        SerializedProperty agents = settings.FindProperty("m_Settings");
        if (agents == null) return null;

        for (int i = 0; i < agents.arraySize; i++)
        {
            SerializedProperty agent = agents.GetArrayElementAtIndex(i);

            if (agent.FindPropertyRelative("agentTypeID").intValue == 0) return agent;
        }

        return null;
    }

    /// <summary>
    /// Bakes every NavMeshSurface in the Game scene the same way the Bake button does. A scene that
    /// was not open is opened alongside whatever is, saved once the bake lands and closed again,
    /// so nothing the user has open is touched. A scene that was already open is baked in place
    /// and left for the user to save — it may hold changes of their own.
    /// </summary>
    private static void RebakeGameScene()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedHere = !scene.isLoaded;

        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        NavMeshSurface[] surfaces = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true))
            .ToArray();

        if (surfaces.Length == 0)
        {
            Debug.LogWarning($"NavMeshAgentSetup: nenhum NavMeshSurface em {GameScenePath}.");
            if (openedHere) EditorSceneManager.CloseScene(scene, removeScene: true);
            return;
        }

        NavMeshAssetManager.instance.StartBakingSurfaces(surfaces.Cast<Object>().ToArray());

        double startedAt = EditorApplication.timeSinceStartup;
        List<NavMeshSurface> pending = surfaces.ToList();

        void WaitForBake()
        {
            pending.RemoveAll(surface => surface == null || !NavMeshAssetManager.instance.IsSurfaceBaking(surface));

            bool timedOut = EditorApplication.timeSinceStartup - startedAt > BakeTimeoutSeconds;
            if (pending.Count > 0 && !timedOut) return;

            EditorApplication.update -= WaitForBake;

            if (timedOut)
            {
                Debug.LogWarning("NavMeshAgentSetup: o bake passou do tempo limite; a cena Game ficou aberta sem salvar.");
                return;
            }

            if (!openedHere)
            {
                Debug.Log("NavMeshAgentSetup: bake da cena Game refeito. Salve a cena (Ctrl+S) para manter.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, removeScene: true);

            Debug.Log("NavMeshAgentSetup: bake da cena Game refeito e salvo.");
        }

        EditorApplication.update += WaitForBake;
    }
}
