using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Utility to help fix Input System TypeLoadException issues.
/// Based on Unity's latest recommendations for resolving InputActionAsset type loading errors.
/// </summary>
public class ForceInputSystemReimport
{
    [MenuItem("Assets/Fix Input System Type Load Error", priority = 1000)]
    public static void FixInputSystemTypeLoadError()
    {
        Debug.Log("=== Fixing Input System TypeLoadException ===");
        
        // Step 1: Force reimport of Input System UXML files
        string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
        int reimported = 0;
        foreach (string guid in uxmlGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("com.unity.inputsystem"))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                reimported++;
            }
        }
        Debug.Log($"✓ Reimported {reimported} Input System UXML files.");
        
        // Step 2: Reimport Input System package folder
        string inputSystemPath = "Packages/com.unity.inputsystem";
        if (AssetDatabase.IsValidFolder(inputSystemPath))
        {
            AssetDatabase.ImportAsset(inputSystemPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("✓ Reimported Input System package folder.");
        }
        
        // Step 3: Force script recompilation
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();
        Debug.Log("✓ Requested script recompilation.");
        
        Debug.Log("\n=== Next Steps ===");
        Debug.Log("If the error persists, follow these steps:");
        Debug.Log("1. Close Unity Editor completely");
        Debug.Log("2. Navigate to your project folder");
        Debug.Log("3. Delete the 'Library' folder (this is safe - Unity will regenerate it)");
        Debug.Log("4. Reopen Unity Editor - it will rebuild everything");
        Debug.Log("\nAlternatively:");
        Debug.Log("- Go to Window > Package Manager");
        Debug.Log("- Find 'Input System' package");
        Debug.Log("- Click the dropdown arrow and select 'Reimport'");
        Debug.Log("\nDone! Check the Console for results.");
    }
    
    [MenuItem("Assets/Verify Input System Configuration", priority = 1001)]
    public static void VerifyInputSystemConfiguration()
    {
        Debug.Log("=== Checking Input System Configuration ===");
        
        // Check if package is installed
        var inputSystemPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityEngine.InputSystem.InputSystem).Assembly);
        if (inputSystemPackage != null)
        {
            Debug.Log($"✓ Input System package found: {inputSystemPackage.version}");
        }
        else
        {
            Debug.LogWarning("✗ Input System package not found!");
            Debug.LogWarning("Install it via: Window > Package Manager > Unity Registry > Input System");
        }
        
        // Check Project Settings for Active Input Handling
        var serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var activeInputHandler = serializedObject.FindProperty("activeInputHandler");
        if (activeInputHandler != null)
        {
            var value = activeInputHandler.intValue;
            Debug.Log($"Active Input Handling: {value} (0=Old, 1=New, 2=Both)");
            if (value == 0)
            {
                Debug.LogWarning("Consider enabling Input System in: Edit > Project Settings > Player > Other Settings > Active Input Handling");
            }
        }
        
        Debug.Log("=== Configuration Check Complete ===");
    }
}

