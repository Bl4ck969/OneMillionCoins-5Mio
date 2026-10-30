using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

// Usage: metadump <dll> <typeRegex>  -> Typen, Felder, Methoden der passenden Typen
var pe = new PEReader(File.OpenRead(args[0]));
var md = pe.GetMetadataReader();
var rx = new Regex(args.Length > 1 ? args[1] : ".", RegexOptions.IgnoreCase);
string Name(TypeDefinition t)
{
    var n = md.GetString(t.Name);
    var ns = md.GetString(t.Namespace);
    if (t.GetDeclaringType() is { IsNil: false } d) return Name(md.GetTypeDefinition(d)) + "+" + n;
    return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
}
foreach (var h in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(h);
    var full = Name(t);
    if (!rx.IsMatch(full)) continue;
    Console.WriteLine($"== {full}");
    foreach (var fh in t.GetFields())
    {
        var f = md.GetFieldDefinition(fh);
        var c = f.GetDefaultValue();
        string val = "";
        if (!c.IsNil)
        {
            var cv = md.GetConstant(c);
            var br = md.GetBlobReader(cv.Value);
            val = cv.TypeCode switch
            {
                ConstantTypeCode.Int32 => " = " + br.ReadInt32(),
                ConstantTypeCode.Int64 => " = " + br.ReadInt64(),
                ConstantTypeCode.Single => " = " + br.ReadSingle(),
                ConstantTypeCode.Double => " = " + br.ReadDouble(),
                ConstantTypeCode.String => " = \"" + br.ReadUTF16(br.Length) + "\"",
                ConstantTypeCode.Boolean => " = " + br.ReadBoolean(),
                _ => ""
            };
        }
        Console.WriteLine($"   F {md.GetString(f.Name)}{val}");
    }
    foreach (var mh in t.GetMethods())
        Console.Write($" {md.GetString(md.GetMethodDefinition(mh).Name)}");
    Console.WriteLine();
}
