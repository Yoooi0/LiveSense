using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    public interface IScriptCompiler : IDisposable
    {
        public string Compile(string source, out IScript instance);
        public void Dispose(IScript instance);
    }

    public class ScriptCompiler : IScriptCompiler
    {
        private readonly ConcurrentDictionary<IScript, AssemblyLoadContext> _scriptContexts;

        public ScriptCompiler()
        {
            _scriptContexts = new ConcurrentDictionary<IScript, AssemblyLoadContext>();
        }

        public string Compile(string source, out IScript instance)
        {
            static string GetFullScriptSource(string source)
            {
                source = Regex.Replace(source, @"\(time,\s*axis\)", "public float? Evaluate(float time, DeviceAxis axis)");

                return @$"
                using System;
                using System.Runtime;
                using LiveSense.Common;
                using LiveSense.MotionSource.TipMenu.ViewModels;
                using static LiveSense.Common.DeviceAxis;
                using static LiveSense.Common.MathUtils;
                using static System.MathF;

                namespace LiveSense.Script
                {{
                    public class Script : IScript
                    {{
                        {source}
                    }}
                }}";
            }

            static string GetCompilationOutput(Stopwatch stopwatch, params string[] lines)
                => string.Join("\n", lines.Append($"Completed in {stopwatch.Elapsed.TotalSeconds}s")
                                            .Select(l => $"[{DateTime.Now.ToLongTimeString()}] {l}"));

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            instance = null;

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(GetFullScriptSource(source));

                var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                var references = new MetadataReference[]
                {
                    MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(typeof(IScript).Assembly.Location),
                };

                var random = new Random();
                var randomId = string.Concat(Enumerable.Range(0, 5).Select(_ => $"{random.Next(0, 15):x}"));
                var dateId = $"{(long)(DateTime.Now - DateTime.UnixEpoch).TotalMilliseconds:x}";

                var assemblyName = $"Script_{dateId}{randomId}";
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees: new[] { syntaxTree },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var stream = new MemoryStream();
                var emitResult = compilation.Emit(stream);

                if (!emitResult.Success)
                {
                    var diagnostics = emitResult.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    return GetCompilationOutput(stopwatch, string.Join(Environment.NewLine, diagnostics));
                }

                stream.Seek(0, SeekOrigin.Begin);
                var context = new CollectibleAssemblyLoadContext();
                var assembly = context.LoadFromStream(stream);
                var type = assembly.GetExportedTypes().FirstOrDefault(t => t.GetInterface("IScript") != null);

                instance = Activator.CreateInstance(type) as IScript;
                _scriptContexts.TryAdd(instance, context);

                return GetCompilationOutput(stopwatch, "Compilation success");
            }
            catch (Exception e)
            {
                return GetCompilationOutput(stopwatch, e.ToString());
            }
        }

        public void Dispose(IScript instance)
        {
            if (instance == null)
                return;

            if (_scriptContexts.TryRemove(instance, out var context))
            {
                var reference = new WeakReference(context);
                context.Unload();
                context = null;

                for (var i = 0; reference.IsAlive && i < 10; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            foreach (var (_, context) in _scriptContexts)
                context.Unload();

            _scriptContexts.Clear();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public class CollectibleAssemblyLoadContext : AssemblyLoadContext
        {
            public CollectibleAssemblyLoadContext()
                : base(isCollectible: true) { }

            protected override Assembly Load(AssemblyName assemblyName) => null;
        }
    }
}