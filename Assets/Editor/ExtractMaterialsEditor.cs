// Assets/Editor/ExtractMaterialsEditor.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine.Rendering;
using System.IO;
using System.Collections.Generic;

public class ExtractMaterialsEditor : EditorWindow
{
    private DefaultAsset folderToScan;

    [MenuItem("Tools/Extract Materials")]
    public static void OpenWindow()
    {
        GetWindow<ExtractMaterialsEditor>("Extract Materials");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Folder to Scan for Models/Prefabs", EditorStyles.boldLabel);
        folderToScan = (DefaultAsset)EditorGUILayout.ObjectField("Folder", folderToScan, typeof(DefaultAsset), false);

        if (folderToScan != null && GUILayout.Button("Run Extraction"))
        {
            string path = AssetDatabase.GetAssetPath(folderToScan);
            if (Directory.Exists(path))
            {
                ExtractModels(path);
                ExtractPrefabs(path);
                AssetDatabase.Refresh();
                Debug.Log("Material extraction complete.");
            }
            else
                Debug.LogError($"\"{path}\" is not a valid folder.");
        }
    }

    private static void ExtractModels(string folderPath)
    {
        string[] modelGUIDs = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
        foreach (var guid in modelGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) continue;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
            importer.SaveAndReimport();

            Debug.Log($"[Model] Extracted materials for {assetPath}");
        }
    }

    private static void ExtractPrefabs(string folderPath)
    {
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (var guid in prefabGUIDs)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);

            // Load prefab contents for editing
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool madeChange = false;

            // Gather all renderers in the prefab
            var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string matPath = AssetDatabase.GetAssetPath(mat);

                    // If it's already a standalone .mat in your project, skip it
                    if (Path.GetExtension(matPath).ToLower() == ".mat")
                        continue;

                    // Otherwise it's embedded or sub-asset: duplicate it
                    string prefabFolder = Path.GetDirectoryName(prefabPath);
                    string newMatName = prefabRoot.name + "_" + mat.name + ".mat";
                    string newMatPath = AssetDatabase.GenerateUniqueAssetPath($"{prefabFolder}/{newMatName}");

                    // Instantiate and save new material asset
                    var newMat = Object.Instantiate(mat);
                    AssetDatabase.CreateAsset(newMat, newMatPath);

                    // Assign the new one on this renderer
                    mats[i] = newMat;
                    madeChange = true;

                    Debug.Log($"[Prefab] Created {newMatPath} and assigned to {prefabPath}");
                }

                if (madeChange)
                    r.sharedMaterials = mats;
            }

            if (madeChange)
            {
                // Write changes back to the prefab asset
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
