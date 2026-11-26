using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>E02-12: the default set of reactions and the pictures behind it (ADR-0095). No database.</summary>
public sealed class ReactionSetDefaultsTests
{
    [Fact]
    public void EveryDefaultReactionHasAPicture() =>
        Assert.All(ReactionSetEntry.Defaults, entry => Assert.True(ReactionGraphics.Has(entry.Code), entry.Code));

    [Fact]
    public void DefaultsHaveDistinctCodesAndPlacesAndShortNames()
    {
        var defaults = ReactionSetEntry.Defaults;
        Assert.Equal(defaults.Count, defaults.Select(e => e.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(1, defaults.Count), defaults.Select(e => e.Position).Order());
        Assert.All(defaults, entry =>
        {
            Assert.InRange(entry.Code.Length, 1, ReactionSetEntry.CodeMaxLength);
            Assert.False(string.IsNullOrWhiteSpace(entry.Title));
            Assert.InRange(entry.Title.Length, 1, ReactionSetEntry.TitleMaxLength);
            Assert.True(entry.Enabled);
        });
    }

    [Fact]
    public void TheExistingLikeIsTheFirstReaction()
    {
        Assert.Equal("like", ReactionSetEntry.Defaults[0].Code);
        Assert.Equal("Нравится", ReactionSetEntry.Defaults[0].Title);
    }

    [Fact]
    public void OnlyShippedCodesHavePictures()
    {
        Assert.False(ReactionGraphics.Has(null));
        Assert.False(ReactionGraphics.Has("Like"));
        Assert.False(ReactionGraphics.Has("dislike"));
    }
}

/// <summary>E02-12: <c>GET /api/v1/reactions/set</c> reads the directory the migration seeds (ADR-0095).</summary>
public sealed class ReactionSetApiTests(ReactionsApi api) : IClassFixture<ReactionsApi>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GuestReadsTheDefaultSetInItsOrder()
    {
        using var response = await api.CreateClient().GetAsync(new Uri("/api/v1/reactions/set", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<ReactionSetItem>>(Ct);
        Assert.Equal(
            ReactionSetEntry.Defaults.Select(e => new ReactionSetItem(e.Code, e.Title)),
            items);
    }

    [Fact]
    public async Task CodesWithoutAPictureAreNotOffered()
    {
        // A unique code of this test: it has no picture, so the set leaves it out even when it is enabled.
        var unknown = $"t{Guid.NewGuid():N}"[..ReactionSetEntry.CodeMaxLength];
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReactionsDbContext>();
            db.ReactionSet.Add(new ReactionSetEntry { Code = unknown, Title = "Без картинки", Position = 0 });
            await db.SaveChangesAsync(Ct);
        }

        try
        {
            var items = await api.CreateClient().GetFromJsonAsync<List<ReactionSetItem>>(
                new Uri("/api/v1/reactions/set", UriKind.Relative), Ct);
            Assert.DoesNotContain(items!, item => item.Code == unknown);
            Assert.Equal(ReactionSetEntry.Defaults.Count, items!.Count);
        }
        finally
        {
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ReactionsDbContext>();
            await db.ReactionSet.Where(e => e.Code == unknown).ExecuteDeleteAsync(Ct);
        }
    }
}
