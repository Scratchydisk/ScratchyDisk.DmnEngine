using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// A folder of DMN models indexed by <c>definitions/@namespace</c>. It is the
    /// metadata-driven counterpart to a single <see cref="DmnModel"/>: you load a
    /// directory once, then create executable definitions by namespace (or by
    /// model name) and let imports resolve inside the same set.
    /// </summary>
    public sealed class DmnModelSet
    {
        private readonly Dictionary<string, string> pathsByNamespace =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> pathsByName =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly HashSet<string> ambiguousNames = new(StringComparer.Ordinal);

        private DmnModelSet(string directory)
        {
            Directory = directory;
        }

        /// <summary>Directory the set was loaded from.</summary>
        public string Directory { get; }

        /// <summary>Namespaces available in the set.</summary>
        public IReadOnlyCollection<string> Namespaces => pathsByNamespace.Keys.ToList();

        /// <summary>Model names (<c>definitions/@name</c>) available in the set.</summary>
        public IReadOnlyCollection<string> ModelNames => pathsByName.Keys.ToList();

        /// <summary>Non-fatal problems encountered while loading, if parse errors are ignored.</summary>
        public IReadOnlyList<DmnModelSetError> Errors { get; private set; } = Array.Empty<DmnModelSetError>();

        /// <summary>
        /// Loads every <c>*.dmn</c> file in a folder and indexes it by namespace and
        /// name. Duplicate namespaces are rejected because the namespace is the
        /// model's identity.
        /// </summary>
        /// <param name="directory">Folder containing DMN files.</param>
        /// <param name="recursive">Include subdirectories.</param>
        /// <param name="ignoreParseErrors">
        /// When true, files that cannot be parsed are reported in <see cref="Errors"/>
        /// and skipped; when false, the first parse failure throws.
        /// </param>
        public static DmnModelSet LoadDirectory(string directory, bool recursive = false, bool ignoreParseErrors = false)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A model directory is required", nameof(directory));
            if (!System.IO.Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Model directory not found: {directory}");

            var set = new DmnModelSet(Path.GetFullPath(directory));
            var errors = new List<DmnModelSetError>();
            var search = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var file in System.IO.Directory.GetFiles(set.Directory, "*.dmn", search).OrderBy(f => f, StringComparer.Ordinal))
            {
                DmnModel model;
                try
                {
                    model = DmnParser.ParseAutoDetect(file);
                }
                catch (Exception ex)
                {
                    if (!ignoreParseErrors)
                        throw new DmnModelSetException($"Failed to parse model '{file}': {ex.Message}", ex);
                    errors.Add(new DmnModelSetError(file, ex.Message));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(model.Namespace))
                {
                    var message = "Missing definitions/@namespace";
                    if (!ignoreParseErrors)
                        throw new DmnModelSetException($"Model '{file}' has no namespace: {message}");
                    errors.Add(new DmnModelSetError(file, message));
                    continue;
                }

                if (set.pathsByNamespace.TryGetValue(model.Namespace, out var existing))
                {
                    throw new DmnModelSetException(
                        $"Duplicate model namespace '{model.Namespace}' in '{existing}' and '{file}'");
                }
                set.pathsByNamespace[model.Namespace] = file;

                if (!string.IsNullOrWhiteSpace(model.Name))
                {
                    if (set.pathsByName.TryGetValue(model.Name, out var existingName) &&
                        !string.Equals(existingName, file, StringComparison.OrdinalIgnoreCase))
                    {
                        set.ambiguousNames.Add(model.Name);
                    }
                    else
                    {
                        set.pathsByName[model.Name] = file;
                    }
                }
            }

            set.Errors = errors;
            return set;
        }

        /// <summary>Returns true when a model with the namespace is present.</summary>
        public bool Contains(string modelNamespace) =>
            !string.IsNullOrWhiteSpace(modelNamespace) && pathsByNamespace.ContainsKey(modelNamespace);

        /// <summary>Gets a fresh model instance for a namespace.</summary>
        public DmnModel GetByNamespace(string modelNamespace)
        {
            if (!TryGetByNamespace(modelNamespace, out var model))
                throw new DmnModelSetException($"No model with namespace '{modelNamespace}' in '{Directory}'");
            return model;
        }

        /// <summary>Tries to get a fresh model instance for a namespace.</summary>
        public bool TryGetByNamespace(string modelNamespace, out DmnModel model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(modelNamespace) ||
                !pathsByNamespace.TryGetValue(modelNamespace, out var path))
                return false;
            model = Load(path);
            return true;
        }

        /// <summary>Gets a model by <c>definitions/@name</c>, when the name is unambiguous.</summary>
        public DmnModel GetByName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName) ||
                !pathsByName.TryGetValue(modelName, out var path))
                throw new DmnModelSetException($"No model named '{modelName}' in '{Directory}'");
            if (ambiguousNames.Contains(modelName))
                throw new DmnModelSetException(
                    $"Model name '{modelName}' is ambiguous; use its namespace instead");
            return Load(path);
        }

        /// <summary>All models in the set, parsed fresh.</summary>
        public IReadOnlyList<DmnModel> GetModels() =>
            pathsByNamespace.Values.OrderBy(p => p, StringComparer.Ordinal).Select(Load).ToList();

        /// <summary>
        /// Creates an executable definition for a namespace or model name. Imports
        /// are resolved inside this set, by namespace (or by locationURI when
        /// present).
        /// </summary>
        public DmnDefinition CreateDefinition(string modelNamespaceOrName)
        {
            DmnModel model;
            if (!TryGetByNamespace(modelNamespaceOrName, out model))
                model = GetByName(modelNamespaceOrName);
            return DmnDefinitionFactory.CreateDmnDefinition(model, this);
        }

        /// <summary>Creates a definition for an already parsed root model.</summary>
        public DmnDefinition CreateDefinition(DmnModel root) =>
            DmnDefinitionFactory.CreateDmnDefinition(root, this);

        internal bool TryLoadByNamespace(string modelNamespace, out DmnModel model) =>
            TryGetByNamespace(modelNamespace, out model);

        internal DmnModel LoadByPath(string path)
        {
            var full = Path.GetFullPath(path);
            if (pathsByNamespace.Values.Any(p => string.Equals(Path.GetFullPath(p), full, StringComparison.OrdinalIgnoreCase)))
                return Load(full);
            return DmnParser.ParseAutoDetect(full);
        }

        private static DmnModel Load(string path)
        {
            var model = DmnParser.ParseAutoDetect(path);
            model.SourcePath = path;
            return model;
        }
    }

    /// <summary>A non-fatal model-set loading problem.</summary>
    public sealed record DmnModelSetError(string Path, string Message);

    /// <summary>Raised when a model set cannot be loaded or resolved.</summary>
    public sealed class DmnModelSetException : Exception
    {
        public DmnModelSetException(string message) : base(message) { }
        public DmnModelSetException(string message, Exception innerException) : base(message, innerException) { }
    }
}
