// Copyright (c) Helsincy. All rights reserved.

using System.Xml.Linq;

namespace Launcher.Tests.Unit.Architecture;

public class ProjectReferenceRulesTests
{
    private static readonly string[] ApplicationForbiddenReferences =
    [
        "Launcher.Infrastructure",
        "Launcher.Presentation",
    ];

    private static readonly string[] DomainAllowedReferences =
    [
        "Launcher.Shared",
    ];

    private static readonly string[] PresentationForbiddenReferences =
    [
        "Launcher.Infrastructure",
    ];

    [Fact]
    public void Application_ShouldNotReferenceInfrastructureOrPresentation()
    {
        var references = ReadProjectReferences("Launcher.Application");

        references.Should().NotContain(ApplicationForbiddenReferences);
    }

    [Fact]
    public void Domain_ShouldReferenceOnlyShared()
    {
        var references = ReadProjectReferences("Launcher.Domain");

        references.Should().BeEquivalentTo(DomainAllowedReferences);
    }

    [Fact]
    public void Presentation_ShouldNotReferenceInfrastructure()
    {
        var references = ReadProjectReferences("Launcher.Presentation");

        references.Should().NotContain(PresentationForbiddenReferences);
    }

    private static string[] ReadProjectReferences(string projectName)
    {
        var projectPath = Path.Combine(FindSolutionRoot(), "src", projectName, $"{projectName}.csproj");
        File.Exists(projectPath).Should().BeTrue($"project file should exist at {projectPath}");

        var document = XDocument.Load(projectPath);

        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
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
