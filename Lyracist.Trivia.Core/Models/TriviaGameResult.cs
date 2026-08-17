// Created on Aug 17, 2026 @ 14:20:30 -> TriviaGameResult model for team & individual winner announcements
using System;
using System.Collections.Generic;

namespace Lyracist.Trivia.Core.Models;

public class TriviaTeamSummary
{
    public string TeamName { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public int PlayerCount => Players.Count;
    public List<string> Players { get; set; } = [];
    public string PlayersRoster => string.Join(", ", Players);
}

public class TriviaGameResult
{
    public bool HasTeamWinner { get; set; }
    public string WinnerTitle { get; set; } = string.Empty;
    public string WinningName { get; set; } = string.Empty;
    public int WinningScore { get; set; }
    public List<string> WinningTeamMembers { get; set; } = [];
    public string WinningTeamMembersRoster => string.Join(", ", WinningTeamMembers);
    public List<TriviaTeamSummary> Teams { get; set; } = [];
    public List<TriviaPlayer> RankedPlayers { get; set; } = [];
}
