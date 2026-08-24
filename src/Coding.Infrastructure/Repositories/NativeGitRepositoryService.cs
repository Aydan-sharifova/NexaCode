using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
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
    private static readonly Regex SafeBranchName = new(
        "^[A-Za-z0-9][A-Za-z0-9._/-]{0,199}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FullCommitSha = new("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly string _rootPath;
    private readonly IProjectRepositoryCoordinator _coordinator;

    public NativeGitRepositoryService(IOptions<RepositoryStorageOptions> options, IHostEnvironment environment, IProjectRepositoryCoordinator coordinator)
    {
        _coordinator = coordinator;
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
        var output = await RunAsync(path, ["status", "--porcelain=v1", "-z", "--no-renames"], cancellationToken);
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
        var repository = RequireRepository(projectId);
        await RunAsync(repository, ["check-ref-format", "--branch", branchName], cancellationToken);
        await EnsureHeadExistsAsync(repository, cancellationToken);
        await RunAsync(repository, ["branch", branchName], cancellationToken);
    }

    public async Task CheckoutAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default)
    {
        ValidateBranch(branchName);
        await RunAsync(RequireRepository(projectId), ["check-ref-format", "--branch", branchName], cancellationToken);
        await RunAsync(RequireRepository(projectId), ["checkout", branchName], cancellationToken);
    }

    public Task StageAsync(Guid projectId, string projectPath, CancellationToken cancellationToken = default) =>
        RunPathOperationAsync(projectId, projectPath, ["add", "--"], cancellationToken);

    public async Task UnstageAsync(Guid projectId, string projectPath, CancellationToken cancellationToken = default)
    {
        var path = ValidateProjectPath(projectPath);
        var repository = RequireRepository(projectId);
        try
        {
            await RunAsync(repository, ["reset", "--", path], cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // An unborn branch has no HEAD for reset. Removing the path from the
            // index is the equivalent safe operation and leaves the worktree intact.
            await RunAsync(repository, ["rm", "--cached", "--ignore-unmatch", "--", path], cancellationToken);
        }
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

    public async Task<GitDiffResult> GetCommitDiffAsync(Guid projectId, string sha, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sha) || !FullCommitSha.IsMatch(sha))
            throw new ArgumentException("Commit SHA is invalid.", nameof(sha));
        var patch = await RunAsync(RequireRepository(projectId),
            ["show", "--format=", "--stat", "--patch", "--no-ext-diff", sha, "--"], cancellationToken);
        return new GitDiffResult(patch);
    }

    public async Task WriteFileAsync(Guid projectId, string projectPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var target = ResolveWorktreePath(projectId, projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, content.ToArray(), cancellationToken);
    }

    public async Task ReplaceWorktreeAsync(Guid projectId, IReadOnlyList<GitBranchFile> files, CancellationToken cancellationToken = default)
    {
        var repository = RequireRepository(projectId);
        var validated = files.Select(file => (Path: ValidateProjectPath(file.Path), file.Content)).ToArray();
        if (validated.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count() != validated.Length)
            throw new ArgumentException("Workspace snapshot contains duplicate paths.", nameof(files));
        foreach (var entry in Directory.EnumerateFileSystemEntries(repository))
        {
            if (string.Equals(Path.GetFileName(entry), ".git", StringComparison.Ordinal)) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.Directory) && !attributes.HasFlag(FileAttributes.ReparsePoint)) Directory.Delete(entry, true);
            else File.Delete(entry);
        }
        foreach (var file in validated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = ResolveWorktreePath(projectId, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, file.Content, cancellationToken);
        }
    }

    public Task<byte[]> ReadFileAsync(Guid projectId, string projectPath, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(ResolveWorktreePath(projectId, projectPath), cancellationToken);

    public async Task<string> GetBranchHeadAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default)
    {
        ValidateBranch(branchName);
        return (await RunAsync(RequireRepository(projectId), ["rev-parse", "--verify", $"refs/heads/{branchName}"], cancellationToken)).Trim();
    }

    public async Task<IReadOnlyList<GitBranchFile>> GetBranchFilesAsync(Guid projectId, string branchName, CancellationToken cancellationToken = default)
    {
        const int maximumFiles = 5000;
        const int maximumFileBytes = 5 * 1024 * 1024;
        const long maximumSnapshotBytes = 50L * 1024 * 1024;
        ValidateBranch(branchName);
        var repository = RequireRepository(projectId);
        await GetBranchHeadAsync(projectId, branchName, cancellationToken);
        var output = await RunBytesAsync(repository, ["ls-tree", "-r", "-z", "--name-only", branchName, "--"], cancellationToken);
        var paths = System.Text.Encoding.UTF8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length > maximumFiles) throw new InvalidOperationException($"Branch snapshots are limited to {maximumFiles} files.");
        var result = new List<GitBranchFile>(paths.Length);
        long totalBytes = 0;
        foreach (var rawPath in paths)
        {
            var path = ValidateProjectPath(rawPath);
            var content = await RunBytesAsync(repository, ["show", $"{branchName}:{path}"], cancellationToken);
            if (content.Length > maximumFileBytes) throw new InvalidOperationException($"File '{path}' exceeds the workspace snapshot limit.");
            totalBytes += content.Length;
            if (totalBytes > maximumSnapshotBytes) throw new InvalidOperationException("Branch snapshot exceeds the workspace size limit.");
            result.Add(new GitBranchFile(path, content));
        }
        return result;
    }

    public async Task<GitDiffResult> CompareBranchesAsync(Guid projectId, string targetBranch, string sourceBranch, CancellationToken cancellationToken = default)
    {
        ValidateBranch(targetBranch);
        ValidateBranch(sourceBranch);
        await GetBranchHeadAsync(projectId, targetBranch, cancellationToken);
        await GetBranchHeadAsync(projectId, sourceBranch, cancellationToken);
        var patch = await RunAsync(RequireRepository(projectId), ["diff", "--no-ext-diff", $"{targetBranch}...{sourceBranch}", "--"], cancellationToken);
        return new GitDiffResult(patch);
    }

    public async Task<bool> HasMergeConflictsAsync(Guid projectId, string targetBranch, string sourceBranch, CancellationToken cancellationToken = default)
    {
        ValidateBranch(targetBranch);
        ValidateBranch(sourceBranch);
        await GetBranchHeadAsync(projectId, targetBranch, cancellationToken);
        await GetBranchHeadAsync(projectId, sourceBranch, cancellationToken);
        var result = await RunWithExitCodeAsync(RequireRepository(projectId), ["merge-tree", "--write-tree", targetBranch, sourceBranch], cancellationToken);
        if (result.ExitCode == 0) return false;
        if (result.ExitCode == 1) return true;
        throw new InvalidOperationException($"Git merge conflict check failed: {result.Error.Trim()}");
    }

    public async Task<GitMergeResult> MergeAsync(Guid projectId, string targetBranch, string sourceBranch, string message, CancellationToken cancellationToken = default)
    {
        ValidateBranch(targetBranch);
        ValidateBranch(sourceBranch);
        if (targetBranch == sourceBranch) throw new ArgumentException("Source and target branches must differ.");
        if (string.IsNullOrWhiteSpace(message) || message.Length > 500) throw new ArgumentException("Merge message is invalid.");
        await using var lease = await _coordinator.AcquireAsync(projectId, cancellationToken);
        var repository = RequireRepository(projectId);
            var status = await GetStatusAsync(projectId, cancellationToken);
            if (!status.IsClean) throw new InvalidOperationException("The repository worktree must be clean before merging.");
            if (await HasMergeConflictsAsync(projectId, targetBranch, sourceBranch, cancellationToken))
                throw new InvalidOperationException("The pull request has merge conflicts.");
            var previousBranch = status.CurrentBranch;
            await RunAsync(repository, ["checkout", targetBranch], cancellationToken);
            try
            {
                await RunAsync(repository, ["-c", "user.name=Coding Platform", "-c", "user.email=git@coding.local", "merge", "--no-ff", "--message", message.Trim(), sourceBranch], cancellationToken);
                var sha = (await RunAsync(repository, ["rev-parse", "HEAD"], cancellationToken)).Trim();
                return new GitMergeResult(sha, sha[..Math.Min(7, sha.Length)]);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousBranch) && previousBranch != targetBranch)
                    await RunAsync(repository, ["checkout", previousBranch], CancellationToken.None);
            }
    }

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
        var relative = ValidateProjectPath(projectPath);
        var target = Path.GetFullPath(Path.Combine(repository, relative));
        var prefix = repository.EndsWith(Path.DirectorySeparatorChar) ? repository : repository + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("File path escaped the repository.");
        RejectSymbolicLinks(repository, relative);
        return target;
    }

    private async Task RunPathOperationAsync(Guid projectId, string projectPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var path = ValidateProjectPath(projectPath);
        await RunAsync(RequireRepository(projectId), [.. arguments, path], cancellationToken);
    }

    private static string ValidateProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || Path.IsPathRooted(projectPath))
            throw new ArgumentException("Project path is invalid.", nameof(projectPath));
        var relative = projectPath.Replace('\\', '/');
        if (relative.StartsWith('/') || relative.Split('/').Any(segment =>
                segment is "" or "." or ".." || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Project path is invalid.", nameof(projectPath));
        return relative;
    }

    private static void RejectSymbolicLinks(string repository, string relative)
    {
        var current = repository;
        foreach (var segment in relative.Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Symbolic links are not permitted in project paths.");
        }
    }

    private static void ValidateBranch(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName) || !SafeBranchName.IsMatch(branchName) ||
            branchName.Contains("..", StringComparison.Ordinal) || branchName.Contains("//", StringComparison.Ordinal) ||
            branchName.EndsWith('.') || branchName.EndsWith('/') || branchName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Branch name is invalid.", nameof(branchName));
    }

    private static GitCommitResult ParseCommit(string line)
    {
        var fields = line.Split('\0');
        if (fields.Length != 6 || !DateTimeOffset.TryParse(fields[5], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedAt))
            throw new InvalidOperationException("Git returned an invalid commit record.");
        return new GitCommitResult(fields[0], fields[1], fields[2], fields[3], fields[4], committedAt);
    }

    private static async Task EnsureHeadExistsAsync(string repository, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(repository, ["rev-parse", "--verify", "HEAD"], cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await RunAsync(repository,
                ["-c", "user.name=Coding Platform", "-c", "user.email=git@coding.local", "commit", "--allow-empty", "--message", "Initialize repository"],
                cancellationToken);
        }
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

    private static async Task<(int ExitCode, string Output, string Error)> RunWithExitCodeAsync(
        string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
        return (process.ExitCode, await stdout, await stderr);
    }

    private static async Task<byte[]> RunBytesAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
        await using var output = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Git operation failed: {error.Trim()}");
        return output.ToArray();
    }
}
