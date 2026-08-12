#r "nuget: Microsoft.CodeAnalysis.CSharp.Workspaces, 4.10.0"
#r "nuget: Microsoft.CodeAnalysis.Workspaces.MSBuild, 4.10.0"
#r "nuget: Microsoft.Build.Locator, 1.7.8"
#r "nuget: System.CommandLine, 2.0.0-beta4.22272.1"

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CommandLine;

// Usage:
//   dotnet script scripts/RemoveTPrefix.csx --solution VertexBPMN.sln --dry-run
//   dotnet script scripts/RemoveTPrefix.csx --solution VertexBPMN.sln
// Only types declared inside the BPMN20 generated file will be considered.

var solutionOption = new Option<string>("--solution", description: "Path to the .sln file", getDefaultValue: () => "VertexBPMN.sln");
var dryRunOption = new Option<bool>("--dry-run", description: "Do not apply changes, only list planned renames.");
var fileOption = new Option<string>("--file", description: "Target generated file relative to solution directory", getDefaultValue: () => Path.Combine("src","VertexBPMN.Model","Schemas","BPMN20","Generated","Bpmn20","VertexBPMN.Domain.Model.Bpmn20.cs"));

var rootCommand = new RootCommand("Remove leading 'T' prefix from BPMN 2.0 generated classes")
{
    solutionOption,
    dryRunOption,
    fileOption
};

rootCommand.SetHandler(async (solutionPath, dryRun, targetFile) =>
{
    if (!File.Exists(solutionPath))
    {
        Console.Error.WriteLine($"Solution not found: {solutionPath}");
        return;
    }

    MSBuildLocator.RegisterDefaults();
    using var workspace = MSBuildWorkspace.Create();
    workspace.WorkspaceFailed += (s, e) => Console.Error.WriteLine($"[MSBuild] {e.Diagnostic.Message}");

    Console.WriteLine($"Loading solution {solutionPath} ...");
    var solution = await workspace.OpenSolutionAsync(solutionPath);

    var fullTargetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solutionPath)!, targetFile));
    Console.WriteLine($"Target file: {fullTargetPath}");

    var docsInTargetFile = solution.Projects.SelectMany(p => p.Documents)
        .Where(d => string.Equals(Path.GetFullPath(d.FilePath ?? ""), fullTargetPath, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (!docsInTargetFile.Any())
    {
        Console.Error.WriteLine("No documents matched target file.");
        return;
    }

    var candidateTypeSymbols = new List<INamedTypeSymbol>();

    foreach (var doc in docsInTargetFile)
    {
        var tree = await doc.GetSyntaxTreeAsync();
        if (tree == null) continue;
        var root = await tree.GetRootAsync();
        var model = await doc.GetSemanticModelAsync();
        if (model == null) continue;

        // Collect class & enum declarations starting with 'T' followed by uppercase letter
        var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var tDecl in typeDecls)
        {
            var name = tDecl.Identifier.Text;
            if (name.Length > 1 && name[0] == 'T' && char.IsUpper(name[1]))
            {
                var symbol = model.GetDeclaredSymbol(tDecl) as INamedTypeSymbol;
                if (symbol != null)
                {
                    candidateTypeSymbols.Add(symbol);
                }
            }
        }
        // Also enums
        var enumDecls = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        foreach (var eDecl in enumDecls)
        {
            var name = eDecl.Identifier.Text;
            if (name.Length > 1 && name[0] == 'T' && char.IsUpper(name[1]))
            {
                var symbol = model.GetDeclaredSymbol(eDecl) as INamedTypeSymbol;
                if (symbol != null)
                {
                    candidateTypeSymbols.Add(symbol);
                }
            }
        }
    }

    Console.WriteLine($"Collected {candidateTypeSymbols.Count} candidate type symbols starting with 'T'.");

    // Build rename plan (avoid collisions)
    var plan = new List<(INamedTypeSymbol Symbol, string NewName)>();
    var existingNamesByNamespace = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    foreach (var proj in solution.Projects)
    {
        foreach (var doc in proj.Documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            if (tree == null) continue;
            var root = await tree.GetRootAsync();
            var model = await doc.GetSemanticModelAsync();
            if (model == null) continue;

            foreach (var decl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                var symbol = model.GetDeclaredSymbol(decl) as INamedTypeSymbol;
                if (symbol == null) continue;
                var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (!existingNamesByNamespace.TryGetValue(ns, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    existingNamesByNamespace[ns] = set;
                }
                set.Add(symbol.Name);
            }
        }
    }

    foreach (var symbol in candidateTypeSymbols.Distinct(SymbolEqualityComparer.Default))
    {
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var newName = symbol.Name.Substring(1); // drop leading 'T'

        if (!existingNamesByNamespace.TryGetValue(ns, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            existingNamesByNamespace[ns] = set;
        }

    Console.WriteLine($"Computed rename plan entries: {plan.Count}.");
    if (plan.Count > 0)
    {
        Console.WriteLine("First 10 planned renames (symbol -> newName):");
        foreach (var (sym, newName) in plan.Take(10))
            Console.WriteLine($"  {sym.Name} -> {newName}");
    }

        if (set.Contains(newName))
        {
            Console.WriteLine($"SKIP (collision): {symbol.Name} -> {newName} in namespace {ns}");
            continue;
        }

        plan.Add((symbol, newName));
        set.Add(newName); // reserve
    }

    if (plan.Count == 0)
    {
        Console.WriteLine("No rename candidates found.");
        return;
    }

    Console.WriteLine("Rename plan:");
    foreach (var (sym, newName) in plan)
    {
        Console.WriteLine($"  {sym.ToDisplayString()} -> {newName}");
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: no changes applied.");
        return;
    }

    var currentSolution = solution;
    int applied = 0;
    foreach (var (sym, newName) in plan)
    {
        Console.WriteLine($"Renaming {sym.Name} -> {newName} ...");
        currentSolution = await Renamer.RenameSymbolAsync(currentSolution, sym, new SymbolRenameOptions(), newName);
        applied++;
    }

    if (applied > 0)
    {
        Console.WriteLine("Applying changes to workspace...");
        var result = workspace.TryApplyChanges(currentSolution);
        Console.WriteLine(result ? "Changes applied successfully." : "Failed to apply changes.");
    }

}, solutionOption, dryRunOption, fileOption);

// dotnet-script does not provide 'args' implicitly in the same way; capture from Environment
var invokeArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
await rootCommand.InvokeAsync(invokeArgs);
