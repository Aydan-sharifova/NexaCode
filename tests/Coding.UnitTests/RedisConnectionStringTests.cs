using Coding.Infrastructure.Caching;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class RedisConnectionStringTests
{
    [Fact]
    public void Redis_uri_is_converted_to_stack_exchange_format() =>
        RedisConnectionString.Normalize("redis://cache.internal:6380")
            .Should().Be("cache.internal:6380,abortConnect=false");

    [Fact]
    public void Secure_uri_preserves_password_and_tls() =>
        RedisConnectionString.Normalize("rediss://default:p%40ss@cache.example:6380")
            .Should().Be("cache.example:6380,password=p@ss,ssl=true,abortConnect=false");

    [Fact]
    public void Native_configuration_is_not_rewritten() =>
        RedisConnectionString.Normalize("redis:6379,password=secret,abortConnect=false")
            .Should().Be("redis:6379,password=secret,abortConnect=false");
}
