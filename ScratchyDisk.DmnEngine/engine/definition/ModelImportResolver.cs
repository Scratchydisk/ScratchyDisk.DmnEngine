using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// Flattens a DMN model and its imports into a single model. Imported elements
    /// are renamed with a unique prefix; each decision/BKM carries the alias map
    /// needed to resolve the names used in its own model. Requirement hrefs and
    /// typeRefs are rewritten to the flattened ids/names.
    /// </summary>
    public static class ModelImportResolver
    {
        public static void Resolve(DmnModel root, DmnModelSet models = null)
        {
            if (root == null || root.Imports == null || root.Imports.Count == 0) return;
            if (root.ImportAliases != null) return;

            var resolver = new Resolver(root, models);
            resolver.Run();
        }

        private sealed class ImportBinding
        {
            public string Name;
            public string Namespace;
            public Resolver.Entry Entry;
        }

        private sealed class Resolver
        {
            private readonly DmnModel root;
            private readonly DmnModelSet models;
            private readonly Dictionary<string, Entry> byNamespace = new(StringComparer.Ordinal);
            private readonly Dictionary<string, DmnModel> modelCache = new(StringComparer.OrdinalIgnoreCase);
            private int prefixCounter;

            public Resolver(DmnModel root, DmnModelSet models)
            {
                this.root = root;
                this.models = models;
            }

            internal sealed class Entry
            {
                public DmnModel Model;
                public string Namespace;
                public string NamePrefix;
                public string IdPrefix;
                public bool ImportsResolved;
                public readonly Dictionary<string, string> IdMap = new(StringComparer.Ordinal);
                public readonly Dictionary<string, string> ElementNames = new(StringComparer.Ordinal);
                public readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase);
                public readonly List<string> InputNames = new();
                public readonly List<string> DecisionNames = new();
                public readonly List<string> BkmNames = new();
                public readonly List<ImportBinding> Imports = new();
            }

            public void Run()
            {
                var rootEntry = new Entry
                {
                    Model = root,
                    Namespace = root.Namespace ?? "",
                    NamePrefix = "",
                    IdPrefix = "",
                };
                byNamespace[rootEntry.Namespace] = rootEntry;
                ResolveImports(rootEntry);

                var entries = Flatten(rootEntry);
                BuildNameMaps(entries);
                MergeIntoRoot(entries);
                RewriteReferencesAndTypes(entries);
                BuildAliases(entries, rootEntry);
            }

            private void ResolveImports(Entry entry)
            {
                if (entry.ImportsResolved) return;
                entry.ImportsResolved = true;
                if (entry.Model.Imports == null) return;

                foreach (var import in entry.Model.Imports)
                {
                    var importNamespace = import.Namespace ?? "";
                    if (!byNamespace.TryGetValue(importNamespace, out var target))
                    {
                        var model = Load(entry, import);
                        if (model == null) continue;

                        var ns = model.Namespace ?? importNamespace;
                        if (!byNamespace.TryGetValue(ns, out target))
                        {
                            prefixCounter++;
                            target = new Entry
                            {
                                Model = model,
                                Namespace = ns,
                                NamePrefix = $"m{prefixCounter}.",
                                IdPrefix = $"m{prefixCounter}#",
                            };
                            byNamespace[ns] = target;
                            ResolveImports(target);
                        }
                    }

                    entry.Imports.Add(new ImportBinding
                    {
                        Name = import.Name ?? "",
                        Namespace = target.Namespace,
                        Entry = target,
                    });
                }
            }

            private DmnModel Load(Entry importer, Import import)
            {
                if (models != null)
                {
                    if (!string.IsNullOrWhiteSpace(import.LocationPath) && importer.Model.SourcePath != null)
                    {
                        var modelDirectory = Path.GetDirectoryName(importer.Model.SourcePath);
                        var modelLocation = import.LocationPath;
                        var modelPath = Path.IsPathRooted(modelLocation)
                            ? modelLocation
                            : Path.Combine(modelDirectory, modelLocation);
                        if (File.Exists(modelPath)) return models.LoadByPath(modelPath);
                    }
                    if (models.TryLoadByNamespace(import.Namespace, out var indexed))
                        return indexed;
                }

                var baseDirectory = importer.Model.SourcePath == null
                    ? null
                    : Path.GetDirectoryName(importer.Model.SourcePath);

                var location = import.LocationPath;
                if (!string.IsNullOrWhiteSpace(location) && baseDirectory != null)
                {
                    var path = Path.IsPathRooted(location) ? location : Path.Combine(baseDirectory, location);
                    if (File.Exists(path)) return LoadFile(path);
                }

                if (baseDirectory == null) return null;
                foreach (var candidate in Directory.GetFiles(baseDirectory, "*.dmn"))
                {
                    if (string.Equals(candidate, importer.Model.SourcePath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var model = LoadFile(candidate);
                    if (model != null && string.Equals(model.Namespace, import.Namespace, StringComparison.Ordinal))
                        return model;
                }
                return null;
            }

            private DmnModel LoadFile(string path)
            {
                if (modelCache.TryGetValue(path, out var cached)) return cached;
                try
                {
                    var model = DmnParser.ParseAutoDetect(path);
                    model.SourcePath = path;
                    modelCache[path] = model;
                    return model;
                }
                catch
                {
                    return null;
                }
            }

            private List<Entry> Flatten(Entry rootEntry)
            {
                var entries = new List<Entry> { rootEntry };
                var seen = new HashSet<Entry> { rootEntry };
                var queue = new Queue<Entry>();
                queue.Enqueue(rootEntry);
                while (queue.Count > 0)
                {
                    foreach (var import in queue.Dequeue().Imports)
                    {
                        if (seen.Add(import.Entry))
                        {
                            entries.Add(import.Entry);
                            queue.Enqueue(import.Entry);
                        }
                    }
                }
                return entries;
            }

            private void BuildNameMaps(List<Entry> entries)
            {
                foreach (var entry in entries)
                {
                    foreach (var input in entry.Model.InputData ?? Enumerable.Empty<InputData>())
                    {
                        var name = input.Name ?? input.Id ?? "";
                        var global = GlobalName(entry, name);
                        entry.ElementNames[name] = global;
                        entry.InputNames.Add(name);
                        if (!string.IsNullOrWhiteSpace(input.Id))
                            entry.IdMap[input.Id] = GlobalId(entry, input.Id);
                    }
                    foreach (var decision in entry.Model.Decisions ?? Enumerable.Empty<Decision>())
                    {
                        var name = decision.Name ?? decision.Id ?? "";
                        var global = GlobalName(entry, name);
                        entry.ElementNames[name] = global;
                        entry.DecisionNames.Add(name);
                        if (!string.IsNullOrWhiteSpace(decision.Id))
                            entry.IdMap[decision.Id] = GlobalId(entry, decision.Id);
                    }
                    foreach (var bkm in entry.Model.BusinessKnowledgeModels ?? Enumerable.Empty<BusinessKnowledgeModel>())
                    {
                        var name = bkm.Name ?? bkm.Id ?? "";
                        entry.ElementNames[name] = GlobalName(entry, name);
                        entry.BkmNames.Add(name);
                        if (!string.IsNullOrWhiteSpace(bkm.Id))
                            entry.IdMap[bkm.Id] = GlobalId(entry, bkm.Id);
                    }
                    foreach (var item in entry.Model.ItemDefinitions ?? Enumerable.Empty<ItemDefinition>())
                    {
                        var name = item.Name ?? item.Id ?? "";
                        entry.TypeNames[name] = GlobalName(entry, name);
                        if (!string.IsNullOrWhiteSpace(item.Id))
                            entry.IdMap[item.Id] = GlobalId(entry, item.Id);
                    }
                }
            }

            private void MergeIntoRoot(List<Entry> entries)
            {
                root.InputData ??= new List<InputData>();
                root.Decisions ??= new List<Decision>();
                root.BusinessKnowledgeModels ??= new List<BusinessKnowledgeModel>();
                root.ItemDefinitions ??= new List<ItemDefinition>();

                foreach (var entry in entries.Where(e => e.NamePrefix.Length > 0))
                {
                    foreach (var input in entry.Model.InputData ?? Enumerable.Empty<InputData>())
                    {
                        input.Name = GlobalName(entry, input.Name ?? input.Id ?? "");
                        input.Id = GlobalId(entry, input.Id ?? input.Name);
                        root.InputData.Add(input);
                    }
                    foreach (var decision in entry.Model.Decisions ?? Enumerable.Empty<Decision>())
                    {
                        decision.Name = GlobalName(entry, decision.Name ?? decision.Id ?? "");
                        decision.Id = GlobalId(entry, decision.Id ?? decision.Name);
                        if (decision.OutputVariable != null) decision.OutputVariable.Name = decision.Name;
                        root.Decisions.Add(decision);
                    }
                    foreach (var bkm in entry.Model.BusinessKnowledgeModels ?? Enumerable.Empty<BusinessKnowledgeModel>())
                    {
                        bkm.Name = GlobalName(entry, bkm.Name ?? bkm.Id ?? "");
                        bkm.Id = GlobalId(entry, bkm.Id ?? bkm.Name);
                        root.BusinessKnowledgeModels.Add(bkm);
                    }
                    foreach (var item in entry.Model.ItemDefinitions ?? Enumerable.Empty<ItemDefinition>())
                    {
                        item.Name = GlobalName(entry, item.Name ?? item.Id ?? "");
                        item.Id = GlobalId(entry, item.Id ?? item.Name);
                        root.ItemDefinitions.Add(item);
                    }
                }
            }

            private void RewriteReferencesAndTypes(List<Entry> entries)
            {
                foreach (var entry in entries)
                {
                    foreach (var decision in entry.Model.Decisions ?? Enumerable.Empty<Decision>())
                    {
                        RewriteDecision(decision, entry);
                    }
                    foreach (var bkm in entry.Model.BusinessKnowledgeModels ?? Enumerable.Empty<BusinessKnowledgeModel>())
                    {
                        if (bkm.OutputVariable != null)
                            bkm.OutputVariable.TypeRef = ResolveType(bkm.OutputVariable.TypeRef, entry);
                        var logic = bkm.EncapsulatedLogic;
                        if (logic != null)
                        {
                            foreach (var parameter in logic.FormalParameters ?? Enumerable.Empty<Variable>())
                                parameter.TypeRef = ResolveType(parameter.TypeRef, entry);
                            if (logic.Expression != null)
                                logic.Expression.TypeRef = ResolveType(logic.Expression.TypeRef, entry);
                        }
                    }
                    foreach (var item in entry.Model.ItemDefinitions ?? Enumerable.Empty<ItemDefinition>())
                    {
                        item.TypeRefElement = ResolveType(item.TypeRefElement, entry);
                        item.TypeRefAttribute = ResolveType(item.TypeRefAttribute, entry);
                        foreach (var component in item.ItemComponents ?? Enumerable.Empty<ItemComponent>())
                        {
                            component.TypeRefElement = ResolveType(component.TypeRefElement, entry);
                            component.TypeRefAttribute = ResolveType(component.TypeRefAttribute, entry);
                        }
                    }
                }
            }

            private void RewriteDecision(Decision decision, Entry entry)
            {
                if (decision.OutputVariable != null)
                    decision.OutputVariable.TypeRef = ResolveType(decision.OutputVariable.TypeRef, entry);
                foreach (var requirement in decision.InformationRequirements ?? Enumerable.Empty<InformationRequirement>())
                {
                    var resolved = ResolveRef(requirement.RawRef, entry);
                    if (resolved != null) requirement.OverrideRef = resolved;
                }
                foreach (var knowledge in decision.KnowledgeRequirements ?? Enumerable.Empty<KnowledgeRequirement>())
                {
                    if (knowledge.Reference?.Ref != null)
                        knowledge.Reference.Ref = ResolveRef(knowledge.Reference.Ref, entry) ?? knowledge.Reference.Ref;
                }
                var table = decision.DecisionTable;
                if (table == null) return;
                foreach (var input in table.Inputs ?? Enumerable.Empty<DecisionTableInput>())
                {
                    if (input.InputExpression != null)
                        input.InputExpression.TypeRef = ResolveType(input.InputExpression.TypeRef, entry);
                }
                foreach (var output in table.Outputs ?? Enumerable.Empty<DecisionTableOutput>())
                {
                    output.TypeRef = ResolveType(output.TypeRef, entry);
                    output.Name = GlobalNameForOutput(entry, output.Name ?? output.Label ?? output.Id ?? "");
                }
                foreach (var rule in table.Rules ?? Enumerable.Empty<DecisionRule>())
                {
                    foreach (var outputEntry in rule.OutputEntries ?? Enumerable.Empty<Expression>())
                        outputEntry.TypeRef = ResolveType(outputEntry.TypeRef, entry);
                }
            }

            private string GlobalNameForOutput(Entry entry, string localName) =>
                entry.NamePrefix.Length == 0 ? localName : GlobalName(entry, localName);

            private string ResolveRef(string reference, Entry entry)
            {
                if (string.IsNullOrWhiteSpace(reference)) return reference;
                if (reference.StartsWith("#"))
                {
                    var local = reference[1..];
                    return entry.IdMap.TryGetValue(local, out var global) ? global : reference;
                }

                var hash = reference.IndexOf('#');
                if (hash > 0)
                {
                    var ns = reference[..hash];
                    var id = reference[(hash + 1)..];
                    if (string.Equals(ns, entry.Namespace, StringComparison.Ordinal))
                        return entry.IdMap.TryGetValue(id, out var localGlobal) ? localGlobal : reference;
                    if (byNamespace.TryGetValue(ns, out var imported) && imported.IdMap.TryGetValue(id, out var importedGlobal))
                        return importedGlobal;
                    return reference;
                }

                return entry.IdMap.TryGetValue(reference, out var bare) ? bare : reference;
            }

            private string ResolveType(string typeRef, Entry entry)
            {
                if (string.IsNullOrWhiteSpace(typeRef)) return typeRef;
                if (entry.TypeNames.TryGetValue(typeRef, out var local)) return local;

                var dot = typeRef.IndexOf('.');
                if (dot > 0)
                {
                    var prefix = typeRef[..dot];
                    var localName = typeRef[(dot + 1)..];
                    var binding = entry.Imports.FirstOrDefault(b => b.Name == prefix);
                    if (binding != null && binding.Entry.TypeNames.TryGetValue(localName, out var imported))
                        return imported;
                }

                foreach (var binding in entry.Imports.Where(b => b.Name.Length == 0))
                {
                    if (binding.Entry.TypeNames.TryGetValue(typeRef, out var imported))
                        return imported;
                }
                return typeRef;
            }

            private int safeAliasCounter;

            private void BuildAliases(List<Entry> entries, Entry rootEntry)
            {
                var aliases = new ModelImportAliases();

                foreach (var entry in entries)
                {
                    var localAliases = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in entry.ElementNames)
                        localAliases[pair.Key] = pair.Value;

                    foreach (var binding in entry.Imports)
                    {
                        foreach (var pair in binding.Entry.ElementNames)
                        {
                            if (binding.Name.Length == 0)
                            {
                                localAliases.TryAdd(pair.Key, pair.Value);
                            }
                            else
                            {
                                localAliases[$"{binding.Name}.{pair.Key}"] = pair.Value;
                            }
                        }
                    }

                    // FEEL names cannot contain dots inside a word, and the name
                    // resolver cannot merge across a '.'; give spaced aliases a
                    // safe identifier and rewrite them in this model's expressions.
                    var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var alias in localAliases.Keys.Where(k => k.Contains(' '))
                                 .OrderByDescending(k => k.Length).ToList())
                    {
                        var safe = $"__imp_{++safeAliasCounter}";
                        replacements[alias] = safe;
                        localAliases[safe] = localAliases[alias];
                        localAliases.Remove(alias);
                    }
                    if (replacements.Count > 0)
                        RewriteExpressions(entry.Model, replacements);

                    foreach (var local in entry.DecisionNames)
                    {
                        var global = entry.ElementNames[local];
                        aliases.DecisionAliases[global] = localAliases;
                    }
                    foreach (var local in entry.BkmNames)
                    {
                        var global = entry.ElementNames[local];
                        aliases.BkmAliases[global] = localAliases;
                    }

                    if (entry.NamePrefix.Length > 0)
                    {
                        foreach (var local in entry.DecisionNames)
                            aliases.QualifiedDecisionNames[$"{entry.Namespace}#{local}"] = entry.ElementNames[local];
                    }
                }

                foreach (var entry in entries)
                {
                    foreach (var local in entry.InputNames)
                    {
                        var global = entry.ElementNames[local];
                        aliases.InputAliases[$"{entry.Namespace}#{local}"] = global;
                        aliases.QualifiedInputNames[$"{entry.Namespace}#{local}"] = global;
                        if (!aliases.InputAliases.ContainsKey(local))
                            aliases.InputAliases[local] = global;
                    }
                }

                // Root decisions are addressed by their plain names.
                foreach (var local in rootEntry.DecisionNames)
                    aliases.QualifiedDecisionNames.TryAdd($"{rootEntry.Namespace}#{local}", local);

                root.ImportAliases = aliases;
            }

            private static void RewriteExpressions(DmnModel model, IReadOnlyDictionary<string, string> replacements)
            {
                string Rewrite(string text)
                {
                    if (string.IsNullOrEmpty(text) || replacements.Count == 0) return text;

                    var ordered = replacements.OrderByDescending(p => p.Key.Length).ToList();
                    var builder = new System.Text.StringBuilder(text.Length);
                    for (var i = 0; i < text.Length;)
                    {
                        if (text[i] == '"')
                        {
                            builder.Append(text[i++]);
                            while (i < text.Length)
                            {
                                var c = text[i++];
                                builder.Append(c);
                                if (c == '\\' && i < text.Length) { builder.Append(text[i++]); continue; }
                                if (c == '"') break;
                            }
                            continue;
                        }

                        var replaced = false;
                        foreach (var pair in ordered)
                        {
                            if (i + pair.Key.Length > text.Length) continue;
                            if (string.CompareOrdinal(text, i, pair.Key, 0, pair.Key.Length) != 0) continue;
                            builder.Append(pair.Value);
                            i += pair.Key.Length;
                            replaced = true;
                            break;
                        }
                        if (!replaced) builder.Append(text[i++]);
                    }
                    return builder.ToString();
                }

                foreach (var decision in model.Decisions ?? Enumerable.Empty<Decision>())
                {
                    if (decision.Expression != null) decision.Expression.Text = Rewrite(decision.Expression.Text);
                    RewriteTable(decision.DecisionTable, Rewrite);
                    RewriteBoxed(decision.Context, decision.Relation, decision.List,
                        decision.Invocation, decision.FunctionDefinition, Rewrite);
                }
                foreach (var bkm in model.BusinessKnowledgeModels ?? Enumerable.Empty<BusinessKnowledgeModel>())
                {
                    var logic = bkm.EncapsulatedLogic;
                    if (logic == null) continue;
                    if (logic.Expression != null) logic.Expression.Text = Rewrite(logic.Expression.Text);
                    RewriteTable(logic.DecisionTable, Rewrite);
                    RewriteBoxed(logic.Context, logic.Relation, logic.List,
                        logic.Invocation, null, Rewrite);
                }
            }

            private static void RewriteBoxed(
                ContextExpression context,
                RelationExpression relation,
                ListExpression list,
                InvocationExpression invocation,
                FunctionDefinition function,
                Func<string, string> rewrite)
            {
                if (context != null)
                {
                    foreach (var entry in context.Entries ?? Enumerable.Empty<ContextEntry>())
                    {
                        if (entry.Expression != null) entry.Expression.Text = rewrite(entry.Expression.Text);
                        RewriteTable(entry.DecisionTable, rewrite);
                        RewriteBoxed(entry.Context, entry.Relation, entry.List, entry.Invocation,
                            entry.FunctionDefinition, rewrite);
                    }
                }

                if (relation != null)
                {
                    foreach (var row in relation.Rows ?? Enumerable.Empty<RelationRow>())
                        foreach (var expression in row.Expressions ?? new List<Expression>())
                            expression.Text = rewrite(expression.Text);
                }

                if (list != null)
                {
                    foreach (var expression in list.Expressions ?? new List<Expression>())
                        expression.Text = rewrite(expression.Text);
                }

                if (invocation != null)
                {
                    if (invocation.Function != null) invocation.Function.Text = rewrite(invocation.Function.Text);
                    foreach (var binding in invocation.Bindings ?? Enumerable.Empty<InvocationBinding>())
                        if (binding.Expression != null) binding.Expression.Text = rewrite(binding.Expression.Text);
                }

                if (function == null) return;
                if (function.Expression != null) function.Expression.Text = rewrite(function.Expression.Text);
                RewriteTable(function.DecisionTable, rewrite);
                RewriteBoxed(function.Context, function.Relation, function.List, function.Invocation,
                    null, rewrite);
            }

            private static void RewriteTable(DecisionTable table, Func<string, string> rewrite)
            {
                if (table == null) return;
                foreach (var input in table.Inputs ?? Enumerable.Empty<DecisionTableInput>())
                    if (input.InputExpression != null) input.InputExpression.Text = rewrite(input.InputExpression.Text);
                foreach (var output in table.Outputs ?? Enumerable.Empty<DecisionTableOutput>())
                {
                    if (output.DefaultOutputEntry != null)
                        output.DefaultOutputEntry.Text = rewrite(output.DefaultOutputEntry.Text);
                    if (output.DefaultText != null) output.DefaultText = rewrite(output.DefaultText);
                }
                foreach (var rule in table.Rules ?? Enumerable.Empty<DecisionRule>())
                {
                    foreach (var entry in rule.InputEntries ?? Enumerable.Empty<Expression>())
                        entry.Text = rewrite(entry.Text);
                    foreach (var entry in rule.OutputEntries ?? Enumerable.Empty<Expression>())
                        entry.Text = rewrite(entry.Text);
                }
            }

            private static string GlobalName(Entry entry, string local) =>
                entry.NamePrefix.Length == 0 ? local : entry.NamePrefix + local;

            private static string GlobalId(Entry entry, string local) =>
                entry.IdPrefix.Length == 0 ? local : entry.IdPrefix + local;
        }
    }
}
