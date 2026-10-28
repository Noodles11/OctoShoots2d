using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OctoShoots.Core.Saves;

/// <summary>
/// The exact state of a level in play, as bytes: every object the world holds, field by field — her, every creature,
/// shot and cloud, the corruption, the random streams mid-sequence — so continuing a run resumes the very moment it was
/// left (docs/DESIGN-TOPDOWN.md §13).
/// <para>
/// A level is regenerated from its seed before it is restored, so some objects already exist on both sides: the
/// <see cref="Anchors"/>, found by walking the same roots in the same order right after the world is made (the world,
/// the run, the level map's whole graph, the item catalog …). They are written by reference and restored in place —
/// whoever holds them still holds them — and those marked kept have their contents saved too (the map's, which the
/// sim changes as rock breaks). Everything else is written in full and made anew.
/// </para>
/// <para>
/// Fields go by name: a field the save has and the type lacks is skipped, one the type has and the save lacks keeps
/// what the fresh level gave it. A type that is gone, or a field whose type changed, throws (the caller then falls
/// back to the level's start).
/// </para>
/// </summary>
public static class Snapshot
{
    const int Version = 1;

    /// <summary>The objects both sides share, in a fixed order, and which of them have their contents saved.</summary>
    public sealed class Anchors
    {
        internal readonly List<object> Objects = new();
        internal readonly List<bool> Kept = new();
        internal readonly Dictionary<object, int> Ids = new(ReferenceEqualityComparer.Instance);

        public int Count => Objects.Count;

        internal void Add(object o, bool kept)
        {
            if (Ids.ContainsKey(o)) return;
            Ids[o] = Objects.Count;
            Objects.Add(o);
            Kept.Add(kept);
        }
    }

    /// <summary>
    /// The anchors: each root (its contents kept or not), and with <c>deep</c> every object reachable from it too.
    /// Call it right after the world is made, on both sides, with the same roots.
    /// </summary>
    public static Anchors Anchor(IEnumerable<(object? Root, bool Deep, bool Kept)> roots)
    {
        var a = new Anchors();
        foreach (var (root, deep, kept) in roots)
        {
            if (root is null) continue;
            if (!deep)
            {
                a.Add(root, kept);
                continue;
            }
            var stack = new Stack<object>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var o = stack.Pop();
                if (a.Ids.ContainsKey(o)) continue;
                a.Add(o, kept);
                var children = new List<object>();
                Collect(Children(o), children);
                for (int i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
            }
        }
        return a;
    }

    /// <summary>The objects among these (and inside the structs among them).</summary>
    static void Collect(IEnumerable<object?> values, List<object> into)
    {
        foreach (var v in values)
        {
            if (v is null) continue;
            var t = v.GetType();
            if (IsReference(t)) into.Add(v);
            else if (!IsValue(t)) Collect(Children(v), into);
        }
    }

    /// <summary>The level's state now.</summary>
    public static byte[] Take(Anchors anchors)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var w = new BinaryWriter(deflate))
        {
            w.Write(Version);
            w.Write(anchors.Count);
            var writer = new Writer(w, anchors);
            for (int i = 0; i < anchors.Count; i++)
                if (anchors.Kept[i]) writer.Kept(anchors.Objects[i]);
        }
        return buffer.ToArray();
    }

    /// <summary>The level's state as text, a value a line with where it lies: for finding what differs between two.</summary>
    public static string Describe(Anchors anchors)
    {
        var log = new StringWriter();
        using var w = new BinaryWriter(Stream.Null);
        var writer = new Writer(w, anchors) { Log = log };
        for (int i = 0; i < anchors.Count; i++)
            if (anchors.Kept[i]) writer.Logged($"#{i}:{anchors.Objects[i].GetType().Name}", anchors.Objects[i]);
        return log.ToString();
    }

    /// <summary>Puts the level back as it was: into the anchors (made by the same roots as when it was taken).</summary>
    public static void Restore(Anchors anchors, byte[] data)
    {
        using var input = new MemoryStream(data);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var r = new BinaryReader(deflate);
        if (r.ReadInt32() != Version) throw new SaveException("The level's state is from another version");
        if (r.ReadInt32() != anchors.Count) throw new SaveException("The level's state does not fit this level");
        var reader = new Reader(r, anchors);
        for (int i = 0; i < anchors.Count; i++)
            if (anchors.Kept[i]) reader.Kept(anchors.Objects[i]);
        // All of it read (and checked against this build): only now is anything that already exists changed.
        reader.Apply();
    }

    // ── What is written how ───────────────────────────────────────────────────────────────────────────────────────

    enum Kind : byte { Fielded, Array, List, Set, Map, Queue, Stack, Heap, Delegate }

    /// <summary>A priority queue's items with their priorities, in its heap's own order.</summary>
    static IEnumerable<(object? Item, object? Priority)> HeapItems(object heap)
    {
        foreach (var pair in (IEnumerable)heap.GetType().GetProperty("UnorderedItems")!.GetValue(heap)!)
        {
            var t = pair.GetType();
            yield return (t.GetField("Item1")!.GetValue(pair), t.GetField("Item2")!.GetValue(pair));
        }
    }

    static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();

    static FieldInfo[] Fields(Type t)
    {
        lock (FieldCache)
        {
            if (FieldCache.TryGetValue(t, out var cached)) return cached;
            var list = new List<FieldInfo>();
            var seen = new HashSet<string>();
            for (var at = t; at is not null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
                foreach (var f in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (seen.Add(f.Name)) list.Add(f);
            return FieldCache[t] = list.ToArray();
        }
    }

    static bool IsValue(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

    static bool IsReference(Type t) => !t.IsValueType && t != typeof(string);

    static Kind KindOf(Type t)
    {
        if (t.IsArray) return Kind.Array;
        if (typeof(Delegate).IsAssignableFrom(t)) return Kind.Delegate;
        if (t.IsGenericType)
        {
            var g = t.GetGenericTypeDefinition();
            if (g == typeof(List<>)) return Kind.List;
            if (g == typeof(HashSet<>)) return Kind.Set;
            if (g == typeof(Dictionary<,>)) return Kind.Map;
            if (g == typeof(Queue<>)) return Kind.Queue;
            if (g == typeof(Stack<>)) return Kind.Stack;
            if (g == typeof(PriorityQueue<,>)) return Kind.Heap;
        }
        if (t.Namespace is { } ns && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)) && !t.IsValueType)
            throw new SaveException($"The level's state cannot hold a {t}");
        return Kind.Fielded;
    }

    /// <summary>What an object holds (for walking the anchors).</summary>
    static IEnumerable<object?> Children(object o)
    {
        var t = o.GetType();
        switch (KindOf(t))
        {
            case Kind.Array:
                if (t.GetElementType()!.IsPrimitive) yield break;
                foreach (var x in (Array)o) yield return x;
                break;
            case Kind.List or Kind.Set or Kind.Queue or Kind.Stack:
                foreach (var x in (IEnumerable)o) yield return x;
                break;
            case Kind.Heap:
                foreach (var (item, priority) in HeapItems(o))
                {
                    yield return item;
                    yield return priority;
                }
                break;
            case Kind.Map:
                foreach (DictionaryEntry e in (IDictionary)o)
                {
                    yield return e.Key;
                    yield return e.Value;
                }
                break;
            case Kind.Delegate:
                yield return ((Delegate)o).Target;
                break;
            default:
                foreach (var f in Fields(t)) yield return f.GetValue(o);
                break;
        }
    }

    enum Tag : byte { Null, Ref, Value, New }

    sealed class Writer
    {
        readonly BinaryWriter _w;
        readonly Dictionary<object, int> _ids;
        int _next;
        readonly Dictionary<Type, int> _types = new();

        public Writer(BinaryWriter w, Anchors anchors)
        {
            _w = w;
            _ids = new Dictionary<object, int>(anchors.Ids, ReferenceEqualityComparer.Instance);
            _next = anchors.Count;
        }

        /// <summary>When set: every value written, by where it lies (<see cref="Describe"/>).</summary>
        public TextWriter? Log;
        readonly List<string> _path = new();

        void At(string where, object? v)
        {
            if (Log is null)
            {
                Value(v);
                return;
            }
            _path.Add(where);
            Value(v);
            _path.RemoveAt(_path.Count - 1);
        }

        public void Logged(string where, object o)
        {
            _path.Clear();
            _path.Add(where);
            Kept(o);
        }

        void Note(string what) => Log?.WriteLine($"{string.Join('.', _path)} = {what}");

        void TypeRef(Type t)
        {
            if (_types.TryGetValue(t, out int i))
            {
                _w.Write7BitEncodedInt(i);
                return;
            }
            _w.Write7BitEncodedInt(_types.Count);
            _types[t] = _types.Count;
            _w.Write(t.AssemblyQualifiedName ?? throw new SaveException($"The level's state cannot name {t}"));
            if (!IsValue(t) && KindOf(t) == Kind.Fielded)
            {
                var fields = Fields(t);
                _w.Write7BitEncodedInt(fields.Length);
                foreach (var f in fields) _w.Write(f.Name);
            }
        }

        public void Value(object? v)
        {
            if (v is null)
            {
                _w.Write((byte)Tag.Null);
                return;
            }
            var t = v.GetType();
            if (IsValue(t) || t.IsValueType)
            {
                _w.Write((byte)Tag.Value);
                TypeRef(t);
                if (IsValue(t))
                {
                    Primitive(v, t);
                    Note(v is float or double ? $"{v:R}" : v.ToString() ?? "");
                }
                else foreach (var f in Fields(t)) At(f.Name, f.GetValue(v));
                return;
            }
            if (_ids.TryGetValue(v, out int id))
            {
                Note($"#{id}");
                _w.Write((byte)Tag.Ref);
                _w.Write7BitEncodedInt(id);
                return;
            }
            _ids[v] = _next++;
            _w.Write((byte)Tag.New);
            TypeRef(t);
            if (t.IsArray)
            {
                var a = (Array)v;
                _w.Write((byte)a.Rank);
                for (int d = 0; d < a.Rank; d++) _w.Write7BitEncodedInt(a.GetLength(d));
            }
            Contents(v);
        }

        void Primitive(object v, Type t)
        {
            if (t.IsEnum)
            {
                _w.Write(Convert.ToInt64(v));
                return;
            }
            switch (v)
            {
                case bool b: _w.Write(b); break;
                case byte b: _w.Write(b); break;
                case sbyte b: _w.Write(b); break;
                case short s: _w.Write(s); break;
                case ushort s: _w.Write(s); break;
                case int i: _w.Write(i); break;
                case uint i: _w.Write(i); break;
                case long l: _w.Write(l); break;
                case ulong l: _w.Write(l); break;
                case float f: _w.Write(f); break;
                case double d: _w.Write(d); break;
                case char c: _w.Write((ushort)c); break;
                case decimal m: _w.Write(m); break;
                case string s: _w.Write(s); break;
                case nint n: _w.Write((long)n); break;
                default: throw new SaveException($"The level's state cannot hold a {t}");
            }
        }

        /// <summary>A kept anchor: its type (so its fields go by name), then its contents.</summary>
        public void Kept(object o)
        {
            TypeRef(o.GetType());
            Contents(o);
        }

        /// <summary>An object's contents (its type and shape are known to the reader already).</summary>
        public void Contents(object o)
        {
            var t = o.GetType();
            switch (KindOf(t))
            {
                case Kind.Array:
                {
                    var a = (Array)o;
                    if (t.GetElementType()!.IsPrimitive)
                    {
                        int bytes = Buffer.ByteLength(a);
                        var raw = new byte[bytes];
                        Buffer.BlockCopy(a, 0, raw, 0, bytes);
                        _w.Write7BitEncodedInt(bytes);
                        _w.Write(raw);
                        if (Log is not null)
                        {
                            int k = 0;
                            foreach (var x in a) Log.WriteLine($"{string.Join('.', _path)}[{k++}] = {(x is float or double ? $"{x:R}" : x)}");
                        }
                    }
                    else
                    {
                        _w.Write7BitEncodedInt(a.Length);
                        int k = 0;
                        foreach (var x in a) At($"[{k++}]", x);
                    }
                    break;
                }
                case Kind.List or Kind.Set or Kind.Queue or Kind.Stack:
                {
                    var items = ((IEnumerable)o).Cast<object?>().ToList();
                    _w.Write7BitEncodedInt(items.Count);
                    for (int k = 0; k < items.Count; k++) At($"[{k}]", items[k]);
                    break;
                }
                case Kind.Heap:
                {
                    var items = HeapItems(o).ToList();
                    _w.Write7BitEncodedInt(items.Count);
                    for (int k = 0; k < items.Count; k++)
                    {
                        At($"[{k}]", items[k].Item);
                        At($"[{k}]^", items[k].Priority);
                    }
                    break;
                }
                case Kind.Map:
                {
                    var map = (IDictionary)o;
                    _w.Write7BitEncodedInt(map.Count);
                    int k = 0;
                    foreach (DictionaryEntry e in map)
                    {
                        At($"[{k}]key", e.Key);
                        At($"[{k++}]", e.Value);
                    }
                    break;
                }
                case Kind.Delegate:
                {
                    var d = (Delegate)o;
                    if (d.GetInvocationList().Length > 1) throw new SaveException("The level's state cannot hold a multicast delegate");
                    TypeRef(d.Method.DeclaringType!);
                    _w.Write(d.Method.Name);
                    At(d.Method.Name, d.Target);
                    break;
                }
                default:
                    foreach (var f in Fields(t)) At(f.Name, f.GetValue(o));
                    break;
            }
        }
    }

    sealed class Reader
    {
        readonly BinaryReader _r;
        readonly List<object> _objects;
        readonly List<(Type Type, string[]? Fields)> _types = new();

        public Reader(BinaryReader r, Anchors anchors)
        {
            _r = r;
            _objects = new List<object>(anchors.Objects);
        }

        (Type Type, string[]? Fields) TypeRef()
        {
            int i = _r.Read7BitEncodedInt();
            if (i < _types.Count) return _types[i];
            if (i != _types.Count) throw new SaveException("The level's state is damaged");
            string name = _r.ReadString();
            var t = Type.GetType(name) ?? throw new SaveException($"The level's state holds a type this build lacks ({name})");
            string[]? fields = null;
            if (!IsValue(t) && KindOf(t) == Kind.Fielded)
            {
                fields = new string[_r.Read7BitEncodedInt()];
                for (int k = 0; k < fields.Length; k++) fields[k] = _r.ReadString();
            }
            _types.Add((t, fields));
            return (t, fields);
        }

        public object? Value()
        {
            switch ((Tag)_r.ReadByte())
            {
                case Tag.Null:
                    return null;
                case Tag.Ref:
                    return _objects[_r.Read7BitEncodedInt()];
                case Tag.Value:
                {
                    var (t, fields) = TypeRef();
                    if (IsValue(t)) return Primitive(t);
                    object box = RuntimeHelpers.GetUninitializedObject(t);
                    Fill(box, t, fields!);
                    return box;
                }
                case Tag.New:
                {
                    var (t, fields) = TypeRef();
                    object o;
                    if (t.IsArray)
                    {
                        int rank = _r.ReadByte();
                        var lengths = new int[rank];
                        for (int d = 0; d < rank; d++) lengths[d] = _r.Read7BitEncodedInt();
                        o = Array.CreateInstance(t.GetElementType()!, lengths);
                    }
                    else
                    {
                        var kind = KindOf(t);
                        o = kind == Kind.Fielded ? RuntimeHelpers.GetUninitializedObject(t)
                            : kind == Kind.Delegate ? null! : Activator.CreateInstance(t)!;
                        if (kind == Kind.Delegate)
                        {
                            // Made from its method and target, which are read first; nothing can refer to it before.
                            int at = _objects.Count;
                            _objects.Add(null!);
                            var d = ReadDelegate(t);
                            _objects[at] = d;
                            return d;
                        }
                    }
                    _objects.Add(o);
                    Contents(o, fields);
                    return o;
                }
                default:
                    throw new SaveException("The level's state is damaged");
            }
        }

        object Primitive(Type t)
        {
            if (t.IsEnum) return Enum.ToObject(t, _r.ReadInt64());
            return Type.GetTypeCode(t) switch
            {
                TypeCode.Boolean => _r.ReadBoolean(),
                TypeCode.Byte => _r.ReadByte(),
                TypeCode.SByte => _r.ReadSByte(),
                TypeCode.Int16 => _r.ReadInt16(),
                TypeCode.UInt16 => _r.ReadUInt16(),
                TypeCode.Int32 => _r.ReadInt32(),
                TypeCode.UInt32 => _r.ReadUInt32(),
                TypeCode.Int64 => _r.ReadInt64(),
                TypeCode.UInt64 => _r.ReadUInt64(),
                TypeCode.Single => _r.ReadSingle(),
                TypeCode.Double => _r.ReadDouble(),
                TypeCode.Char => (char)_r.ReadUInt16(),
                TypeCode.Decimal => _r.ReadDecimal(),
                TypeCode.String => _r.ReadString(),
                _ when t == typeof(nint) => (nint)_r.ReadInt64(),
                _ => throw new SaveException($"The level's state cannot hold a {t}"),
            };
        }

        Delegate ReadDelegate(Type t)
        {
            var (owner, _) = TypeRef();
            string name = _r.ReadString();
            object? target = Value();
            var method = owner.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new SaveException($"The level's state names a method this build lacks ({owner}.{name})");
            return method.IsStatic ? Delegate.CreateDelegate(t, method) : Delegate.CreateDelegate(t, target, method);
        }

        /// <summary>The fields' values as saved, each checked against the field this build has (unknown ones skipped).</summary>
        List<(FieldInfo Field, object? Value)> ReadFields(Type t, string[] names)
        {
            var have = Fields(t);
            var values = new List<(FieldInfo, object?)>();
            foreach (string name in names)
            {
                object? v = Value();
                var f = Array.Find(have, x => x.Name == name);
                if (f is null) continue;
                bool fits = v is null ? !f.FieldType.IsValueType || Nullable.GetUnderlyingType(f.FieldType) is not null : f.FieldType.IsInstanceOfType(v);
                if (!fits) throw new SaveException($"The level's state does not fit {t}.{name}");
                values.Add((f, v));
            }
            return values;
        }

        void Fill(object o, Type t, string[] names)
        {
            foreach (var (f, v) in ReadFields(t, names)) f.SetValue(o, v);
        }

        /// <summary>What is to be put into the kept anchors, once everything has been read.</summary>
        readonly List<Action> _apply = new();

        /// <summary>A kept anchor's contents: read aside now, put into it by <see cref="Apply"/>.</summary>
        public void Kept(object o)
        {
            var (t, fields) = TypeRef();
            if (t != o.GetType()) throw new SaveException("The level's state does not fit this level");
            var kind = KindOf(t);
            if (kind == Kind.Fielded)
            {
                var values = ReadFields(t, fields!);
                _apply.Add(() =>
                {
                    foreach (var (f, v) in values) f.SetValue(o, v);
                });
                return;
            }
            object stand;
            if (t.IsArray)
            {
                var a = (Array)o;
                var lengths = new int[a.Rank];
                for (int d = 0; d < a.Rank; d++) lengths[d] = a.GetLength(d);
                stand = Array.CreateInstance(t.GetElementType()!, lengths);
            }
            else stand = Activator.CreateInstance(t)!;
            Contents(stand, null);
            _apply.Add(() => CopyInto(o, stand, kind));
        }

        public void Apply()
        {
            foreach (var apply in _apply) apply();
        }

        /// <summary>A collection's contents, as read aside, put into the one that exists (in the same order).</summary>
        static void CopyInto(object target, object from, Kind kind)
        {
            var t = target.GetType();
            switch (kind)
            {
                case Kind.Array:
                    Array.Copy((Array)from, (Array)target, ((Array)from).Length);
                    break;
                case Kind.List:
                {
                    var list = (IList)target;
                    list.Clear();
                    foreach (var x in (IList)from) list.Add(x);
                    break;
                }
                case Kind.Map:
                {
                    var map = (IDictionary)target;
                    map.Clear();
                    foreach (DictionaryEntry e in (IDictionary)from) map[e.Key] = e.Value;
                    break;
                }
                case Kind.Heap:
                {
                    t.GetMethod("Clear")!.Invoke(target, null);
                    var enqueue = t.GetMethod("Enqueue")!;
                    foreach (var (item, priority) in HeapItems(from)) enqueue.Invoke(target, new[] { item, priority });
                    break;
                }
                default:
                {
                    t.GetMethod("Clear")!.Invoke(target, null);
                    var add = t.GetMethod(kind == Kind.Set ? "Add" : kind == Kind.Queue ? "Enqueue" : "Push")!;
                    var items = ((IEnumerable)from).Cast<object?>().ToArray();
                    // A stack lists its top first: pushed back bottom first.
                    if (kind == Kind.Stack) Array.Reverse(items);
                    foreach (var x in items) add.Invoke(target, new[] { x });
                    break;
                }
            }
        }

        void Contents(object o, string[]? fields)
        {
            var t = o.GetType();
            switch (KindOf(t))
            {
                case Kind.Array:
                {
                    var a = (Array)o;
                    if (t.GetElementType()!.IsPrimitive)
                    {
                        int bytes = _r.Read7BitEncodedInt();
                        if (bytes != Buffer.ByteLength(a)) throw new SaveException("The level's state does not fit this level");
                        Buffer.BlockCopy(_r.ReadBytes(bytes), 0, a, 0, bytes);
                    }
                    else
                    {
                        if (_r.Read7BitEncodedInt() != a.Length) throw new SaveException("The level's state does not fit this level");
                        // Row-major over every rank.
                        var index = new int[a.Rank];
                        for (int n = 0; n < a.Length; n++)
                        {
                            int rest = n;
                            for (int d = a.Rank - 1; d >= 0; d--)
                            {
                                index[d] = rest % a.GetLength(d);
                                rest /= a.GetLength(d);
                            }
                            a.SetValue(Value(), index);
                        }
                    }
                    break;
                }
                case Kind.List:
                {
                    var list = (IList)o;
                    list.Clear();
                    int n = _r.Read7BitEncodedInt();
                    for (int i = 0; i < n; i++) list.Add(Value());
                    break;
                }
                case Kind.Set or Kind.Queue or Kind.Stack:
                {
                    var kind = KindOf(t);
                    t.GetMethod("Clear")!.Invoke(o, null);
                    var add = t.GetMethod(kind == Kind.Set ? "Add" : kind == Kind.Queue ? "Enqueue" : "Push")!;
                    int n = _r.Read7BitEncodedInt();
                    var items = new object?[n];
                    for (int i = 0; i < n; i++) items[i] = Value();
                    // A stack lists its top first: pushed back bottom first.
                    if (kind == Kind.Stack) Array.Reverse(items);
                    foreach (var x in items) add.Invoke(o, new[] { x });
                    break;
                }
                case Kind.Heap:
                {
                    // Enqueued in the heap's own order, every item stays where it was: the very same heap.
                    t.GetMethod("Clear")!.Invoke(o, null);
                    var enqueue = t.GetMethod("Enqueue")!;
                    int n = _r.Read7BitEncodedInt();
                    for (int i = 0; i < n; i++)
                    {
                        var item = Value();
                        var priority = Value();
                        enqueue.Invoke(o, new[] { item, priority });
                    }
                    break;
                }
                case Kind.Map:
                {
                    var map = (IDictionary)o;
                    map.Clear();
                    int n = _r.Read7BitEncodedInt();
                    for (int i = 0; i < n; i++)
                    {
                        object key = Value() ?? throw new SaveException("The level's state is damaged");
                        map[key] = Value();
                    }
                    break;
                }
                default:
                    Fill(o, t, fields ?? throw new SaveException("The level's state is damaged"));
                    break;
            }
        }
    }
}
