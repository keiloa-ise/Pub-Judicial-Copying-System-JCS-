using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using ResourceIQ.Jcs.Domain.Workflow;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

public class CopyStateMachineTests
{
    [Theory]
    [InlineData(CopyState.Created, CopyState.InPreparation)]
    [InlineData(CopyState.InPreparation, CopyState.UnderReview)]
    [InlineData(CopyState.UnderReview, CopyState.Approved)]
    [InlineData(CopyState.UnderReview, CopyState.InPreparation)]
    [InlineData(CopyState.Approved, CopyState.Unlocked)]
    [InlineData(CopyState.Unlocked, CopyState.UnderReview)] // re-submit after re-edit (decision #3)
    public void Allowed_transitions_pass(CopyState from, CopyState to) =>
        Assert.True(CopyStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(CopyState.Created, CopyState.Approved)]
    [InlineData(CopyState.InPreparation, CopyState.Approved)]
    [InlineData(CopyState.Approved, CopyState.InPreparation)] // approved is read-only (BR-04)
    [InlineData(CopyState.Approved, CopyState.UnderReview)]
    [InlineData(CopyState.Unlocked, CopyState.InPreparation)] // unlocked only re-submits → UnderReview
    [InlineData(CopyState.Unlocked, CopyState.Approved)]      // never approved directly from Unlocked
    public void Illegal_transitions_are_rejected(CopyState from, CopyState to)
    {
        Assert.False(CopyStateMachine.CanTransition(from, to));
        Assert.Throws<DomainException>(() => CopyStateMachine.EnsureTransition(from, to));
    }

    [Fact]
    public void Unlocked_only_allows_resubmission_to_review()
    {
        Assert.True(CopyStateMachine.CanTransition(CopyState.Unlocked, CopyState.UnderReview));
        foreach (CopyState target in Enum.GetValues<CopyState>())
            if (target != CopyState.UnderReview)
                Assert.False(CopyStateMachine.CanTransition(CopyState.Unlocked, target));
    }
}
