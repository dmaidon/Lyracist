// Created on Oct 7, 2026 @ 19:43:00 -> Unit tests for NextTrackSelector smart shuffle algorithm
using System;
using System.Collections.Generic;
using Lyracist.Shared;
using Xunit;

namespace Lyracist.Tests;

public class NextTrackSelectorTests
{
    [Fact]
    public void SelectNextIndex_EmptyCandidates_ReturnsMinusOne()
    {
        int index = NextTrackSelector.SelectNextIndex(null, []);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void SelectNextIndex_SingleCandidate_ReturnsZero()
    {
        var track = new FillInTrack("C:\\music\\song.mp3", "Artist", 120);
        int index = NextTrackSelector.SelectNextIndex(track, [track]);
        Assert.Equal(0, index);
    }

    [Fact]
    public void SelectNextIndex_AvoidsCurrentTrack_WhenMultipleCandidatesExist()
    {
        var current = new FillInTrack("C:\\music\\song1.mp3", "Artist A", 120);
        var other = new FillInTrack("C:\\music\\song2.mp3", "Artist B", 120);

        int index = NextTrackSelector.SelectNextIndex(current, [current, other]);
        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectNextIndex_AvoidsRecentTracks_WhenAlternativesAvailable()
    {
        var current = new FillInTrack("C:\\music\\song1.mp3", "Artist A", 120);
        var recent = new FillInTrack("C:\\music\\song2.mp3", "Artist B", 120);
        var fresh = new FillInTrack("C:\\music\\song3.mp3", "Artist C", 120);

        int index = NextTrackSelector.SelectNextIndex(
            current,
            [current, recent, fresh],
            recentPaths: [recent.Path]);

        Assert.Equal(2, index);
    }

    [Fact]
    public void SelectNextIndex_AvoidsSameArtistBackToBack_WhenPossible()
    {
        var current = new FillInTrack("C:\\music\\song1.mp3", "The Beatles", 120);
        var sameArtist = new FillInTrack("C:\\music\\song2.mp3", "The Beatles", 120);
        var diffArtist = new FillInTrack("C:\\music\\song3.mp3", "Queen", 120);

        int index = NextTrackSelector.SelectNextIndex(current, [current, sameArtist, diffArtist]);
        Assert.Equal(2, index);
    }

    [Fact]
    public void SelectNextIndex_PicksBpmCompatibleCandidate()
    {
        // Current tempo 120 BPM. Candidates: 80 BPM, 122 BPM (compatible), 180 BPM
        var current = new FillInTrack("C:\\music\\song1.mp3", "Artist 1", 120);
        var cand80 = new FillInTrack("C:\\music\\song2.mp3", "Artist 2", 80);
        var cand122 = new FillInTrack("C:\\music\\song3.mp3", "Artist 3", 122); // within 8% of 120
        var cand180 = new FillInTrack("C:\\music\\song4.mp3", "Artist 4", 180);

        int index = NextTrackSelector.SelectNextIndex(current, [current, cand80, cand122, cand180]);
        Assert.Equal(2, index);
    }

    [Fact]
    public void SelectNextIndex_PicksHalfTimeBpmCompatibleCandidate()
    {
        // Current tempo 140 BPM. Half time: 70 BPM. Candidate 71 BPM (within 8% of 70).
        var current = new FillInTrack("C:\\music\\song1.mp3", "Artist 1", 140);
        var cand1 = new FillInTrack("C:\\music\\song2.mp3", "Artist 2", 95);
        var candHalfTime = new FillInTrack("C:\\music\\song3.mp3", "Artist 3", 71);

        int index = NextTrackSelector.SelectNextIndex(current, [current, cand1, candHalfTime]);
        Assert.Equal(2, index);
    }

    [Fact]
    public void SelectNextIndex_PicksDoubleTimeBpmCompatibleCandidate()
    {
        // Current tempo 75 BPM. Double time: 150 BPM. Candidate 152 BPM (within 8% of 150).
        var current = new FillInTrack("C:\\music\\song1.mp3", "Artist 1", 75);
        var cand1 = new FillInTrack("C:\\music\\song2.mp3", "Artist 2", 110);
        var candDoubleTime = new FillInTrack("C:\\music\\song3.mp3", "Artist 3", 152);

        int index = NextTrackSelector.SelectNextIndex(current, [current, cand1, candDoubleTime]);
        Assert.Equal(2, index);
    }

    [Fact]
    public void IsBpmCompatible_ReturnsExpectedValues()
    {
        Assert.True(NextTrackSelector.IsBpmCompatible(125, 120, 0.08)); // (125-120)/120 = 4.16%
        Assert.False(NextTrackSelector.IsBpmCompatible(135, 120, 0.08)); // (135-120)/120 = 12.5%
        Assert.True(NextTrackSelector.IsBpmCompatible(60, 120, 0.08)); // Half tempo exact
        Assert.True(NextTrackSelector.IsBpmCompatible(240, 120, 0.08)); // Double tempo exact
    }
}
