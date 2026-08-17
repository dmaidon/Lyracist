// Edited on Aug 17, 2026 @ 13:36:30 -> Added EliminatingAnswers state for progressive incorrect option fading
namespace Lyracist.Trivia.Core.Models;

public enum TriviaGameState
{
    Lobby,
    Countdown,
    QuestionActive,
    AnsweringLocked,
    EliminatingAnswers,
    RevealAnswer,
    RoundLeaderboard,
    GameComplete
}

public enum TriviaQuestionType
{
    MultipleChoice,
    TrueFalse,
    FinishTheLyric,
    NameThatTune
}

public enum TriviaDifficulty
{
    Easy,
    Medium,
    Hard
}
