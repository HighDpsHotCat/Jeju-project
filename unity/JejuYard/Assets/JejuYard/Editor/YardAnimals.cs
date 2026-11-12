using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jeju.Yard.EditorArt
{
    public static class YardAnimals
    {
        const string Art = "Assets/JejuYard/Art/";
        const string AnimalGroup = Art + "YardAnimals.prefab";

        public static GameObject Build()
        {
            var group = new GameObject("Yard animals");
            YardMammals.Build(group.transform);
            YardBirds.Build(group.transform);
            YardButterfly.Build(group.transform);
            for (int i = 0; i < group.transform.childCount; i++)
            {
                GameObject animal = group.transform.GetChild(i).gameObject;
                SavePrefab(animal, Art + animal.name + ".prefab");
            }
            SavePrefab(group, AnimalGroup);
            return group;
        }

        static void SavePrefab(GameObject model, string path)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(model, path, InteractionMode.AutomatedAction, out bool saved);
            if (!saved) throw new IOException("Could not save " + path);
        }

        [MenuItem("Jeju Yard/Add animals to courtyard")]
        public static void PlaceInCourtyard()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != YardStudio.ScenePath)
                throw new InvalidOperationException("Open JejuYard.unity before placing the animals.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnimalGroup);
            if (prefab == null) throw new FileNotFoundException("Animal models are missing", AnimalGroup);
            foreach (var root in scene.GetRootGameObjects())
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab) return;

            var group = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            group.name = "Yard animals";
            Undo.RegisterCreatedObjectUndo(group, "Add yard animals");
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
