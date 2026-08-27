// Created on Aug 27, 2026 @ 14:35:45 -> ITokenService and TokenService managing shield token awards and loss triggers
using System;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface ITokenService
{
    event EventHandler<KnockoutPlayer>? TokenAwarded;
    event EventHandler<KnockoutPlayer>? TokenLost;
    event EventHandler<KnockoutPlayer>? TokenFlyOffRequested;

    bool AwardToken(KnockoutPlayer player, int maxTokens = 3);
    bool DeductToken(KnockoutPlayer player);
}

public class TokenService : ITokenService
{
    public event EventHandler<KnockoutPlayer>? TokenAwarded;
    public event EventHandler<KnockoutPlayer>? TokenLost;
    public event EventHandler<KnockoutPlayer>? TokenFlyOffRequested;

    public bool AwardToken(KnockoutPlayer player, int maxTokens = 3)
    {
        if (player.Tokens < maxTokens)
        {
            player.Tokens++;
            TokenAwarded?.Invoke(this, player);
            return true;
        }
        return false;
    }

    public bool DeductToken(KnockoutPlayer player)
    {
        if (player.Tokens > 0)
        {
            player.Tokens--;
            TokenLost?.Invoke(this, player);
            TokenFlyOffRequested?.Invoke(this, player);
            return true;
        }
        return false;
    }
}
