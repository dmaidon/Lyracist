// Edited on Aug 29, 2026 @ 10:33:00 -> Fixed Super Streak threshold milestone check without resetting StreakCount on token awards
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

            // Check Super Streak milestone (e.g. 20 consecutive)
            if (player.StreakCount == superStreakThreshold)
            {
                SuperStreakReached?.Invoke(this, player);
            }

            // Check standard streak token reward on each multiple of streakRequirement (e.g. 5, 10, 15, 20...)
            if (streakRequirement > 0 && player.StreakCount >= streakRequirement && player.StreakCount % streakRequirement == 0)
            {
                _tokenService.AwardToken(player, maxTokens);
                TokenRewardEarned?.Invoke(this, player);
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
