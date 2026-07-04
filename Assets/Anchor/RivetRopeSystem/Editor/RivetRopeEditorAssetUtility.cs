using UnityEditor;
using UnityEngine;

namespace Anchor.RivetRopeSystem.Editor
{
    internal static class RivetRopeEditorAssetUtility
    {
        public const string ConfigFolder = "Assets/Anchor/RivetRopeSystem/Config";
        public const string DefaultRopeTexturePath = ConfigFolder + "/RivetRopeDefaultTexture.asset";
        public const string DefaultRopeMaterialPath = ConfigFolder + "/RivetRopeDefaultMaterial.mat";

        public static void EnsureCoreFolders()
        {
            EnsureFolder("Assets/Anchor/RivetRopeSystem", "Config");
            EnsureFolder("Assets/Anchor/RivetRopeSystem", "Scenes");
        }

        public static Material EnsureDefaultRopeMaterial()
        {
            EnsureCoreFolders();
            var texture = EnsureDefaultRopeTexture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultRopeMaterialPath);
            if (material != null)
            {
                ConfigureRopeMaterial(material, texture);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                return material;
            }

            material = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            material.name = "RivetRopeDefaultMaterial";
            ConfigureRopeMaterial(material, texture);
            AssetDatabase.CreateAsset(material, DefaultRopeMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ConfigureRopeMaterial(Material material, Texture2D texture)
        {
            var standard = Shader.Find("Standard");
            if (standard != null && material.shader != standard)
            {
                material.shader = standard;
            }

            material.color = new Color(0.86f, 0.78f, 0.58f);
            material.mainTexture = texture;
            material.renderQueue = 2000;
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.12f);
            }
        }

        private static Texture2D EnsureDefaultRopeTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultRopeTexturePath);
            if (texture != null)
            {
                return texture;
            }

            texture = new Texture2D(32, 8, TextureFormat.RGBA32, false)
            {
                name = "RivetRopeDefaultTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var baseColor = new Color(0.78f, 0.66f, 0.42f, 1f);
            var strandColor = new Color(0.96f, 0.86f, 0.62f, 1f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    var strand = (x + y * 2) % 8 < 3;
                    texture.SetPixel(x, y, strand ? strandColor : baseColor);
                }
            }

            texture.Apply();
            AssetDatabase.CreateAsset(texture, DefaultRopeTexturePath);
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
