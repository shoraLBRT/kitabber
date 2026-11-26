namespace Platform.Modules.Notifications.IntegrationTests;

/// <summary>E12-02 (ADR-0093): the names of the types in the API and what a mark-read request may name.</summary>
public sealed class NotificationViewRulesTests
{
    [Fact]
    public void EveryTypeHasItsOwnName()
    {
        var names = Enum.GetValues<NotificationType>().Select(NotificationEndpoints.TypeName).ToList();

        Assert.Equal(
            ["comment_reply", "post_comment", "reaction", "mention", "new_subscriber"],
            names);
    }

    [Fact]
    public void MarkingNamesOneToAPageOfNotifications()
    {
        Assert.NotNull(NotificationEndpoints.MarkProblem(null));
        Assert.NotNull(NotificationEndpoints.MarkProblem([]));
        Assert.Null(NotificationEndpoints.MarkProblem([Guid.NewGuid()]));
        Assert.Null(NotificationEndpoints.MarkProblem([.. Enumerable.Range(0, NotificationEndpoints.MaxMarked)
            .Select(_ => Guid.NewGuid())]));
        Assert.NotNull(NotificationEndpoints.MarkProblem([.. Enumerable.Range(0, NotificationEndpoints.MaxMarked + 1)
            .Select(_ => Guid.NewGuid())]));
    }
}
