using System.Collections.Generic;
using System.Linq;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

public class TriviaPackManagerTests
{
    private static TriviaQuestionPack CreatePack(string id, int questionCount)
    {
        var questions = new List<TriviaQuestion>();
        for (int i = 0; i < questionCount; i++)
        {
            questions.Add(new TriviaQuestion
            {
                Id = $"{id}-{i}",
                Prompt = $"Question {i}",
                Options = ["A", "B", "C", "D"],
                CorrectAnswerIndex = 0
            });
        }
        return new TriviaQuestionPack { PackId = id, Title = id, Questions = questions };
    }

    [Fact]
    public void BuildMultiGameQuestionSets_ReturnsOneSetPerGame_SizedToQuestionsPerGame()
    {
        var pack = CreatePack("Pack1", 100);

        var sets = TriviaPackManager.BuildMultiGameQuestionSets([pack], questionsPerGame: 20, gameCount: 3);

        Assert.Equal(3, sets.Count);
        Assert.All(sets, s => Assert.Equal(20, s.Count));
    }

    [Fact]
    public void BuildMultiGameQuestionSets_DoesNotRepeatQuestionsAcrossGames_WhenPoolIsLargeEnough()
    {
        var pack = CreatePack("Pack1", 100); // Plenty for 3 games of 20 (60 total) with no repeats

        var sets = TriviaPackManager.BuildMultiGameQuestionSets([pack], questionsPerGame: 20, gameCount: 3);

        var allIds = sets.SelectMany(s => s.Select(q => q.Id)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
    }

    [Fact]
    public void BuildMultiGameQuestionSets_WrapsAroundWithRepeats_WhenPoolIsSmallerThanTotalNeeded()
    {
        var pack = CreatePack("Pack1", 10); // Only 10 questions, but 3 games of 20 need 60

        var sets = TriviaPackManager.BuildMultiGameQuestionSets([pack], questionsPerGame: 20, gameCount: 3);

        // Still fills every game to the requested size by reshuffling and wrapping around the
        // pool, even though that necessarily means some questions repeat.
        Assert.Equal(3, sets.Count);
        Assert.All(sets, s => Assert.Equal(20, s.Count));
        var allIds = sets.SelectMany(s => s.Select(q => q.Id)).ToList();
        Assert.True(allIds.Distinct().Count() < allIds.Count);
    }

    [Fact]
    public void BuildMultiGameQuestionSets_ReturnsEmpty_WhenGameCountIsZero()
    {
        var pack = CreatePack("Pack1", 20);

        var sets = TriviaPackManager.BuildMultiGameQuestionSets([pack], questionsPerGame: 10, gameCount: 0);

        Assert.Empty(sets);
    }
}
