using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified HP bar controller that manages modular UI components.
/// Can be used for both Player and Wild Pokemon HP bars.
/// </summary>
public class HPBarUnified : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Pokemon pokemon;
    
    [Header("Modules Container")]
    [SerializeField] private RectTransform modulesContainer;

    [Header("Pre-configured Modules (set in prefab)")]
    [SerializeField] private List<MonoBehaviour> prefabModules = new List<MonoBehaviour>();

    private List<IHPBarModule> activeModules = new List<IHPBarModule>();
    private Dictionary<string, IHPBarModule> moduleRegistry = new Dictionary<string, IHPBarModule>();
    private bool isOwner;
    private bool isInitialized;

    public Pokemon Pokemon => pokemon;
    public bool IsInitialized => isInitialized;
    public IReadOnlyList<IHPBarModule> ActiveModules => activeModules;

    /// <summary>
    /// Initialize the HP bar with a Pokemon.
    /// </summary>
    /// <param name="pokemon">The Pokemon to display.</param>
    /// <param name="isOwner">Whether the local player owns this Pokemon.</param>
    public void SetPokemon(Pokemon pokemon, bool isOwner = false)
    {
        this.pokemon = pokemon;
        this.isOwner = isOwner;

        // Initialize all pre-configured modules from prefab
        foreach (var moduleBehaviour in prefabModules)
        {
            if (moduleBehaviour is IHPBarModule module && moduleBehaviour != null)
            {
                RegisterAndInitializeModule(module);
            }
        }

        isInitialized = true;
    }

    /// <summary>
    /// Add a module at runtime.
    /// </summary>
    /// <param name="module">The module to add.</param>
    public void AddModule(IHPBarModule module)
    {
        if (module == null) return;

        RegisterAndInitializeModule(module);

        if (isInitialized)
        {
            module.Initialize(pokemon, isOwner);
        }
    }

    /// <summary>
    /// Add a module by instantiating a prefab.
    /// </summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <param name="prefab">The prefab to instantiate.</param>
    /// <param name="parent">Optional parent transform (defaults to modulesContainer).</param>
    /// <returns>The instantiated module.</returns>
    public T AddModuleFromPrefab<T>(T prefab, RectTransform parent = null) where T : MonoBehaviour, IHPBarModule
    {
        if (prefab == null) return null;

        RectTransform targetParent = parent != null ? parent : modulesContainer;
        T instance = Instantiate(prefab, targetParent);
        AddModule(instance);
        return instance;
    }

    /// <summary>
    /// Remove a module by reference.
    /// </summary>
    /// <param name="module">The module to remove.</param>
    /// <param name="destroyGameObject">Whether to destroy the module's GameObject.</param>
    public void RemoveModule(IHPBarModule module, bool destroyGameObject = true)
    {
        if (module == null) return;

        if (moduleRegistry.ContainsKey(module.ModuleId))
        {
            moduleRegistry.Remove(module.ModuleId);
        }
        activeModules.Remove(module);

        module.Cleanup();

        if (destroyGameObject && module is MonoBehaviour mb)
        {
            Destroy(mb.gameObject);
        }
    }

    /// <summary>
    /// Remove a module by ID.
    /// </summary>
    /// <param name="moduleId">The module ID to remove.</param>
    /// <param name="destroyGameObject">Whether to destroy the module's GameObject.</param>
    public void RemoveModule(string moduleId, bool destroyGameObject = true)
    {
        if (TryGetModule(moduleId, out IHPBarModule module))
        {
            RemoveModule(module, destroyGameObject);
        }
    }

    /// <summary>
    /// Get a module by ID.
    /// </summary>
    /// <param name="moduleId">The module ID.</param>
    /// <returns>The module, or null if not found.</returns>
    public IHPBarModule GetModule(string moduleId)
    {
        moduleRegistry.TryGetValue(moduleId, out IHPBarModule module);
        return module;
    }

    /// <summary>
    /// Try to get a module by ID.
    /// </summary>
    public bool TryGetModule(string moduleId, out IHPBarModule module)
    {
        return moduleRegistry.TryGetValue(moduleId, out module);
    }

    /// <summary>
    /// Get a module by type.
    /// </summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns>The first module of the specified type, or null.</returns>
    public T GetModule<T>() where T : class, IHPBarModule
    {
        foreach (var module in activeModules)
        {
            if (module is T typedModule)
            {
                return typedModule;
            }
        }
        return null;
    }

    /// <summary>
    /// Get all modules of a specific type.
    /// </summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns>List of modules of the specified type.</returns>
    public List<T> GetModules<T>() where T : class, IHPBarModule
    {
        List<T> result = new List<T>();
        foreach (var module in activeModules)
        {
            if (module is T typedModule)
            {
                result.Add(typedModule);
            }
        }
        return result;
    }

    /// <summary>
    /// Check if a module with the given ID exists.
    /// </summary>
    public bool HasModule(string moduleId)
    {
        return moduleRegistry.ContainsKey(moduleId);
    }

    /// <summary>
    /// Set visibility for all modules.
    /// </summary>
    public void SetAllModulesVisibility(bool visible)
    {
        foreach (var module in activeModules)
        {
            module.SetVisibility(visible);
        }
    }

    /// <summary>
    /// Set visibility for a specific module by ID.
    /// </summary>
    public void SetModuleVisibility(string moduleId, bool visible)
    {
        if (TryGetModule(moduleId, out IHPBarModule module))
        {
            module.SetVisibility(visible);
        }
    }

    /// <summary>
    /// Force update all modules.
    /// </summary>
    public void UpdateAllModules()
    {
        foreach (var module in activeModules)
        {
            module.UpdateUI();
        }
    }

    private void RegisterAndInitializeModule(IHPBarModule module)
    {
        string id = module.ModuleId;

        // Handle duplicate IDs by appending a number
        if (moduleRegistry.ContainsKey(id))
        {
            int counter = 1;
            while (moduleRegistry.ContainsKey($"{id}_{counter}"))
            {
                counter++;
            }
            id = $"{id}_{counter}";
        }

        moduleRegistry[id] = module;
        activeModules.Add(module);

        if (isInitialized && pokemon != null)
        {
            module.Initialize(pokemon, isOwner);
        }
    }

    private void OnDestroy()
    {
        foreach (var module in activeModules)
        {
            module.Cleanup();
        }
        activeModules.Clear();
        moduleRegistry.Clear();
    }
}
