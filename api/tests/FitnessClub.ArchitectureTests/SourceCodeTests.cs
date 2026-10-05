using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FitnessClub.ArchitectureTests;

public class SourceCodeTests
{
    private static readonly SyntaxKind[] CommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    [Fact]
    public void Source_files_contain_no_comments()
    {
        var root = SolutionRoot();
        var files = new[] { "src", "tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .ToList();

        var offenders = files
            .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantTrivia(descendIntoTrivia: true)
                .Where(trivia => CommentKinds.Contains(trivia.Kind()))
                .Select(trivia => $"{Path.GetRelativePath(root, path)}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"));

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj");

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FitnessClub.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("FitnessClub.slnx was not found.");
    }
}
