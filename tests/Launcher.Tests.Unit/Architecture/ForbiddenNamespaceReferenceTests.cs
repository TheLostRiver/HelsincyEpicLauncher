// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Tests.Unit.Architecture;

public class ForbiddenNamespaceReferenceTests
{
    private static readonly string[] KnownPresentationDomainReferenceExceptions =
    [
        "src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs",
    ];

    [Fact]
    public void Presentation_ShouldNotAddNewDomainNamespaceReferences()
    {
        var violatingFiles = FindPresentationFilesReferencing("Launcher.Domain");

        violatingFiles.Should().BeEquivalentTo(
            KnownPresentationDomainReferenceExceptions,
            "current Presentation -> Domain references must stay explicit until they are removed");
    }

    private static string[] FindPresentationFilesReferencing(string forbiddenNamespace)
    {
        var solutionRoot = FindSolutionRoot();
        var presentationRoot = Path.Combine(solutionRoot, "src", "Launcher.Presentation");
        Directory.Exists(presentationRoot).Should().BeTrue($"Presentation project directory should exist at {presentationRoot}");

        return Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(file => File.ReadLines(file).Any(line => line.Contains(forbiddenNamespace, StringComparison.Ordinal)))
            .Select(file => NormalizePath(Path.GetRelativePath(solutionRoot, file)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSourceFile(string file)
    {
        var normalized = NormalizePath(file);

        return !normalized.Contains("/bin/", StringComparison.Ordinal)
            && !normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HelsincyEpicLauncher.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
