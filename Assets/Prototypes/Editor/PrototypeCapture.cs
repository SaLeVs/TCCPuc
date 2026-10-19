using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Prototypes.Editor
{
    /// <summary>
    /// Renders views of a prototype level to PNG with a temporary camera, without touching the
    /// Scene view: a top-down overview without ceilings, and eye-level shots.
    /// </summary>
    public static class PrototypeCapture
    {
        public struct Shot { public string Name; public Vector3 Position; public Vector3 LookAt; public float Fov; }

        /// <summary>Top-down close-up of one area (world x/z rect), ceilings hidden, flat light.</summary>
        public static string CaptureArea(string scenePath, string outFile, Rect area, float topY = 40f)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
            var camGo = new GameObject("__ProtoCaptureCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            int w = 1400, h = Mathf.RoundToInt(1400f * area.height / area.width);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = area.height * 0.5f;
            cam.transform.SetPositionAndRotation(new Vector3(area.center.x, topY, area.center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 0.1f; cam.farClipPlane = topY + 5f;
            cam.cullingMask = LayerMask.GetMask("Ground", "Walls", "Doors", "Props", "Recordable", "Interactable", "MainStage");
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.25f, 0.35f);
            var sun = FlatLight(scene);
            try
            {
                cam.Render(); cam.Render();
                Render(cam, rt, outFile);
            }
            finally
            {
                cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(sun);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return outFile;
        }

        /// <summary>The same set of views for every level: overview, corridors, hub rooms.</summary>
        public static string CaptureStandard(int level, string outFolder)
        {
            var l = new PrototypeLayout(PrototypeDiagram.Load(PrototypeBuilder.DiagramPath(level)));
            var shots = new List<Shot>();
            Vector3 Eye(WPiece p, float t) => p.Horizontal ? new Vector3(t, p.FloorA + 1.6f, p.Line) : new Vector3(p.Line, p.FloorA + 1.6f, t);

            var red = l.Pieces.Where(p => p.Color == BarColor.Red).OrderByDescending(p => p.Length).First();
            shots.Add(new Shot { Name = "1_corredor_terreo", Position = Eye(red, red.A + 2f), LookAt = Eye(red, red.B) });
            var green = l.Pieces.Where(p => p.Color == BarColor.Green).OrderByDescending(p => p.Length).First();
            shots.Add(new Shot { Name = "2_corredor_andar2", Position = Eye(green, green.B - 1f), LookAt = Eye(green, green.A) });
            shots.Add(new Shot { Name = "3_auditorio_do_fundo", Position = new Vector3(0f, 4.5f + 1.7f, 19f), LookAt = new Vector3(0f, 2.5f, -8f), Fov = 75f });
            shots.Add(new Shot { Name = "4_auditorio_do_palco", Position = new Vector3(0f, 1.5f + 1.7f, -4f), LookAt = new Vector3(0f, 4f, 18f), Fov = 75f });
            WRoom b = l.Hub(RoomKind.Bilheteria);
            shots.Add(new Shot { Name = "5_bilheteria", Position = b.Pivot + new Vector3(-8f, 1.7f, -9f), LookAt = b.Pivot + new Vector3(4f, 1.2f, 8f), Fov = 75f });
            WRoom e = l.Hub(RoomKind.Escape);
            shots.Add(new Shot { Name = "6_frente_do_escape", Position = new Vector3(0f, 4.5f + 1.7f, e.Footprint.yMin - 3f), LookAt = new Vector3(0f, 4.5f + 1.5f, e.Footprint.yMin + 4f) });
            WRoom dr = l.Rooms.First(r => r.Room.Kind == RoomKind.Dressing);
            Vector3 dp = dr.DoorPoints.Values.First();
            shots.Add(new Shot { Name = "7_porta_do_camarim", Position = dp + new Vector3(0.5f, 1.6f, 7f), LookAt = dp + new Vector3(0f, 1.2f, 0f) });
            var wing = l.Pieces.Where(p => p.Color == BarColor.Red && !p.Horizontal && p.Line < -20f).OrderByDescending(p => p.Length).FirstOrDefault();
            if (wing != null) shots.Add(new Shot { Name = "8_ala_esquerda", Position = Eye(wing, wing.B - 2f), LookAt = Eye(wing, wing.A) });

            var plan = new PrototypeCorridors(l, null);
            plan.Plan();
            int k = 0;
            foreach (var s in plan.Stairs.Take(2))
            {
                shots.Add(new Shot { Name = $"9_escada_{k}_de_baixo", Position = s.Bottom + s.Dir * 2.5f + Vector3.up * 1.6f, LookAt = s.Top + Vector3.up * 1.6f, Fov = 75f });
                shots.Add(new Shot { Name = $"9_escada_{k}_de_cima", Position = s.Top - s.Dir * 1.5f + Vector3.up * 1.6f, LookAt = s.Bottom + Vector3.up * 0.8f, Fov = 75f });
                k++;
            }
            k = 0;
            foreach (var d in plan.Ducts.Take(2))
            {
                Vector3 da = d.FloorA <= d.FloorB ? d.A : d.B, db = d.FloorA <= d.FloorB ? d.B : d.A;
                Vector3 dir = (new Vector3(db.x, 0, db.z) - new Vector3(da.x, 0, da.z)).normalized;
                shots.Add(new Shot { Name = $"10_duto_{k}_entrada", Position = da - dir * 1.4f + Vector3.up * 1.2f + Vector3.Cross(Vector3.up, dir) * 0.8f, LookAt = da + Vector3.up * 0.6f, Fov = 75f });
                shots.Add(new Shot { Name = $"10_duto_{k}_dentro", Position = da + dir * 1.2f + Vector3.up * 0.7f, LookAt = da + dir * 8f + Vector3.up * 0.7f, Fov = 80f });
                k++;
            }
            shots.Add(new Shot { Name = "11_porta_lateral_auditorio", Position = new Vector3(-24f, 1.6f, -1.85f), LookAt = new Vector3(-16f, 1.4f, -1.85f) });
            shots.Add(new Shot { Name = "12_dentro_porta_lateral", Position = new Vector3(-10f, 2.6f, -1.85f), LookAt = new Vector3(-16.5f, 1.4f, -1.85f) });
            return Capture(PrototypeBuilder.ScenePath(level), outFolder, shots, true);
        }

        public static string Capture(string scenePath, string outFolder, IList<Shot> shots, bool overview, int width = 1600, int height = 900)
        {
            Directory.CreateDirectory(outFolder);
            // An isolated preview copy: nothing else open in the editor gets into the picture.
            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);

            var log = new System.Text.StringBuilder();
            var camGo = new GameObject("__ProtoCaptureCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene; // renders this scene only: the other open scenes stay untouched
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;

            try
            {
                if (overview)
                {
                    var bounds = SceneBounds(scene);
                    var sun = FlatLight(scene);
                    cam.orthographic = true;
                    cam.transform.SetPositionAndRotation(new Vector3(bounds.center.x, 80f, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
                    cam.orthographicSize = Mathf.Max(bounds.size.z * 0.5f, bounds.size.x * 0.5f * height / width) + 4f;
                    cam.cullingMask = LayerMask.GetMask("Ground", "Walls", "Doors", "Props", "Recordable", "Interactable", "MainStage");
                    data.renderPostProcessing = false;
                    Render(cam, rt, Path.Combine(outFolder, "visao_de_cima.png"));
                    log.AppendLine("visao_de_cima.png");
                    Object.DestroyImmediate(sun);
                    data.renderPostProcessing = true;
                }

                cam.orthographic = false;
                cam.cullingMask = ~0;
                foreach (Shot s in shots)
                {
                    cam.fieldOfView = s.Fov > 0 ? s.Fov : 70f;
                    cam.transform.position = s.Position;
                    cam.transform.LookAt(s.LookAt);
                    Render(cam, rt, Path.Combine(outFolder, s.Name + ".png"));
                    log.AppendLine(s.Name + ".png");
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return log.ToString();
        }

        /// <summary>A bright light from above, only inside the preview scene, for the plan views.</summary>
        private static GameObject FlatLight(Scene scene)
        {
            var go = new GameObject("__ProtoCaptureSun");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(80f, 30f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
            return go;
        }

        private static void Render(Camera cam, RenderTexture rt, string path)
        {
            // The first frames after a domain reload come out black: warm up before reading.
            cam.Render();
            cam.Render();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Bounds SceneBounds(Scene scene)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "NIVEL_PROTOTIPO");
            var rs = (root != null ? root.GetComponentsInChildren<Renderer>() : scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>())).Where(r => r.enabled).ToList();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
