using System.Collections.Concurrent;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Identity.Contracts;
using Platform.Modules.Notifications.Contracts;
using Platform.Modules.Reactions.Contracts;
using Platform.Testing.Containers;
using Platform.Testing.Hosting;
using Platform.Workers;

namespace Platform.Modules.Notifications.IntegrationTests;

/// <summary>A black list for the tests: the pairs «recipient, actor» it holds are suppressed.</summary>
public sealed class FakeSuppression : INotificationSuppression
{
    private readonly ConcurrentDictionary<(Guid Recipient, Guid Actor), bool> _blocked = new();

    public void Block(Guid recipient, Guid actor) => _blocked[(recipient, actor)] = true;

    public Task<IReadOnlySet<Guid>> SuppressedActorsAsync(
        Guid recipientAccountId,
        IReadOnlyCollection<Guid> actorAccountIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(
            actorAccountIds.Where(a => _blocked.ContainsKey((recipientAccountId, a))).ToHashSet());
}

/// <summary>
/// E12-01 (FR-NOTIFY, ADR-0092): the real workers host (<see cref="WorkerHost.CreateBuilder"/>, in-memory bus) turns
/// the events of Comments, Reactions and Blogs into notifications. A reply, a comment on a post, a mention, a reaction
/// and a subscription each notify the person they concern; a person the recipient blocked notifies no one; a repeated
/// event — the same message or a new one of the same action — adds nothing; a deleted account loses its notifications.
/// </summary>
public sealed class NotificationGenerationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeSuppression _suppression = new();
    private IHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var database = await postgres.CreateDatabaseAsync();
        var builder = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Test, "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory",
                "--Mail:Provider", "InMemory", $"--ConnectionStrings:Platform={database}"]);
        builder.Services.AddSingleton<INotificationSuppression>(_suppression);
        _host = builder.Build();
        await HostStartGate.RunAsync(async () =>
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync(Ct);
                await scope.ServiceProvider.GetRequiredService<Preferences.PreferencesDbContext>().Database.MigrateAsync(Ct);
                await scope.ServiceProvider.GetRequiredService<Blogs.BlogsDbContext>().Database.MigrateAsync(Ct);
            }

            await _host.StartAsync(Ct);
        }, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    [Fact]
    public async Task ReplyReactionMentionAndSubscriptionNotify()
    {
        var postAuthor = NewAccount();
        var parentAuthor = NewAccount();
        var mentioned = NewAccount();
        var actor = NewAccount();
        var post = Guid.CreateVersion7(_at);
        var comment = Guid.CreateVersion7(_at);
        var blog = Guid.CreateVersion7(_at);

        await PublishAsync(new CommentPosted(comment, post, postAuthor, actor, Guid.CreateVersion7(_at), parentAuthor,
            [mentioned], _at));
        await PublishAsync(new ReactionPut("post", post, post, postAuthor, actor, "like", _at.AddMinutes(1)));
        await PublishAsync(new BlogSubscribed(blog, BlogKind.Personal, postAuthor, actor, _at.AddMinutes(2)));

        Assert.Equal(
            [NotificationType.PostComment, NotificationType.Reaction, NotificationType.NewSubscriber],
            (await NotificationsOfAsync(postAuthor)).Select(n => n.Type));
        var reply = Assert.Single(await NotificationsOfAsync(parentAuthor));
        Assert.Equal((NotificationType.CommentReply, actor, post, comment), (reply.Type, reply.ActorAccountId, reply.PostId,
            reply.CommentId));
        Assert.Null(reply.ReadAt);
        Assert.Equal(_at, reply.CreatedAt);
        var mention = Assert.Single(await NotificationsOfAsync(mentioned));
        Assert.Equal(NotificationType.Mention, mention.Type);
        Assert.Empty(await NotificationsOfAsync(actor));
    }

    // E10-08, ADR-0117: a repost notifies the original's author, about the original.
    [Fact]
    public async Task RepostNotifiesTheOriginalsAuthor()
    {
        var author = NewAccount();
        var reposter = NewAccount();
        var post = Guid.CreateVersion7(_at);

        await PublishAsync(new Posts.Contracts.PostReposted(Guid.CreateVersion7(_at), post, author, reposter, _at));

        var repost = Assert.Single(await NotificationsOfAsync(author));
        Assert.Equal((NotificationType.Repost, reposter, post), (repost.Type, repost.ActorAccountId, repost.PostId));
        Assert.Empty(await NotificationsOfAsync(reposter));
    }

    [Fact]
    public async Task BlockedPersonNotifiesNoOne()
    {
        var author = NewAccount();
        var blocked = NewAccount();
        var other = NewAccount();
        var post = Guid.CreateVersion7(_at);
        _suppression.Block(author, blocked);

        await PublishAsync(new CommentPosted(Guid.CreateVersion7(_at), post, author, blocked, null, null, [], _at));
        await PublishAsync(new ReactionPut("post", post, post, author, blocked, "like", _at));
        await PublishAsync(new BlogSubscribed(Guid.CreateVersion7(_at), BlogKind.Personal, author, blocked, _at));
        Assert.Empty(await NotificationsOfAsync(author));

        // The black list is the recipient's: the same actions of someone else still notify.
        await PublishAsync(new ReactionPut("post", post, post, author, other, "like", _at));
        Assert.Single(await NotificationsOfAsync(author));
    }

    [Fact]
    public async Task AccountInTheRecipientsBlackListNotifiesNoOne()
    {
        var author = NewAccount();
        var blocked = NewAccount();
        var post = Guid.CreateVersion7(_at);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Preferences.PreferencesDbContext>();
            db.BlockedAccounts.Add(new Preferences.Blocks.BlockedAccount
            {
                OwnerAccountId = author,
                AccountId = blocked,
                BlockedAt = _at,
            });
            await db.SaveChangesAsync(Ct);
        }

        await PublishAsync(new CommentPosted(Guid.CreateVersion7(_at), post, author, blocked, null, null, [], _at));
        await PublishAsync(new ReactionPut("post", post, post, author, blocked, "like", _at));

        Assert.Empty(await NotificationsOfAsync(author));
    }

    [Fact]
    public async Task RepeatedEventsAddNothing()
    {
        var author = NewAccount();
        var reader = NewAccount();
        var post = Guid.CreateVersion7(_at);
        var comment = new CommentPosted(Guid.CreateVersion7(_at), post, author, reader, null, null, [], _at);
        var like = new ReactionPut("post", post, post, author, reader, "like", _at.AddMinutes(1));
        var subscribed = new BlogSubscribed(Guid.CreateVersion7(_at), BlogKind.Personal, author, reader, _at.AddMinutes(2));

        var messageId = await PublishAsync(comment);
        // The same message again: the inbox drops it.
        await _host.Services.GetRequiredService<IBus>().Publish(comment, c => c.MessageId = messageId, Ct);
        // New messages of the same actions: a like taken back and put again, a subscription renewed.
        await PublishAsync(comment);
        await PublishAsync(like);
        await PublishAsync(like with { OccurredAt = _at.AddHours(1) });
        await PublishAsync(subscribed);
        await PublishAsync(subscribed with { OccurredAt = _at.AddHours(1) });

        Assert.Equal(
            [NotificationType.PostComment, NotificationType.Reaction, NotificationType.NewSubscriber],
            (await NotificationsOfAsync(author)).Select(n => n.Type));
    }

    [Fact]
    public async Task DeletedAccountLosesItsNotifications()
    {
        var deleted = NewAccount();
        var other = NewAccount();
        var post = Guid.CreateVersion7(_at);
        await PublishAsync(new ReactionPut("post", post, post, deleted, other, "like", _at));
        await PublishAsync(new ReactionPut("post", post, post, other, deleted, "like", _at));
        var otherPost = Guid.CreateVersion7(_at);
        var third = NewAccount();
        await PublishAsync(new ReactionPut("post", otherPost, otherPost, other, third, "like", _at));

        await PublishAsync(new AccountDeleted(deleted, _at.AddDays(1)));

        Assert.Empty(await NotificationsOfAsync(deleted));
        var kept = Assert.Single(await NotificationsOfAsync(other));
        Assert.Equal(third, kept.ActorAccountId);
    }

    private static Guid NewAccount() => Guid.NewGuid();

    // Publishes the event and waits until the consumer of the Notifications schema has handled it: its inbox record is
    // marked consumed in the same transaction as the notifications.
    private async Task<Guid> PublishAsync<T>(T message)
        where T : class
    {
        var messageId = NewId.NextGuid();
        await _host.Services.GetRequiredService<IBus>().Publish(message, c => c.MessageId = messageId, Ct);
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
                if (await db.Set<InboxState>().AnyAsync(i => i.MessageId == messageId && i.Consumed != null, Ct))
                {
                    return messageId;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, $"{typeof(T).Name} was not handled in {_timeout.TotalSeconds:0}s");
            await Task.Delay(100, Ct);
        }
    }

    private async Task<IReadOnlyList<Notification>> NotificationsOfAsync(Guid account)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Notifications.AsNoTracking()
            .Where(n => n.RecipientAccountId == account)
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Type)
            .ToListAsync(Ct);
    }
}
