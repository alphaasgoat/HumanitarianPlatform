using FluentAssertions;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class NotificationDispatchServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero));
    private readonly ScriptedChannel _email = new(NotificationChannel.Email);
    private readonly NotificationDispatchService _sut;

    public NotificationDispatchServiceTests()
    {
        var options = new DispatchOptions { MaxAttempts = 3, BaseBackoff = TimeSpan.FromSeconds(30), MaxBackoff = TimeSpan.FromMinutes(10) };
        _sut = new NotificationDispatchService(_uow, new NotificationChannelFactory([_email]), _time, options);
    }

    private sealed class ScriptedChannel(NotificationChannel channel) : INotificationChannel
    {
        public NotificationChannel Channel => channel;
        public Func<OutboundMessage, ChannelSendResult> Behaviour { get; set; } = _ => new ChannelSendResult(true, "test");
        public List<OutboundMessage> Sent { get; } = new();

        public Task<ChannelSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(Behaviour(message));
        }
    }

    private NotificationDelivery AddDelivery(NotificationChannel channel = NotificationChannel.Email, DateTime? due = null)
    {
        var n = new Notification { RecipientUserId = Guid.NewGuid(), Title = "Hello", Body = "World", Language = "en", DedupeKey = Guid.NewGuid().ToString() };
        _uow.Repo<Notification>().Items.Add(n);
        var d = new NotificationDelivery
        {
            NotificationId = n.Id, Channel = channel, Destination = "a@b.co", NextAttemptUtc = due ?? _time.GetUtcNow().UtcDateTime
        };
        _uow.Repo<NotificationDelivery>().Items.Add(d);
        return d;
    }

    [Fact]
    public async Task ADueMessage_IsSent_AndMarkedAsSent()
    {
        var d = AddDelivery();

        var summary = await _sut.ProcessDueAsync();

        summary.Should().Be(new DispatchSummary(1, 1, 0, 0));
        d.Status.Should().Be(DeliveryStatus.Sent);
        d.Provider.Should().Be("test");
        d.SentAtUtc.Should().NotBeNull();
        d.Attempts.Should().Be(1);
        _email.Sent.Single().Subject.Should().Be("Hello");
    }

    [Fact]
    public async Task ProcessingTwice_NeverSendsTheSameMessageTwice()
    {
        AddDelivery();

        await _sut.ProcessDueAsync();
        await _sut.ProcessDueAsync();

        _email.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task AMessageNotYetDue_IsLeftAlone()
    {
        var d = AddDelivery(due: _time.GetUtcNow().UtcDateTime.AddMinutes(5));

        var summary = await _sut.ProcessDueAsync();

        summary.Claimed.Should().Be(0);
        d.Status.Should().Be(DeliveryStatus.Pending);
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AFailedSend_IsRetriedLater_WithExponentialBackOff()
    {
        _email.Behaviour = _ => new ChannelSendResult(false, "smtp", "server down");
        var d = AddDelivery();

        var first = await _sut.ProcessDueAsync();

        first.Retried.Should().Be(1);
        d.Status.Should().Be(DeliveryStatus.Pending);
        d.Attempts.Should().Be(1);
        d.LastError.Should().Be("server down");
        d.NextAttemptUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddSeconds(30));

        // not due yet -> nothing happens
        (await _sut.ProcessDueAsync()).Claimed.Should().Be(0);

        // after the wait it is tried again, and the wait doubles
        _time.Advance(TimeSpan.FromSeconds(31));
        await _sut.ProcessDueAsync();
        d.Attempts.Should().Be(2);
        d.NextAttemptUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddSeconds(60));
    }

    [Fact]
    public async Task AfterTheLastAttempt_TheMessageIsParkedAsFailed()
    {
        _email.Behaviour = _ => new ChannelSendResult(false, "smtp", "server down");
        var d = AddDelivery();

        for (var i = 0; i < 3; i++)
        {
            await _sut.ProcessDueAsync();
            _time.Advance(TimeSpan.FromMinutes(20));
        }

        d.Status.Should().Be(DeliveryStatus.Failed);
        d.Attempts.Should().Be(3);
        (await _sut.ProcessDueAsync()).Claimed.Should().Be(0);
    }

    [Fact]
    public async Task APermanentError_IsNotRetried()
    {
        _email.Behaviour = _ => new ChannelSendResult(false, "smtp", ChannelSendResult.PermanentPrefix + "invalid address");
        var d = AddDelivery();

        var summary = await _sut.ProcessDueAsync();

        summary.Failed.Should().Be(1);
        d.Status.Should().Be(DeliveryStatus.Failed);
        d.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task AChannelThatThrows_IsTreatedAsAFailedAttempt_NotACrash()
    {
        _email.Behaviour = _ => throw new InvalidOperationException("socket closed");
        var d = AddDelivery();

        var summary = await _sut.ProcessDueAsync();

        summary.Retried.Should().Be(1);
        d.LastError.Should().Contain("socket closed");
    }

    [Fact]
    public async Task ACrashedWorkersMessage_IsPickedUpAgainWhenItsLeaseExpires()
    {
        var d = AddDelivery();
        d.Status = DeliveryStatus.Processing;
        d.LockedUntilUtc = _time.GetUtcNow().UtcDateTime.AddMinutes(2);
        d.Attempts = 1;

        (await _sut.ProcessDueAsync()).Claimed.Should().Be(0);   // still leased by the other worker

        _time.Advance(TimeSpan.FromMinutes(3));
        var summary = await _sut.ProcessDueAsync();

        summary.Sent.Should().Be(1);
        d.Status.Should().Be(DeliveryStatus.Sent);
    }

    [Fact]
    public async Task AChannelThatIsNotConfigured_FailsPermanently()
    {
        var d = AddDelivery(NotificationChannel.Sms);   // only e-mail is registered in this test

        var summary = await _sut.ProcessDueAsync();

        summary.Failed.Should().Be(1);
        d.Status.Should().Be(DeliveryStatus.Failed);
        d.LastError.Should().Contain("Sms");
    }

    [Fact]
    public async Task OneBrokenMessage_DoesNotStopTheOthers()
    {
        var bad = AddDelivery();
        var good = AddDelivery();
        _email.Behaviour = m => m.DeliveryId == bad.Id
            ? new ChannelSendResult(false, "smtp", "bounce")
            : new ChannelSendResult(true, "smtp");

        var summary = await _sut.ProcessDueAsync();

        summary.Should().Be(new DispatchSummary(2, 1, 1, 0));
        good.Status.Should().Be(DeliveryStatus.Sent);
        bad.Status.Should().Be(DeliveryStatus.Pending);
    }
}
