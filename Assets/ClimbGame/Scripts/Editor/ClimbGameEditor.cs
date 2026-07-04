#if UNITY_EDITOR
using System.IO;
using ClimbGame.Art;
using ClimbGame.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClimbGame.EditorTools
{
    /// <summary>
    /// Editor conveniences for the climbing demo. None of this is required to play —
    /// the game assembles itself from <see cref="ClimbGameBootstrap"/> at runtime — but
    /// these menu items make first-time setup and asset export a single click.
    /// </summary>
    public static class ClimbGameEditor
    {
        private const string SceneDir = "Assets/ClimbGame/Scenes";
        private const string ScenePath = SceneDir + "/ClimbDemo.unity";
        private const string SpriteDir = "Assets/ClimbGame/Art/Generated";

        [MenuItem("Tools/ClimbGame/Create & Open Demo Scene")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("ClimbGame");
            go.AddComponent<ClimbGameBootstrap>();

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "ClimbGame",
                "Demo scene created:\n" + ScenePath +
                "\n\nPress Play to climb.\nControls: WASD / Arrow keys, or drag the on-screen joystick.",
                "OK");
        }

        [MenuItem("Tools/ClimbGame/Export Climber Sprites to PNG")]
        public static void ExportClimberSprites()
        {
            Directory.CreateDirectory(SpriteDir);

            var sprites = ClimberSpriteFactory.Create();
            for (int i = 0; i < sprites.ClimbFrames.Length; i++)
                WriteSpritePng(sprites.ClimbFrames[i], $"climb_{i:00}.png");
            WriteSpritePng(sprites.Idle, "idle.png");

            AssetDatabase.Refresh();

            // Apply pixel-art friendly import settings to everything we just wrote.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.spritePixelsPerUnit = ClimberSpriteFactory.Width;
                    importer.SaveAndReimport();
                }
            }

            EditorUtility.DisplayDialog("ClimbGame",
                "Exported climber frames to:\n" + SpriteDir, "OK");
        }

        private static void WriteSpritePng(Sprite sprite, string fileName)
        {
            if (sprite == null || sprite.texture == null) return;
            byte[] png = sprite.texture.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(SpriteDir, fileName), png);
        }
    }
}
#endif
