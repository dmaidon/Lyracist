// Created on Aug 27, 2026 @ 14:35:50 -> IStreakService and StreakService managing player streaks and Super Streak triggers
using System;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface IStreakService
{
    event EventHandler<KnockoutPlayer>? TokenRewardEarned;
    event EventHandler<KnockoutPlayer>? SuperStreakReached;

    void RecordAnswer(KnockoutPlayer player, bool isCorrect, int streakRequirement = 5, int superStreakThreshold = 20, int maxTokens = 3);
    void ResetStreak(KnockoutPlayer player);
}

public class StreakService : IStreakService
{
    private readonly ITokenService _tokenService;

    public event EventHandler<KnockoutPlayer>? TokenRewardEarned;
    public event EventHandler<KnockoutPlayer>? SuperStreakReached;

    public StreakService(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public void RecordAnswer(KnockoutPlayer player, bool isCorrect, int streakRequirement = 5, int superStreakThreshold = 20, int maxTokens = 3)
    {
        if (isCorrect)
        {
            player.StreakCount++;

            // Check standard streak token reward (e.g. every 5)
            if (player.StreakCount >= streakRequirement)
            {
                _tokenService.AwardToken(player, maxTokens);
                TokenRewardEarned?.Invoke(this, player);
                player.StreakCount = 0;
            }

            // Check Super Streak milestone (e.g. 20 consecutive)
            if (player.StreakCount == superStreakThreshold)
            {
                SuperStreakReached?.Invoke(this, player);
            }
        }
        else
        {
            ResetStreak(player);
        }
    }

    public void ResetStreak(KnockoutPlayer player)
    {
        player.StreakCount = 0;
    }
}
