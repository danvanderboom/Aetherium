using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Ensures Input System assembly is loaded early to prevent TypeLoadException when UXML files
/// reference InputActionAsset types. This script runs on domain reload and after compilation
/// to ensure the assembly is available when Unity's UXML importer parses type references.
/// </summary>
public class InputSystemUxmlPreloader
{
    private static bool s_InputSystemAssemblyLoaded = false;
    private static Assembly s_InputSystemAssembly = null;

    /// <summary>
    /// Called when Unity's domain reloads or editor initializes.
    /// Ensures Input System assembly is loaded early to prevent TypeLoadException during UXML import.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EnsureInputSystemAssemblyLoaded();
        
        // Also reimport UXML files after compilation completes to fix any import errors
        CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    /// <summary>
    /// Called after compilation finishes. Reimports Input System UXML files to fix any type loading errors.
    /// Uses EditorApplication.delayCall to defer reimport until after compilation is fully complete.
    /// </summary>
    private static void OnCompilationFinished(object obj)
    {
        EnsureInputSystemAssemblyLoaded();
        
        // Delay reimport to ensure compilation is fully complete
        EditorApplication.delayCall += () =>
        {
            // Reimport Input System UXML files after compilation to ensure types are available
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
            
            if (reimported > 0)
            {
                Debug.Log($"Input System UXML Preloader: Reimported {reimported} UXML files after compilation to resolve type references.");
            }
        };
    }

    /// <summary>
    /// Ensures the Input System assembly is loaded and available for type resolution.
    /// </summary>
    private static void EnsureInputSystemAssemblyLoaded()
    {
        // If already loaded, skip
        if (s_InputSystemAssemblyLoaded && s_InputSystemAssembly != null)
            return;

        try
        {
            // Try to get the Input System assembly by name from already loaded assemblies
            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.GetName().Name == "Unity.InputSystem")
                {
                    s_InputSystemAssembly = assembly;
                    s_InputSystemAssemblyLoaded = true;
                    return;
                }
            }

            // If not found, try loading it explicitly
            // This might happen during the first import when assemblies aren't loaded yet
            try
            {
                s_InputSystemAssembly = Assembly.Load("Unity.InputSystem");
                if (s_InputSystemAssembly != null)
                {
                    s_InputSystemAssemblyLoaded = true;
                }
            }
            catch (Exception ex)
            {
                // Assembly not available yet - this is expected during first import
                // Unity will load it later, and subsequent imports will succeed
                Debug.LogWarning($"Input System assembly not yet available. This is normal during first import. Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - we don't want to break the import process
            Debug.LogWarning($"Could not preload Input System assembly: {ex.Message}");
        }
    }
}

