using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Persistence;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// The Api host with the Comments module on PostgreSQL and Redis in Testcontainers, signed in by the test scheme, with
/// branches limited to <see cref="MaxDepth"/> levels by the setting.
/// </summary>
public sealed class CommentsApi : IntegrationApiFactory
{
    public const int MaxDepth = 3;

    public CommentsApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
        WithSetting(CommentsSettings.MaxDepthKey, MaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WithModule(services => services.AddTestAuthentication());
    }

    /// <summary>The directory of reactions is reference data from the migration (ADR-0095): reactions need it.</summary>
    protected override IEnumerable<string> KeepTables => ["reactions.reaction_set"];
}

/// <summary>
/// E09-01: comments under a post through <c>/api/v1/posts/{postId}/comments</c> and <c>/api/v1/comments/{id}/branch</c>
/// — replying, reading a tree and a branch with one query, the depth limit, guest comments and the rights.
/// </summary>
public sealed class CommentApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _reader;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _reader = await SaveConfirmedAccountAsync(1, "reader@example.com");
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReaderRepliesToThePostAndToAComment()
    {
        var post = await SavePostAsync(publish: true);

        using var created = await CommentAsync(post.Id, " Первый! ", parentId: null, _reader);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var root = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var rootId = root.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/comments/{rootId}/branch", created.Headers.Location?.OriginalString);
        Assert.Equal("Первый!", root.GetProperty("text").GetString());
        Assert.Equal(1, root.GetProperty("depth").GetInt32());
        Assert.Equal("native", root.GetProperty("kind").GetString());
        Assert.Equal(_reader, root.GetProperty("authorAccountId").GetGuid());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("guest").ValueKind);

        using var replied = await CommentAsync(post.Id, "Ответ", rootId, _reader);
        Assert.Equal(HttpStatusCode.Created, replied.StatusCode);
        var reply = await replied.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(rootId, reply.GetProperty("parentId").GetGuid());
        Assert.Equal(2, reply.GetProperty("depth").GetInt32());
    }

    [Fact]
    public async Task NewCommentIsPublishedWithThePeopleItConcerns()
    {
        var events = new System.Collections.Concurrent.ConcurrentQueue<CommentPosted>();
        var endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"comments-tests-comment-posted-{Guid.NewGuid():N}",
            (_, e) => e.Handler<CommentPosted>(c =>
            {
                events.Enqueue(c.Message);
                return Task.CompletedTask;
            }));
        await endpoint.Ready;
        try
        {
            var post = await SavePostAsync(publish: true);
            var other = await SaveConfirmedAccountAsync(2, "other@example.com");
            var rootId = await CreateAsync(post.Id, "Первый", parentId: null);
            using var replied = await CommentAsync(post.Id, "Ответ", rootId, other);
            var replyId = (await replied.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (events.Count(e => e.PostId == post.Id) < 2)
            {
                Assert.True(DateTime.UtcNow < deadline, "the comments were not published in 30s");
                await Task.Delay(100, Ct);
            }

            var root = Assert.Single(events, e => e.CommentId == rootId);
            Assert.Equal((post.AuthorAccountId, _reader, (Guid?)null, (Guid?)null), (root.PostAuthorAccountId,
                root.AuthorAccountId, root.ParentCommentId, root.ParentAuthorAccountId));
            var reply = Assert.Single(events, e => e.CommentId == replyId);
            Assert.Equal((other, (Guid?)rootId, (Guid?)_reader), (reply.AuthorAccountId, reply.ParentCommentId,
                reply.ParentAuthorAccountId));
            Assert.Empty(reply.MentionedAccountIds);
        }
        finally
        {
            await endpoint.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TreeComesInTheOrderOfTheTree()
    {
        var post = await SavePostAsync(publish: true);
        var first = await CreateAsync(post.Id, "1", parentId: null);
        var second = await CreateAsync(post.Id, "2", parentId: null);
        var firstReply = await CreateAsync(post.Id, "1.1", first);
        var deeper = await CreateAsync(post.Id, "1.1.1", firstReply);
        var secondReply = await CreateAsync(post.Id, "2.1", second);
        var laterFirstReply = await CreateAsync(post.Id, "1.2", first);

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, account: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tree = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(post.Id, tree.GetProperty("postId").GetGuid());
        Assert.Equal(CommentsApi.MaxDepth, tree.GetProperty("maxDepth").GetInt32());
        Assert.Equal([first, firstReply, deeper, laterFirstReply, second, secondReply], IdsOf(tree));
    }

    [Fact]
    public async Task TreeComesPopularFirstUnlessTheReadAsksForTheNewest()
    {
        var post = await SavePostAsync(publish: true);
        var quiet = await CreateAsync(post.Id, "тихий", parentId: null);
        var busy = await CreateAsync(post.Id, "обсуждаемый", parentId: null);
        var latest = await CreateAsync(post.Id, "последний", parentId: null);
        var reply = await CreateAsync(post.Id, "ответ", busy);
        var otherReply = await CreateAsync(post.Id, "ещё ответ", busy);

        using var byDefault = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, null);
        using var popular = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?sort=popular", null, null);
        using var byDate = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?sort=date", null, null);
        using var dateTop = await SendAsync(
            HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?sort=date&levels=1", null, null);

        Assert.Equal(HttpStatusCode.OK, byDefault.StatusCode);
        Assert.Equal([busy, reply, otherReply, quiet, latest], IdsOf(await byDefault.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        Assert.Equal([busy, reply, otherReply, quiet, latest], IdsOf(await popular.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        Assert.Equal([latest, busy, reply, otherReply, quiet], IdsOf(await byDate.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        var top = await dateTop.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal([latest, busy, quiet], IdsOf(top));
        Assert.Equal([0, 2, 0], RepliesOf(top));
    }

    [Fact]
    public async Task PopularPutsTheHigherRatedThreadFirstAndFollowsTheChangesOfItsReactions()
    {
        var post = await SavePostAsync(publish: true);
        var quiet = await CreateAsync(post.Id, "тихий, но оценённый", parentId: null);
        var busy = await CreateAsync(post.Id, "обсуждаемый", parentId: null);
        var reply = await CreateAsync(post.Id, "ответ", busy);
        var fan = await SaveConfirmedAccountAsync(2, "fan@example.com");
        var otherFan = await SaveConfirmedAccountAsync(3, "other-fan@example.com");

        using var liked = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{quiet}/reaction", """{"kind":"heart"}""", fan);
        using var otherLiked = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{quiet}/reaction", """{"kind":"heart"}""", otherFan);
        Assert.Equal(HttpStatusCode.OK, otherLiked.StatusCode);
        using var rated = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, null);
        using var byDate = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?sort=date", null, null);

        Assert.Equal([quiet, busy, reply], IdsOf(await rated.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        Assert.Equal([busy, reply, quiet], IdsOf(await byDate.Content.ReadFromJsonAsync<JsonElement>(Ct)));

        using var taken = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{quiet}/reaction", null, fan);
        using var otherTaken = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{quiet}/reaction", null, otherFan);
        using var unrated = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, null);

        Assert.Equal([busy, reply, quiet], IdsOf(await unrated.Content.ReadFromJsonAsync<JsonElement>(Ct)));
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("Date")]
    public async Task UnknownSortIsAValidationError(string sort)
    {
        var post = await SavePostAsync(publish: true);
        await CreateAsync(post.Id, "1", parentId: null);

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?sort={sort}", null, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["sort"], await ErrorFieldsOf(response));
    }

    [Fact]
    public async Task BranchIsTheCommentAndEveryReplyUnderIt()
    {
        var post = await SavePostAsync(publish: true);
        var first = await CreateAsync(post.Id, "1", parentId: null);
        var reply = await CreateAsync(post.Id, "1.1", first);
        var deeper = await CreateAsync(post.Id, "1.1.1", reply);
        await CreateAsync(post.Id, "2", parentId: null);
        var otherPost = await SavePostAsync(publish: true);
        await CreateAsync(otherPost.Id, "чужой", parentId: null);

        using var whole = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{first}/branch", null, account: null);
        using var inner = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{reply}/branch", null, account: null);

        Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
        Assert.Equal([first, reply, deeper], IdsOf(await whole.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        Assert.Equal([reply, deeper], IdsOf(await inner.Content.ReadFromJsonAsync<JsonElement>(Ct)));
    }

    [Fact]
    public async Task PlaceNamesTheLevelAndTheCommentsAboveRootFirst()
    {
        var post = await SavePostAsync(publish: true);
        var first = await CreateAsync(post.Id, "1", parentId: null);
        var reply = await CreateAsync(post.Id, "1.1", first);
        var deeper = await CreateAsync(post.Id, "1.1.1", reply);

        using var root = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{first}/place", null, account: null);
        using var deep = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{deeper}/place", null, account: null);

        Assert.Equal(HttpStatusCode.OK, deep.StatusCode);
        var rootPlace = await root.Content.ReadFromJsonAsync<CommentPlaceView>(Ct);
        Assert.Equal(first, rootPlace!.Id);
        Assert.Equal(post.Id, rootPlace.PostId);
        Assert.Equal(1, rootPlace.Depth);
        Assert.Empty(rootPlace.Ancestors);
        var deepPlace = await deep.Content.ReadFromJsonAsync<CommentPlaceView>(Ct);
        Assert.Equal(3, deepPlace!.Depth);
        Assert.Equal([first, reply], deepPlace.Ancestors);
    }

    [Fact]
    public async Task PlaceOfAnUnknownCommentOrOfADeletedPostIsNotFound()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Текст", parentId: null);
        await DeletePostAsync(post.Id);
        var unknown = Guid.CreateVersion7(api.Time.GetUtcNow());

        using var ofDeleted = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{comment}/place", null, account: null);
        using var ofUnknown = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{unknown}/place", null, account: null);

        Assert.Equal(HttpStatusCode.NotFound, ofDeleted.StatusCode);
        Assert.Equal(CommentEndpoints.CommentNotFoundCode, await CodeOf(ofDeleted));
        Assert.Equal(HttpStatusCode.NotFound, ofUnknown.StatusCode);
        Assert.Equal(CommentEndpoints.CommentNotFoundCode, await CodeOf(ofUnknown));
    }

    [Fact]
    public async Task ReadStopsSomeLevelsDownAndCountsTheHiddenReplies()
    {
        var post = await SavePostAsync(publish: true);
        var first = await CreateAsync(post.Id, "1", parentId: null);
        var reply = await CreateAsync(post.Id, "1.1", first);
        var deeper = await CreateAsync(post.Id, "1.1.1", reply);
        var otherReply = await CreateAsync(post.Id, "1.2", first);
        var second = await CreateAsync(post.Id, "2", parentId: null);

        using var whole = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, account: null);
        using var top = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?levels=1", null, null);
        using var branch = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{reply}/branch?levels=1", null, null);
        using var rootBranch = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{first}/branch?levels=1", null, null);

        var wholeTree = await whole.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal([first, reply, deeper, otherReply, second], IdsOf(wholeTree));
        Assert.Equal([3, 1, 0, 0, 0], RepliesOf(wholeTree));
        var topTree = await top.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal([first, second], IdsOf(topTree));
        Assert.Equal([3, 0], RepliesOf(topTree));
        Assert.Equal([reply, deeper], IdsOf(await branch.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        var rootTree = await rootBranch.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal([first, reply, otherReply], IdsOf(rootTree));
        Assert.Equal([3, 1, 0], RepliesOf(rootTree));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public async Task LevelsOutsideTheLimitAreValidationErrors(int levels)
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "1", parentId: null);

        using var tree = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?levels={levels}", null, null);
        using var branch = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{comment}/branch?levels={levels}", null, null);

        Assert.Equal(HttpStatusCode.BadRequest, tree.StatusCode);
        Assert.Equal(["levels"], await ErrorFieldsOf(tree));
        Assert.Equal(HttpStatusCode.BadRequest, branch.StatusCode);
        Assert.Equal(["levels"], await ErrorFieldsOf(branch));
    }

    [Fact]
    public async Task PostWithAThousandCommentsComesInOneRead()
    {
        var post = await SavePostAsync(publish: true);
        var now = api.Time.GetUtcNow();
        var comments = new List<Comment>(1000);
        for (var n = 0; n < 1000; n++)
        {
            // 250 threads of four levels: a reply to the post and three replies, each under the one before.
            var parent = n % 4 == 0 ? null : comments[^1];
            comments.Add(Comment.CreateNative(_reader, post.Id, parent, $"комментарий {n}", now.AddMilliseconds(n)));
        }

        await api.SaveAsync<CommentsDbContext>([.. comments]);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments?levels=3", null, null);

        watch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tree = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(750, IdsOf(tree).Length);
        Assert.Equal(250, RepliesOf(tree).Count(replies => replies == 1));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"The tree took {watch.Elapsed}.");
    }

    [Fact]
    public async Task TreeAndBranchAreReadWithOneQueryEach()
    {
        var post = await SavePostAsync(publish: true);
        var root = await CreateAsync(post.Id, "1", parentId: null);
        var reply = await CreateAsync(post.Id, "1.1", root);
        await CreateAsync(post.Id, "1.1.1", reply);
        await CreateAsync(post.Id, "2", parentId: null);
        var counter = new CommandCounter();
        await using var db = OpenComments(counter);

        var tree = await CommentTree.OfPost(db, post.Id).ToListAsync(Ct);
        Assert.Equal(1, counter.Count);
        var branch = await CommentTree.Branch(db, root).ToListAsync(Ct);
        Assert.Equal(2, counter.Count);

        Assert.Equal(4, tree.Count);
        Assert.Equal(3, branch.Count);
        Assert.Equal(root, branch[0].Id);
        Assert.Empty(await CommentTree.Branch(db, Guid.CreateVersion7(api.Time.GetUtcNow())).ToListAsync(Ct));
    }

    [Fact]
    public async Task ReplyDeeperThanTheSettingIsRefused()
    {
        var post = await SavePostAsync(publish: true);
        var parent = await CreateAsync(post.Id, "1", parentId: null);
        for (var depth = 2; depth <= CommentsApi.MaxDepth; depth++)
        {
            parent = await CreateAsync(post.Id, $"уровень {depth}", parent);
        }

        using var response = await CommentAsync(post.Id, "слишком глубоко", parent, _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["parentId"], await ErrorFieldsOf(response));
        Assert.Equal(CommentsApi.MaxDepth, await CountAsync());
    }

    [Fact]
    public async Task GuestCommentIsShownWithoutAnAccountAndTakesNoReplies()
    {
        var post = await SavePostAsync(publish: true);
        var guest = Comment.CreateGuest(
            post.Id, parent: null, new GuestAuthor("Иван из Telegram", new Uri("https://t.me/c/123/42")),
            new CommentSource("-100123", "42"), "Привет из канала", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);

        using var tree = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, account: null);
        using var reply = await CommentAsync(post.Id, "Ответ гостю", guest.Id, _reader);

        var view = Assert.Single((await tree.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("comments")
            .EnumerateArray());
        Assert.Equal("guest", view.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("authorAccountId").ValueKind);
        Assert.Equal("Иван из Telegram", view.GetProperty("guest").GetProperty("name").GetString());
        Assert.Equal("https://t.me/c/123/42", view.GetProperty("guest").GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
        Assert.Equal(["parentId"], await ErrorFieldsOf(reply));
    }

    [Fact]
    public async Task SameOutsideMessageIsCarriedOverOnce()
    {
        var post = await SavePostAsync(publish: true);
        var source = new CommentSource("-100123", "42");
        await api.SaveAsync<CommentsDbContext>(Comment.CreateGuest(
            post.Id, null, new GuestAuthor("Иван", null), source, "Раз", api.Time.GetUtcNow()));

        await Assert.ThrowsAsync<DbUpdateException>(() => api.SaveAsync<CommentsDbContext>(Comment.CreateGuest(
            post.Id, null, new GuestAuthor("Иван", null), source, "Два", api.Time.GetUtcNow())));
    }

    [Fact]
    public async Task EmptyOrTooLongTextAndAForeignParentAreValidationErrors()
    {
        var post = await SavePostAsync(publish: true);
        var otherPost = await SavePostAsync(publish: true);
        var foreign = await CreateAsync(otherPost.Id, "под другим постом", parentId: null);

        using var empty = await CommentAsync(post.Id, "   ", parentId: null, _reader);
        using var tooLong = await CommentAsync(post.Id, new string('а', CommentRules.MaxTextLength + 1), null, _reader);
        using var fits = await CommentAsync(post.Id, new string('а', CommentRules.MaxTextLength), null, _reader);
        using var wrongParent = await CommentAsync(post.Id, "Ответ", foreign, _reader);

        Assert.Equal(["text"], await ErrorFieldsOf(empty));
        Assert.Equal(["text"], await ErrorFieldsOf(tooLong));
        Assert.Equal(HttpStatusCode.Created, fits.StatusCode);
        Assert.Equal(["parentId"], await ErrorFieldsOf(wrongParent));
    }

    [Fact]
    public async Task DraftOrUnknownPostHasNoComments()
    {
        var draft = await SavePostAsync(publish: false);
        var unknown = Guid.CreateVersion7(api.Time.GetUtcNow());

        using var toDraft = await CommentAsync(draft.Id, "Текст", parentId: null, _reader);
        using var toUnknown = await CommentAsync(unknown, "Текст", parentId: null, _reader);
        using var readDraft = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}/comments", null, account: null);
        using var noBranch = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{unknown}/branch", null, account: null);

        Assert.Equal(HttpStatusCode.NotFound, toDraft.StatusCode);
        Assert.Equal(CommentEndpoints.PostNotFoundCode, await CodeOf(toDraft));
        Assert.Equal(CommentEndpoints.PostNotFoundCode, await CodeOf(toUnknown));
        Assert.Equal(CommentEndpoints.PostNotFoundCode, await CodeOf(readDraft));
        Assert.Equal(HttpStatusCode.NotFound, noBranch.StatusCode);
        Assert.Equal(CommentEndpoints.CommentNotFoundCode, await CodeOf(noBranch));
    }

    [Fact]
    public async Task BranchOfADeletedPostIsNotFound()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Текст", parentId: null);
        await DeletePostAsync(post.Id);

        using var branch = await SendAsync(HttpMethod.Get, $"/api/v1/comments/{comment}/branch", null, account: null);

        Assert.Equal(HttpStatusCode.NotFound, branch.StatusCode);
    }

    [Fact]
    public async Task OnlyASignedInAccountWithAConfirmedEmailComments()
    {
        var post = await SavePostAsync(publish: true);
        var unconfirmed = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(5));

        using var anonymous = await CommentAsync(post.Id, "Текст", parentId: null, account: null);
        using var withoutEmail = await CommentAsync(post.Id, "Текст", parentId: null, unconfirmed);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutEmail.StatusCode);
        Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await CodeOf(withoutEmail));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task AuthorChangesTheTextWhileTheTimeLastsAndTheTreeMarksIt()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Было", parentId: null);
        var reply = await CreateAsync(post.Id, "Ответ", comment);

        using var edited = await EditAsync(comment, " Стало ", _reader);

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var view = await edited.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Стало", view.GetProperty("text").GetString());
        Assert.Equal(api.Time.GetUtcNow(), view.GetProperty("editedAt").GetDateTimeOffset());
        Assert.Equal(1, view.GetProperty("replies").GetInt32());
        using var tree = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, account: null);
        var comments = (await tree.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("comments");
        var shown = comments.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == comment);
        Assert.Equal("Стало", shown.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.String, shown.GetProperty("editedAt").ValueKind);
        Assert.Equal(
            shown.GetProperty("createdAt").GetDateTimeOffset().AddMinutes(CommentLimits.DefaultEditMinutes),
            shown.GetProperty("editableUntil").GetDateTimeOffset());
        var untouched = comments.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == reply);
        Assert.Equal(JsonValueKind.Null, untouched.GetProperty("editedAt").ValueKind);
        Assert.False(untouched.GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task ChangeAfterTheTimeIsRefusedByTheServer()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Было", parentId: null);
        api.Time.Advance(TimeSpan.FromMinutes(CommentLimits.DefaultEditMinutes));

        using var late = await EditAsync(comment, "Стало", _reader);

        Assert.Equal(HttpStatusCode.Forbidden, late.StatusCode);
        Assert.Equal(CommentEndpoints.EditClosedCode, await CodeOf(late));
        Assert.Equal("Было", await TextOfAsync(comment));
    }

    [Fact]
    public async Task OnlyTheAuthorChangesOrDeletesAComment()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Моё", parentId: null);
        var stranger = await SaveConfirmedAccountAsync(2, "stranger@example.com");

        using var anonymousEdit = await EditAsync(comment, "Чужое", account: null);
        using var anonymousDelete = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment}", null, null);
        using var foreignEdit = await EditAsync(comment, "Чужое", stranger);
        using var foreignDelete = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment}", null, stranger);
        using var unknown = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{Guid.CreateVersion7()}", null, _reader);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignEdit.StatusCode);
        Assert.Equal(CommentEndpoints.NotAuthorCode, await CodeOf(foreignEdit));
        Assert.Equal(HttpStatusCode.Forbidden, foreignDelete.StatusCode);
        Assert.Equal(CommentEndpoints.NotAuthorCode, await CodeOf(foreignDelete));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Моё", await TextOfAsync(comment));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ChangeToAnEmptyTextIsAValidationError(string? text)
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Было", parentId: null);

        using var response = await EditAsync(comment, text, _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["text"], await ErrorFieldsOf(response));
        Assert.Equal("Было", await TextOfAsync(comment));
    }

    [Fact]
    public async Task DeletedCommentWithRepliesStaysAsAStubAndGoesWithTheLastOne()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await CreateAsync(post.Id, "Удалю", parentId: null);
        var reply = await CreateAsync(post.Id, "Ответ", comment);
        var alone = await CreateAsync(post.Id, "Без ответов", parentId: null);

        using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment}", null, _reader);
        using var deletedAlone = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{alone}", null, _reader);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletedAlone.StatusCode);
        using var tree = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments", null, account: null);
        var read = await tree.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal([comment, reply], IdsOf(read));
        var stub = read.GetProperty("comments")[0];
        Assert.True(stub.GetProperty("deleted").GetBoolean());
        Assert.Equal(string.Empty, stub.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, stub.GetProperty("authorAccountId").ValueKind);
        Assert.Equal(JsonValueKind.Null, stub.GetProperty("editableUntil").ValueKind);
        Assert.Equal(1, stub.GetProperty("replies").GetInt32());

        using var replyToStub = await CommentAsync(post.Id, "Ещё", comment, _reader);
        using var again = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment}", null, _reader);
        using var editStub = await EditAsync(comment, "Вернул", _reader);
        Assert.Equal(HttpStatusCode.BadRequest, replyToStub.StatusCode);
        Assert.Equal(["parentId"], await ErrorFieldsOf(replyToStub));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, editStub.StatusCode);

        using var lastReply = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{reply}", null, _reader);
        Assert.Equal(HttpStatusCode.NoContent, lastReply.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task AccountReadsWhichCommentsUnderThePostAreItsOwn()
    {
        var post = await SavePostAsync(publish: true);
        var stranger = await SaveConfirmedAccountAsync(2, "stranger@example.com");
        var own = await CreateAsync(post.Id, "Моё", parentId: null);
        using var foreign = await CommentAsync(post.Id, "Чужое", own, stranger);
        var gone = await CreateAsync(post.Id, "Удалю", parentId: null);
        using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{gone}", null, _reader);

        using var mine = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments/mine", null, _reader);
        using var anonymous = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/comments/mine", null, null);

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var ids = (await mine.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ids");
        Assert.Equal([own], [.. ids.EnumerateArray().Select(i => i.GetGuid())]);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private Task<HttpResponseMessage> EditAsync(Guid commentId, string? text, Guid? account) =>
        SendAsync(HttpMethod.Patch, $"/api/v1/comments/{commentId}", JsonSerializer.Serialize(new { text }), account);

    private async Task<string> TextOfAsync(Guid commentId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommentsDbContext>().Comments
            .Where(c => c.Id == commentId).Select(c => c.Text).SingleAsync(Ct);
    }

    private async Task<Guid> CreateAsync(Guid postId, string text, Guid? parentId)
    {
        using var response = await CommentAsync(postId, text, parentId, _reader);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Every comment a moment after the one before: the order of siblings is the order of their creation.
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CommentAsync(Guid postId, string text, Guid? parentId, Guid? account) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/posts/{postId}/comments",
            JsonSerializer.Serialize(new { text, parentId }),
            account);

    private async Task<Post> SavePostAsync(bool publish)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(
            Guid.CreateVersion7(now), Guid.CreateVersion7(now), "Пост", PostFactory.Body("Текст поста."), now);
        if (publish)
        {
            post.Publish(now);
        }

        await api.SaveAsync<PostsDbContext>(post);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return post;
    }

    private async Task DeletePostAsync(Guid postId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        var post = await db.Posts.SingleAsync(p => p.Id == postId, Ct);
        post.Delete(api.Time.GetUtcNow());
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SaveConfirmedAccountAsync(int number, string email)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(number)),
            CreatedAt = api.Time.GetUtcNow(),
        };
        account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    private CommentsDbContext OpenComments(CommandCounter counter)
    {
        var options = new DbContextOptionsBuilder<CommentsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<CommentsDbContext>(
            options, api.DatabaseConnectionString);
        options.AddInterceptors(counter);
        return new CommentsDbContext(options.Options);
    }

    private async Task<int> CountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommentsDbContext>().Comments.CountAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? content, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }

    private static Guid[] IdsOf(JsonElement tree) =>
        [.. tree.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("id").GetGuid())];

    private static int[] RepliesOf(JsonElement tree) =>
        [.. tree.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("replies").GetInt32())];

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<string[]> ErrorFieldsOf(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").EnumerateObject()
            .Select(e => e.Name).Order(StringComparer.Ordinal)];

    /// <summary>Counts the SQL commands a context sends.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
