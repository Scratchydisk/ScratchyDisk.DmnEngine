using System;
using System.Collections.Generic;
using System.Linq;

namespace ScratchyDisk.DmnEngine.Feel.Types
{
    /// <summary>
    /// FEEL function wrapper that supports both positional and named parameter invocation.
    /// </summary>
    public sealed class FeelFunction
    {
        /// <summary>
        /// The ordered list of parameter names for this function
        /// </summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>
        /// Declared FEEL types of the parameters, parallel to <see cref="ParameterNames"/>.
        /// A null (or missing) entry means the parameter is untyped ("any").
        /// </summary>
        public IReadOnlyList<string> ParameterTypes { get; }

        /// <summary>
        /// Named item type definitions used to resolve declared parameter types.
        /// </summary>
        public IReadOnlyDictionary<string, string> TypeAliases { get; }

        /// <summary>
        /// The underlying callable that takes an array of positional arguments and returns a result
        /// </summary>
        private Func<object[], object> Body { get; }

        /// <summary>
        /// True for functions declared in a DMN model: their formal parameter list
        /// is fixed, so a call with a different number of arguments is invalid.
        /// Built-in functions may accept optional arguments and leave this false.
        /// </summary>
        public bool ValidateArity { get; }

        /// <summary>
        /// Display name of this function (for debugging/error messages)
        /// </summary>
        public string Name { get; }

        public FeelFunction(string name, IReadOnlyList<string> parameterNames, Func<object[], object> body,
            IReadOnlyList<string> parameterTypes = null,
            IReadOnlyDictionary<string, string> typeAliases = null,
            bool validateArity = false)
        {
            Name = name ?? "anonymous";
            ParameterNames = parameterNames ?? Array.Empty<string>();
            ParameterTypes = parameterTypes;
            TypeAliases = typeAliases;
            ValidateArity = validateArity;
            Body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <summary>
        /// Invokes the function with positional arguments
        /// </summary>
        public object Invoke(params object[] args)
        {
            args ??= Array.Empty<object>();

            // A model-defined FEEL function has a fixed formal parameter list:
            // passing fewer or more arguments than declared makes the invocation
            // invalid.
            if (ValidateArity && args.Length != ParameterNames.Count) return null;

            if (ParameterTypes != null)
            {
                var converted = new object[args.Length];
                for (var i = 0; i < args.Length; i++)
                {
                    var typeName = i < ParameterTypes.Count ? ParameterTypes[i] : null;
                    if (!TryCoerceArgument(args[i], typeName, out converted[i])) return null;
                }

                args = converted;
            }

            return Body(args);
        }

        /// <summary>
        /// Invokes the function with named arguments.
        /// Arguments are reordered to match the declared parameter order.
        /// Missing parameters are passed as null; a name that is not a declared
        /// parameter makes the invocation invalid (DMN function invocation by name).
        /// </summary>
        public object InvokeNamed(IDictionary<string, object> namedArgs)
        {
            if (namedArgs == null) return Invoke();

            if (namedArgs.Keys.Any(key => !ParameterNames.Contains(key, StringComparer.Ordinal)))
                return null;

            var positional = new object[ParameterNames.Count];
            for (var i = 0; i < ParameterNames.Count; i++)
            {
                positional[i] = namedArgs.TryGetValue(ParameterNames[i], out var val) ? val : null;
            }
            return Invoke(positional);
        }

        private bool TryCoerceArgument(object value, string typeName, out object result)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                result = value;
                return true;
            }

            return FeelTypeConformance.TryCoerce(value, typeName, TypeAliases, out result);
        }

        public override string ToString() => $"function({string.Join(", ", ParameterNames)})";
    }
}
