using System.Security.Cryptography;
using Coding.Application.Features.Users;
using Coding.Data;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class PublicUserIdGenerator(AppDbContext db) : IPublicUserIdGenerator
{
    internal const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    internal const int Length = 8;

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var candidate = CreateCandidate();
            if (!await db.Users.IgnoreQueryFilters().AnyAsync(user => user.PublicId == candidate, cancellationToken))
                return candidate;
        }
        throw new InvalidOperationException("A unique public user identifier could not be generated.");
    }

    public static string CreateCandidate()
    {
        var chars = new char[Length];
        for (var index = 0; index < chars.Length; index++)
            chars[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
