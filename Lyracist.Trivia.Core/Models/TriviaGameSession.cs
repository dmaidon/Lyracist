// Created on Aug 17, 2026 @ 13:01:00 -> TriviaGameSession state model
using System;
using System.Collections.Generic;

namespace Lyracist.Trivia.Core.Models;

public class TriviaGameSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string Title { get; set; } = "Pub Trivia Show";
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public TriviaGameState State { get; set; } = TriviaGameState.Lobby;
    public List<TriviaRound> Rounds { get; set; } = [];
    public int CurrentRoundIndex { get; set; } = 0;
    public int CurrentQuestionIndex { get; set; } = 0;

    public TriviaRound? CurrentRound => (Rounds.Count > CurrentRoundIndex && CurrentRoundIndex >= 0)
        ? Rounds[CurrentRoundIndex]
        : null;

    public TriviaQuestion? CurrentQuestion => (CurrentRound != null && CurrentRound.Questions.Count > CurrentQuestionIndex && CurrentQuestionIndex >= 0)
        ? CurrentRound.Questions[CurrentQuestionIndex]
        : null;
}
