using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tutorial.Building
{
    /// <summary>
    /// Placas de parede do tutorial no estilo do teatro: a mesma placa do ACHADOS E PERDIDOS da cena
    /// Game (tábua M_PlacaAchadosEPerdidos, moldura SignBoard e letra Lalezar laranja), atrás de cada
    /// texto de parede. As marcas de chão (fita adesiva de palco) continuam como estão.
    ///
    /// <para>Usado pelo gerador da cena e pelo menu, que converte a cena aberta sem gerar de novo
    /// (a cena tem bake de luz e ajustes feitos à mão).</para>
    /// </summary>
    public static class TutorialPlaques
    {
        public const string BoardMaterialPath = "Assets/Materials/Scenary/Dressing_room/LostAndFound/M_PlacaAchadosEPerdidos.mat";
        public const string FrameMaterialPath = "Assets/Materials/Scenary/Box_Office/SignBoard/SignBoard.mat";
        public const string FontPath = "Assets/Fonts/Lalezar-Regular SDF.asset";

        private const string PlaqueName = "Placa";

        // Cor do texto da placa ACHADOS E PERDIDOS do jogo, e o papel do HUD para o texto menor.
        public static readonly Color TitleColor = new Color32(0xF2, 0x9E, 0x38, 0xFF);
        public static readonly Color PaperColor = new Color32(0xE0, 0xD6, 0xCF, 0xFF);

        // Lalezar é mais alta que a Acme: o mesmo texto fica do mesmo tamanho com 85% da fonte.
        private const float FontScale = 0.85f;
        private const float BoardThickness = 0.04f;
        private const float FrameWidth = 0.03f;

        [MenuItem("Tools/TCC/Tutorial/Placas de parede no estilo do teatro (cena aberta)", priority = 30)]
        private static void ConvertOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != TutorialLauncher.SceneName)
            {
                EditorUtility.DisplayDialog("Placas do tutorial", $"Abra a cena {TutorialLauncher.SceneName} primeiro.", "Ok");
                return;
            }

            int count = ConvertScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Tutorial: {count} placas de parede no estilo do teatro.");
        }

        /// <summary>Converte todos os textos de parede da cena. Devolve quantos viraram placa.</summary>
        public static int ConvertScene(Scene scene)
        {
            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TextMeshPro text in root.GetComponentsInChildren<TextMeshPro>(true))
                {
                    if (IsWallLabel(text) && Convert(text)) count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Texto de parede do tutorial: em pé (não é marca de chão), não é placa de SAÍDA (verde, de
        /// propósito) e ainda não tem placa.
        /// </summary>
        private static bool IsWallLabel(TextMeshPro text)
        {
            if (Mathf.Abs(text.transform.forward.y) > 0.5f) return false;
            if (text.text.Contains("SAÍDA")) return false;

            // Já convertido (ou a placa ACHADOS E PERDIDOS, que já é a do jogo).
            return text.font == null || text.font.name != "Lalezar-Regular SDF";
        }

        /// <summary>
        /// Troca a letra para a da placa do jogo e põe a tábua com moldura atrás do texto. As peças
        /// ficam irmãs do texto (no mesmo grupo), então somem junto quando a lição acaba.
        /// </summary>
        public static bool Convert(TextMeshPro text)
        {
            Material board = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
            Material frame = AssetDatabase.LoadAssetAtPath<Material>(FrameMaterialPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (board == null || frame == null || font == null)
            {
                Debug.LogWarning("Tutorial: material ou fonte da placa do jogo não encontrado.");
                return false;
            }

            Color original = text.color;

            text.font = font;
            text.fontSize *= FontScale;
            text.color = PlaqueColor(original);
            text.ForceMeshUpdate();

            Bounds bounds = text.textBounds;
            if (bounds.size.x <= 0.001f) return false;

            float padX = Mathf.Max(0.1f, text.fontSize * 0.06f);
            float padY = Mathf.Max(0.05f, text.fontSize * 0.03f);
            Vector2 size = new Vector2(bounds.size.x + padX * 2f, bounds.size.y + padY * 2f);

            // A frente do texto lê de -forward: a tábua vai meio centímetro para trás dele.
            GameObject plaque = new GameObject(PlaqueName);
            plaque.transform.SetParent(text.transform.parent, false);
            plaque.transform.SetPositionAndRotation(
                text.transform.TransformPoint(bounds.center) + text.transform.forward * (0.005f + BoardThickness * 0.5f),
                text.transform.rotation);
            plaque.isStatic = false;

            Cube(plaque.transform, "Tabua", Vector3.zero, new Vector3(size.x, size.y, BoardThickness), board);

            float frameDepth = BoardThickness + 0.01f;
            Cube(plaque.transform, "Borda", new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, FrameWidth, frameDepth), frame);
            Cube(plaque.transform, "Borda", new Vector3(0f, -size.y * 0.5f, 0f), new Vector3(size.x, FrameWidth, frameDepth), frame);
            Cube(plaque.transform, "Borda", new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(FrameWidth, size.y + FrameWidth, frameDepth), frame);
            Cube(plaque.transform, "Borda", new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(FrameWidth, size.y + FrameWidth, frameDepth), frame);

            // A moldura sai um pouco para a frente da tábua: o texto não pode ficar atrás dela.
            text.transform.position -= text.transform.forward * 0.004f;

            EditorUtility.SetDirty(text);
            return true;
        }

        /// <summary>
        /// Título em laranja (a cor da placa do jogo); avisos com cor de sentido (vermelho de
        /// "travada", verde de "aberta") mantêm a cor; o resto vira papel.
        /// </summary>
        private static Color PlaqueColor(Color original)
        {
            Color.RGBToHSV(original, out float h, out float s, out _);

            bool yellow = h > 0.1f && h < 0.2f && s > 0.4f;
            if (yellow) return TitleColor;

            bool meaningful = s > 0.35f;
            return meaningful ? original : PaperColor;
        }

        private static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());

            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
