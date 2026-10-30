// Aufruf: dotnet run -c Release --project werkzeuge/skilldump -- "<Spiel>/One Million Coins_Data/Managed" <ausgabe.tsv>
using System.Reflection;
using System.Text;

// Gibt SkillCatalog.All des Spiels aus (nur lesend, per Reflexion).
string managed = args[0];
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var name = new AssemblyName(e.Name).Name;
    if (name == "mscorlib" || name == "netstandard" || name!.StartsWith("System")) return null;
    var path = Path.Combine(managed, name + ".dll");
    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
};
var asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
var cat = asm.GetType("GoldDigger.SkillCatalog")!;
var all = (Array)cat.GetField("All")!.GetValue(null)!;
var sb = new StringBuilder();
foreach (var s in all)
{
    var t = s.GetType();
    object F(string n) => t.GetField(n)!.GetValue(s)!;
    var bonus = F("Bonuses");
    var bs = new List<string>();
    foreach (var f in bonus.GetType().GetFields())
    {
        var v = f.GetValue(bonus);
        if (v is float fl && fl != 0f) bs.Add($"{f.Name}={fl:0.####}");
        else if (v is int i && i != 0) bs.Add($"{f.Name}={i}");
        else if (v is bool b && b) bs.Add(f.Name);
    }
    var req = F("Requires") as Array;
    sb.Append($"{(int)F("Id")}\tb{F("Branch")}\tt{F("Tier")}\tl{F("Lane")}\tc{F("Cost")}\tp{(t.GetField("Parent")!.GetValue(s) is object p ? ((int)p).ToString() : "-")}\t{F("Name")}\t[{string.Join(",", bs)}]\treq={(req == null ? "" : string.Join(",", req.Cast<object>().Select(x => ((int)x).ToString())))}\tkey={F("IsKeystone")}\tmin={F("MinimumCoins")}\n");
    sb.Append("\t\t" + ((string)F("Description")).Replace("\n", " / ") + "\n");
}
File.WriteAllText(args[1], sb.ToString());
Console.WriteLine($"{all.Length} Skills");
