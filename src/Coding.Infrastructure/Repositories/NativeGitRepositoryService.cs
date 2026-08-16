using System.Diagnostics;
using System.Globalization;
using Coding.Application.Features.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Coding.Infrastructure.Repositories;

public sealed class RepositoryStorageOptions
{
    public const string SectionName = "RepositoryStorage";
    public string RootPath { get; set; } = "App_Data/repositories";
}

public sealed class NativeGitRepositoryService : IGitRepositoryService
{
    private readonly string _rootPath;

    public NativeGitRepositoryService(IOptions<RepositoryStorageOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("RepositoryStorage:RootPath must be configured.");

        _rootPath = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task InitializeAsync(Guid projectId, string defaultBranch, CancellationToken cancellationToken = default)
    {
        ValidateBranch(defaultBranch);
        var path = GetRepositoryPath(projectId);
        Directory.CreateDirectory(path);
        if (Directory.Exists(Path.Combine(path, ".git"))) return;
        await RunAsync(path, ["init", "--initial-branch", defaultBranch], cancellationToken);
    }

    public async Task<GitStatusResult> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var path = RequireRepository(projectId);
        var branch = (await RunAsync(path, ["branch", "--show-current"], cancellationToken)).Trim();
        var output = await RunAsync(path, ["status", "--porcelain=v1", "-z"], cancellationToken);
        var files = output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => new GitFileStatus(entry.Length > 3 ? entry[3..] : string.Empty, entry[0].ToString(), entry[1].ToString()))
            .ToArray();
        return new GitStatusResult(branch, files.Length == 0, files);
    }

    public async Task<IReadOnlyList<GitBranchResult>> GetBranchesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(RequireRepository(projectId), ["branch", "--format=%(HEAD)%00%(refname:short)"], cancellationToken);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\0'))
            .Where(parts => parts.Length == 2)
            .Select(parts => new GitBranchResult(parts[1], parts[0] == "*"))
            .ToArray();
    }

    public async Task CreateBranchAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default)
    {
        ValidateBranch(branchName);
        await RunAsync(RequireRepository(projectId), ["check-ref-format", "--branch", branchName], cancellationToken);
        await RunAsync(RequireRepository(projectId), ["branch", branchName], cancellationToken);
    }

    public async Task CheckoutAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default)
    {
        ValidateBranch(branchName);
        await RunAsync(RequireRepository(projectId), ["check-ref-format", "--branch", branchName], cancellationToken);
        await RunAsync(RequireRepository(projectId), ["checkout", branchName], cancellationToken);
    }

    public async Task<GitCommitResult> CommitAllAsync(Guid projectId, string message, string authorName, string authorEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 500)
            throw new ArgumentException("Commit message must contain between 1 and 500 characters.", nameof(message));
        if (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(authorEmail))
            throw new ArgumentException("Commit author name and email are required.");

        var path = RequireRepository(projectId);
        await RunAsync(path, ["add", "--all"], cancellationToken);
        await RunAsync(path, ["-c", $"user.name={authorName.Trim()}", "-c", $"user.email={authorEmail.Trim()}", "commit", "--message", message.Trim()], cancellationToken);
        return (await GetHistoryAsync(projectId, 1, cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<GitCommitResult>> GetHistoryAsync(Guid projectId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var output = await RunAsync(RequireRepository(projectId), ["log", $"--max-count={take}", "--format=%H%x00%h%x00%an%x00%ae%x00%s%x00%cI"], cancellationToken, allowEmptyRepository: true);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseCommit)
            .ToArray();
    }

    public async Task<GitDiffResult> GetDiffAsync(Guid projectId, bool staged, CancellationToken cancellationToken = default)
    {
        var arguments = staged ? new[] { "diff", "--cached", "--no-ext-diff" } : new[] { "diff", "--no-ext-diff" };
        return new GitDiffResult(await RunAsync(RequireRepository(projectId), arguments, cancellationToken));
    }

    public async Task WriteFileAsync(Guid projectId, string projectPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var target = ResolveWorktreePath(projectId, projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, content.ToArray(), cancellationToken);
    }

    public Task<byte[]> ReadFileAsync(Guid projectId, string projectPath, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(ResolveWorktreePath(projectId, projectPath), cancellationToken);

    private string GetRepositoryPath(Guid projectId)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Project ID is required.", nameof(projectId));
        var path = Path.GetFullPath(Path.Combine(_rootPath, projectId.ToString("N")));
        var prefix = _rootPath.EndsWith(Path.DirectorySeparatorChar) ? _rootPath : _rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Repository path escaped the configured root.");
        return path;
    }

    private string RequireRepository(Guid projectId)
    {
        var path = GetRepositoryPath(projectId);
        if (!Directory.Exists(Path.Combine(path, ".git"))) throw new DirectoryNotFoundException("Project repository has not been initialized.");
        return path;
    }

    private string ResolveWorktreePath(Guid projectId, string projectPath)
    {
        var repository = RequireRepository(projectId);
        var relative = projectPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
            relative.Split('/').Any(segment => segment is "" or "." or ".." or ".git"))
            throw new ArgumentException("Project path is invalid.", nameof(projectPath));
        var target = Path.GetFullPath(Path.Combine(repository, relative));
        var prefix = repository.EndsWith(Path.DirectorySeparatorChar) ? repository : repository + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("File path escaped the repository.");
        return target;
    }

    private static void ValidateBranch(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName) || branchName.Length > 200 || branchName != branchName.Trim())
            throw new ArgumentException("Branch name is invalid.", nameof(branchName));
    }

    private static GitCommitResult ParseCommit(string line)
    {
        var fields = line.Split('\0');
        if (fields.Length != 6 || !DateTimeOffset.TryParse(fields[5], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedAt))
            throw new InvalidOperationException("Git returned an invalid commit record.");
        return new GitCommitResult(fields[0], fields[1], fields[2], fields[3], fields[4], committedAt);
    }

    private static async Task<string> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool allowEmptyRepository = false)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Git.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
        {
            if (allowEmptyRepository && error.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            throw new InvalidOperationException($"Git operation failed: {error.Trim()}");
        }
        return output;
    }
}
