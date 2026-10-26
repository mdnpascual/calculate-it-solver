// Drift guard for GameStubs/: every public type and declared public member of the stub assemblies must exist in the
// installed game's assembly with the same kind, signature, static-ness, base type and declaring type.
// Usage: StubCheck --game <Managed dir> --stubs <dir>[;<dir>...] [--dump Type1,Type2]
// Exit code 0: no differences. 1: differences (printed). 2: bad arguments or missing files.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

static class Program
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly string[] StubAssemblies = { "Assembly-CSharp", "UnityEngine.UI" };

    static int Main(string[] args)
    {
        string game = Arg(args, "--game"), stubs = Arg(args, "--stubs"), dump = Arg(args, "--dump");
        if (game == null || !Directory.Exists(game)) return Fail("--game <Managed dir> is required and must exist");
        using var real = Context(game);

        if (dump != null)
        {
            var asm = real.LoadFromAssemblyName("Assembly-CSharp");
            foreach (var name in dump.Split(','))
            {
                var t = asm.GetType(name.Trim());
                if (t == null) { Console.WriteLine("== " + name + " NOT FOUND"); continue; }
                Console.WriteLine("== " + t.FullName + " : " + Safe(() => t.BaseType.FullName));
                foreach (var m in t.GetMembers(Declared).Where(IsApi).OrderBy(m => m.MemberType).ThenBy(m => m.Name))
                    Console.WriteLine("   " + Sig(m));
            }
            return 0;
        }

        var stubDirs = (stubs ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (stubDirs.Length == 0 || stubDirs.Any(d => !Directory.Exists(d))) return Fail("--stubs <dir>[;<dir>...] is required and every directory must exist");
        using var fake = Context(game, stubDirs);
        int diffs = 0, types = 0, members = 0;
        foreach (var name in StubAssemblies)
        {
            if (!stubDirs.Any(d => File.Exists(Path.Combine(d, name + ".dll")))) return Fail("stub assembly not found (build GameStubs first): " + name + ".dll");
            var stub = fake.LoadFromAssemblyName(name);
            var gameAsm = real.LoadFromAssemblyName(name);
            foreach (var st in stub.GetTypes().Where(t => t.IsPublic || t.IsNestedPublic))
            {
                types++;
                var rt = gameAsm.GetType(st.FullName);
                if (rt == null) { Console.WriteLine($"MISSING TYPE   {name}: {st.FullName}"); diffs++; continue; }
                if (Safe(() => st.BaseType?.FullName) != Safe(() => rt.BaseType?.FullName))
                { Console.WriteLine($"BASE TYPE      {st.FullName}: stub {Safe(() => st.BaseType?.FullName)}, game {Safe(() => rt.BaseType?.FullName)}"); diffs++; }
                var gameSigs = new HashSet<string>(rt.GetMembers(Declared).Where(IsApi).Select(Sig));
                foreach (var sm in st.GetMembers(Declared).Where(IsApi))
                {
                    members++;
                    string s = Sig(sm);
                    if (gameSigs.Contains(s)) continue;
                    var near = rt.GetMembers(Declared).Where(m => m.Name == sm.Name).Select(Sig).ToList();
                    Console.WriteLine($"DIFFERENT      {st.FullName}: stub `{s}`, game {(near.Count == 0 ? "has no declared member of that name (inherited, renamed or gone)" : "`" + string.Join("` / `", near) + "`")}");
                    diffs++;
                }
            }
        }
        Console.WriteLine($"checked {types} types, {members} members: {(diffs == 0 ? "no differences" : diffs + " difference(s)")}");
        return diffs == 0 ? 0 : 1;
    }

    // Stubs resolve their own assemblies first, then the game's Managed folder (mscorlib, UnityEngine*).
    static MetadataLoadContext Context(string managed, params string[] stubDirs)
    {
        var paths = new List<string>();
        foreach (var d in stubDirs) paths.AddRange(Directory.GetFiles(d, "*.dll"));
        // A stub replaces the game's file of the same name; two assemblies with one name can't share a context.
        paths.AddRange(Directory.GetFiles(managed, "*.dll").Where(f => !paths.Any(p => string.Equals(Path.GetFileName(p), Path.GetFileName(f), StringComparison.OrdinalIgnoreCase))).ToList());
        return new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
    }

    static bool IsApi(MemberInfo m)
    {
        switch (m)
        {
            case FieldInfo f: return f.IsPublic;
            case ConstructorInfo: return false;   // constructors aren't part of the contract the mod uses
            case MethodInfo mi: return !mi.IsSpecialName && (mi.IsPublic || mi.IsFamily);
            case PropertyInfo p: return (p.GetMethod ?? p.SetMethod)?.IsPublic == true;
            default: return false;
        }
    }

    static string Sig(MemberInfo m)
    {
        switch (m)
        {
            case FieldInfo f: return $"field {(f.IsStatic ? "static " : "")}{TypeName(f.FieldType)} {f.Name}";
            case PropertyInfo p:
                return $"property {((p.GetMethod ?? p.SetMethod).IsStatic ? "static " : "")}{TypeName(p.PropertyType)} {p.Name} {{{(p.GetMethod != null ? "get;" : "")}{(p.SetMethod != null ? "set;" : "")}}}";
            case MethodInfo mi:
                return $"method {(mi.IsStatic ? "static " : "")}{TypeName(mi.ReturnType)} {mi.Name}({string.Join(", ", mi.GetParameters().Select(x => TypeName(x.ParameterType)))})";
            case ConstructorInfo c: return $"ctor({string.Join(", ", c.GetParameters().Select(x => TypeName(x.ParameterType)))})";
            default: return m.ToString();
        }
    }

    static string TypeName(Type t) => Safe(() => t.ToString());
    static string Safe(Func<string> f) { try { return f(); } catch (Exception e) { return "?" + e.GetType().Name; } }
    static string Arg(string[] a, string k) { int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
    static int Fail(string m) { Console.Error.WriteLine(m); return 2; }
}
