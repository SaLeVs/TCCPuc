using Objects;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gives every door in the open scenes that has no NetworkObject above it one of its own.
///
/// <para>Door is a NetworkBehaviour: it only works under a spawned NetworkObject. The doors
/// inside the mission rooms borrow the room's, but a door dropped loose in the scene, or into a
/// room that is not networked (DressingRoom _Game, the Auditorium), has none — it never spawns,
/// and opening it throws "The NetworkBehaviour must be spawned before calling this method".</para>
///
/// <para>Added through the editor rather than written into the scene file, because the editor is
/// what fills in the NetworkObject's GlobalObjectIdHash — the id every peer uses to match the
/// door in its own copy of the scene. The component goes on the instance, not the prefab: a prefab
/// with its own NetworkObject would clash with the room's the day it is placed inside one.</para>
/// </summary>
public static class DoorNetworkSetup
{
    private const string MenuPath = "Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede";

    [MenuItem(MenuPath)]
    public static void AddMissingNetworkObjects()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("DoorNetworkSetup: saia do Play Mode antes — componentes adicionados no Play não são salvos.");
            return;
        }

        Door[] doors = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int added = 0;

        foreach (Door door in doors)
        {
            if (door.GetComponentInParent<NetworkObject>(true) != null) continue;

            Undo.AddComponent<NetworkObject>(door.gameObject);
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);

            Debug.Log($"DoorNetworkSetup: NetworkObject adicionado em '{PathOf(door.transform)}' (cena {door.gameObject.scene.name}).", door);
            added++;
        }

        Debug.Log(added == 0
            ? $"DoorNetworkSetup: nenhuma das {doors.Length} portas abertas estava sem rede."
            : $"DoorNetworkSetup: {added} de {doors.Length} portas receberam NetworkObject. Salve a cena (Ctrl+S).");
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;

        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
