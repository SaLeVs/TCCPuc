using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Network
{
    /// <summary>
    /// Keeps scene-placed NetworkObjects where the scene put them, on every peer.
    ///
    /// <para><b>The bug.</b> Netcode 2.10 spawns a scene-placed NetworkObject on a client with the
    /// server's pose. When that object sits under a parent with no NetworkObject (a door under
    /// "Fixed Rooms/Box_Office_Room", the escape door under "EscapeRoom/Managers"), the server sends
    /// the <i>world</i> position but the client applies it as the <i>local</i> one
    /// (NetworkSpawnManager.CreateLocalNetworkObject, the nonNetworkObjectParent case). The object
    /// lands at parent + world position: tens of metres away, on the stage, in the middle of the
    /// map, still solid and still interactable. Every client, every match, LAN or Relay; the host
    /// never sees it. Door.Awake only covered NetworkObjects that carry a Door — not the escape
    /// door's managers, nor the doors left in the Win scene.</para>
    ///
    /// <para><b>The fix.</b> Right after a scene loads — Unity's sceneLoaded runs before Netcode
    /// collects the scene's objects and before any spawn data is applied — every scene-placed
    /// NetworkObject under a plain parent gets AutoObjectParentSync off. With it off the server
    /// writes the world pose and the client applies it as world pose, which is the same pose it
    /// already has.</para>
    ///
    /// <para><b>The resync.</b> Scene-placed NetworkObjects with nothing to move them (no
    /// NetworkTransform, no dynamic Rigidbody, no agent) never move by design. Their pose is noted
    /// at load and checked a few times a second; one found elsewhere is put back and logged, so a
    /// cause nobody has found yet still cannot leave a door in the middle of the map.</para>
    /// </summary>
    public sealed class InScenePoseGuard : MonoBehaviour
    {
        private const float CheckInterval = 0.25f;
        private const float MaxDrift = 0.01f;
        private const float MaxTurnDegrees = 0.5f;

        private sealed class Anchor
        {
            public NetworkObject Object;
            public Transform Transform;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public string Path;
            public int Corrections;
        }

        private static InScenePoseGuard _instance;
        private static readonly List<Anchor> Anchors = new List<Anchor>();

        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            GameObject go = new GameObject("[InScenePoseGuard]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InScenePoseGuard>();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            int unparented = 0;
            int anchored = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    Transform t = networkObject.transform;

                    // Netcode only looks at the immediate parent.
                    if (t.parent != null && t.parent.GetComponent<NetworkObject>() == null && networkObject.AutoObjectParentSync)
                    {
                        networkObject.AutoObjectParentSync = false;
                        unparented++;
                    }

                    if (!IsAnchored(networkObject)) continue;

                    Anchors.Add(new Anchor
                    {
                        Object = networkObject,
                        Transform = t,
                        Parent = t.parent,
                        LocalPosition = t.localPosition,
                        LocalRotation = t.localRotation,
                        LocalScale = t.localScale,
                        Path = HierarchyPath(t)
                    });
                    anchored++;
                }
            }

            if (unparented > 0 || anchored > 0)
            {
                LanDiagnosticsLog.Note($"[POSE] Cena '{scene.name}': {unparented} objeto(s) de rede sob pai comum protegidos, {anchored} vigiados.");
            }
        }

        /// <summary>Nothing on it can legitimately move its root.</summary>
        private static bool IsAnchored(NetworkObject networkObject)
        {
            GameObject go = networkObject.gameObject;

            // UI is laid out by its canvas, which moves it on its own.
            if (go.transform is RectTransform) return false;
            if (go.GetComponent<NetworkTransform>() != null) return false;
            if (go.GetComponent<NavMeshAgent>() != null) return false;
            if (go.GetComponent<CharacterController>() != null) return false;
            if (go.TryGetComponent(out Rigidbody body) && !body.isKinematic) return false;

            return true;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;

            for (int i = Anchors.Count - 1; i >= 0; i--)
            {
                Anchor a = Anchors[i];

                // Scene unloaded, or the object despawned and destroyed (a picked-up item).
                if (a.Transform == null || a.Object == null)
                {
                    Anchors.RemoveAt(i);
                    continue;
                }

                Transform t = a.Transform;

                bool parentChanged = t.parent != a.Parent && a.Parent != null;
                bool moved = (t.localPosition - a.LocalPosition).sqrMagnitude > MaxDrift * MaxDrift
                             || Quaternion.Angle(t.localRotation, a.LocalRotation) > MaxTurnDegrees;

                if (!parentChanged && !moved) continue;

                Vector3 wrong = t.position;

                if (parentChanged) t.SetParent(a.Parent, false);
                t.SetLocalPositionAndRotation(a.LocalPosition, a.LocalRotation);
                t.localScale = a.LocalScale;

                a.Corrections++;

                NetworkManager nm = NetworkManager.Singleton;
                string role = nm == null ? "offline" : nm.IsHost ? "host" : nm.IsServer ? "server" : nm.IsClient ? "client" : "offline";

                // Logged in full a few times per object, then only now and then, so a loop shows
                // without flooding the file.
                if (a.Corrections <= 3 || a.Corrections % 50 == 0)
                {
                    Debug.LogWarning(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[POSE] {0} ({1}) estava fora do lugar em ({2:0.00}, {3:0.00}, {4:0.00}){5}; reposto em ({6:0.00}, {7:0.00}, {8:0.00}). Correção nº {9}, id de rede {10}.",
                        a.Path, role, wrong.x, wrong.y, wrong.z, parentChanged ? " e com outro pai" : "",
                        t.position.x, t.position.y, t.position.z, a.Corrections,
                        a.Object.IsSpawned ? a.Object.NetworkObjectId.ToString() : "sem spawn"));
                }
            }
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return t.gameObject.scene.name + ":" + path;
        }
    }
}
