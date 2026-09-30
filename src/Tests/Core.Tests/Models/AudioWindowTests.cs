using Core.Models;

namespace Core.Tests.Models;

public class AudioWindowTests
{
    private static readonly TimeSpan ThreeMinutes = TimeSpan.FromMinutes(3);

    [Fact]
    public void LongTrack_TakesTheMiddle()
    {
        var window = AudioWindow.Centered(TimeSpan.FromMinutes(5), ThreeMinutes);

        Assert.Equal(TimeSpan.FromMinutes(1), window.Start);
        Assert.Equal(ThreeMinutes, window.Length);
    }

    [Fact]
    public void TrackShorterThanTheCap_StartsAtZero()
    {
        var window = AudioWindow.Centered(TimeSpan.FromMinutes(2), ThreeMinutes);

        Assert.Equal(TimeSpan.Zero, window.Start);
    }

    [Fact]
    public void UnknownDuration_StartsAtZero()
    {
        var window = AudioWindow.Centered(trackDuration: null, ThreeMinutes);

        Assert.Equal(new AudioWindow(TimeSpan.Zero, ThreeMinutes), window);
    }

    [Fact]
    public void NonPositiveCap_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioWindow.Centered(ThreeMinutes, TimeSpan.Zero));
    }
}
