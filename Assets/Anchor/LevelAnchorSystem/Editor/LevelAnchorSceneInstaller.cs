using Anchor.LevelAnchorSystem;
using Anchor.ForceSystem;
using DesignerSpace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anchor.LevelAnchorSystem.Editor
{
    public static class LevelAnchorSceneInstaller
    {
        private const string RegistryObjectName = "LevelAnchorRegistry";
        private const string ForceDriverObjectName = "ForceSystemDebugDriver";

        [MenuItem("Anchor/Level Anchors/Install Registry And Smoke Test")]
        public static void InstallRegistryAndSmokeTestMenu()
        {
            Debug.Log(InstallRegistryAndRunSmoke());
        }

        [MenuItem("Anchor/Level Anchors/Install Registry And Force Debug Driver")]
        public static void InstallRegistryAndForceDebugDriverMenu()
        {
            Debug.Log(InstallRegistryAndForceDebugDriver());
        }

        public static string InstallRegistryAndRunSmoke()
        {
            var registry = FindOrCreateRegistry();
            registry.RebuildFromScene();

            var anchors = Object.FindObjectsOfType<AnchorPoint>();
            if (anchors.Length == 0)
            {
                return "LevelAnchorRegistry smoke failed: no AnchorPoint found in active scene.";
            }

            var anchorPosition = anchors[0].transform.position;
            var stability = registry.GetStability(anchorPosition);
            var foundNearest = registry.TryFindNearestAnchor(anchorPosition, out var nearest);
            var farPoint = anchorPosition + Vector3.one * 1000f;
            var foundFar = registry.TryFindNearestAnchor(farPoint, out _, 0.1f);

            if (registry.RegisteredCount <= 0 || stability <= 0 || !foundNearest || foundFar)
            {
                return $"LevelAnchorRegistry smoke failed: registered={registry.RegisteredCount}, " +
                    $"stability={stability}, foundNearest={foundNearest}, foundFar={foundFar}.";
            }

            EditorSceneManager.SaveOpenScenes();
            return $"LevelAnchorRegistry smoke passed: registered={registry.RegisteredCount}, " +
                $"anchor={nearest.DebugName}, stability={stability}, distance={nearest.Distance:0.###}.";
        }

        public static string InstallRegistryAndForceDebugDriver()
        {
            var registry = FindOrCreateRegistry();
            registry.RebuildFromScene();

            var driver = FindOrCreateForceDebugDriver();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("gripQueryProviderSource").objectReferenceValue = registry;
            serializedDriver.FindProperty("useGripQueryProvider").boolValue = true;
            serializedDriver.FindProperty("cacheGripCandidates").boolValue = true;
            serializedDriver.FindProperty("gripQueryIntervalSeconds").floatValue = 0.02f;
            serializedDriver.ApplyModifiedProperties();

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(driver);

            var anchors = Object.FindObjectsOfType<AnchorPoint>();
            AnchorPointQueryResult nearest = default;
            var smokePassed = anchors.Length > 0 &&
                registry.RegisteredCount > 0 &&
                registry.TryFindNearestAnchor(anchors[0].transform.position, out nearest) &&
                nearest.CurrentStability > 0;

            EditorSceneManager.SaveOpenScenes();

            return smokePassed
                ? $"Force debug driver connected: registry={registry.name}, driver={driver.name}, " +
                    $"registered={registry.RegisteredCount}, sample={nearest.DebugName}, stability={nearest.CurrentStability}."
                : $"Force debug driver connected but smoke incomplete: registered={registry.RegisteredCount}, anchors={anchors.Length}.";
        }

        private static LevelAnchorRegistry FindOrCreateRegistry()
        {
            var registry = Object.FindObjectOfType<LevelAnchorRegistry>();
            if (registry != null)
            {
                return registry;
            }

            var existingObject = GameObject.Find(RegistryObjectName);
            if (existingObject == null)
            {
                existingObject = new GameObject(RegistryObjectName);
                Undo.RegisterCreatedObjectUndo(existingObject, "Create LevelAnchorRegistry");
            }

            registry = existingObject.GetComponent<LevelAnchorRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<LevelAnchorRegistry>(existingObject);
            }

            return registry;
        }

        private static ForceSystemDebugDriver FindOrCreateForceDebugDriver()
        {
            var driver = Object.FindObjectOfType<ForceSystemDebugDriver>();
            if (driver != null)
            {
                return driver;
            }

            var existingObject = GameObject.Find(ForceDriverObjectName);
            if (existingObject == null)
            {
                existingObject = new GameObject(ForceDriverObjectName);
                Undo.RegisterCreatedObjectUndo(existingObject, "Create ForceSystemDebugDriver");
            }

            driver = existingObject.GetComponent<ForceSystemDebugDriver>();
            if (driver == null)
            {
                driver = Undo.AddComponent<ForceSystemDebugDriver>(existingObject);
            }

            return driver;
        }
    }
}
