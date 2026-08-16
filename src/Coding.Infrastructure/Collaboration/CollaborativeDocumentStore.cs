using System.Security.Cryptography;
using Coding.Application.Features.Collaboration;
using Coding.Data;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Collaboration;

public sealed class CollaborativeDocumentStore(AppDbContext db) : ICollaborativeDocumentStore
{
    public async Task<CollaborativeSnapshotData?> GetLatestSnapshotAsync(Guid fileId, CancellationToken ct) => await db.CollaborativeDocumentSnapshots.AsNoTracking().Where(x => x.FileId == fileId).OrderByDescending(x => x.SequenceNumber).Select(x => new CollaborativeSnapshotData(x.EncodedState, x.StateVector, x.SequenceNumber, x.ContentHash)).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<CollaborativeUpdateData>> GetUpdatesAfterAsync(Guid fileId, long sequence, CancellationToken ct) => await db.CollaborativeDocumentUpdates.AsNoTracking().Where(x => x.FileId == fileId && x.SequenceNumber > sequence).OrderBy(x => x.SequenceNumber).Select(x => new CollaborativeUpdateData(x.ProjectId, x.FileId, x.UpdateId, x.EncodedUpdate, x.SequenceNumber, x.CreatedAt, x.CreatedByUserId)).ToArrayAsync(ct);
    public async Task<(bool Appended, long SequenceNumber)> AppendUpdateAsync(Guid projectId, Guid fileId, Guid updateId, byte[] update, Guid userId, CancellationToken ct)
    {
        var duplicate = await db.CollaborativeDocumentUpdates.AsNoTracking().Where(x => x.FileId == fileId && x.UpdateId == updateId).Select(x => (long?)x.SequenceNumber).SingleOrDefaultAsync(ct);
        if (duplicate is not null) return (false, duplicate.Value);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var sequence = Math.Max(
                await db.CollaborativeDocumentUpdates.Where(x => x.FileId == fileId).Select(x => (long?)x.SequenceNumber).MaxAsync(ct) ?? 0,
                await db.CollaborativeDocumentSnapshots.Where(x => x.FileId == fileId).Select(x => (long?)x.SequenceNumber).MaxAsync(ct) ?? 0) + 1;
            db.CollaborativeDocumentUpdates.Add(new CollaborativeDocumentUpdate
            {
                ID = Guid.NewGuid(),
                ProjectId = projectId,
                FileId = fileId,
                UpdateId = updateId,
                EncodedUpdate = update,
                SequenceNumber = sequence,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            });
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return (true, sequence);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                var existingSequence = await db.CollaborativeDocumentUpdates.AsNoTracking()
                    .Where(x => x.FileId == fileId && x.UpdateId == updateId)
                    .Select(x => (long?)x.SequenceNumber)
                    .SingleOrDefaultAsync(ct);
                return (false, existingSequence ?? 0);
            }
        });
    }
    public Task SaveSnapshotAsync(Guid projectId, Guid fileId, byte[] state, byte[] vector, long sequence, Guid userId, CancellationToken ct) { db.CollaborativeDocumentSnapshots.Add(new CollaborativeDocumentSnapshot { ID = Guid.NewGuid(), ProjectId = projectId, FileId = fileId, EncodedState = state, StateVector = vector, SequenceNumber = sequence, CreatedAt = DateTime.UtcNow, CreatedByUserId = userId, ContentHash = Convert.ToHexString(SHA256.HashData(state)).ToLowerInvariant() }); return db.SaveChangesAsync(ct); }
    public async Task CompactDocumentAsync(Guid projectId, Guid fileId, byte[] state, byte[] vector, long throughSequence, Guid userId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await SaveSnapshotAsync(projectId, fileId, state, vector, throughSequence, userId, ct);
            await db.CollaborativeDocumentUpdates
                .Where(item => item.FileId == fileId && item.SequenceNumber <= throughSequence)
                .ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
