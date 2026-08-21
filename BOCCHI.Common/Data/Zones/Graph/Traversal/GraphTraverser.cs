using BOCCHI.Common.Data.Paths;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public record TraversalCandidate(float TotalCost, List<PathStep> Steps);

public class GraphTraverser(ZoneGraph graph, IPathfinder pathfinder, ILogger logger)
{
    private readonly List<IGraphCandidateCalculator> calculators = [];

    public void AddCalculator(IGraphCandidateCalculator calculator)
    {
        calculators.Add(calculator);
    }

    public async Task<List<PathStep>> FindPath(Vector3 start, Node goal)
    {
        return await FindPath(start, goal, 1, Random.Shared);
    }

    public async Task<List<PathStep>> FindPath(Vector3 start, Node goal, int topK, Random? rng = null)
    {
        List<TraversalCandidate> candidates = new();

        // Calculators with a cheap lower bound run last, and are skipped when they cannot beat
        // a candidate already found.
        List<(IGraphCandidateCalculator Calculator, float Bound)> deferred = [];

        foreach(IGraphCandidateCalculator calculator in calculators)
        {
            if (calculator.LowerBoundCost(graph, start, goal) is { } bound)
            {
                deferred.Add((calculator, bound));
                continue;
            }

            await Evaluate(calculator, start, goal, candidates);
        }

        foreach((IGraphCandidateCalculator calculator, float bound) in deferred)
        {
            float best = candidates.Count > 0
                ? candidates.Min(c => c.TotalCost)
                : float.PositiveInfinity;

            if (bound >= best)
            {
                logger.Debug(
                    $"Skipping Calculator: {calculator.Key()} (lower bound {bound:F0} >= best {best:F0})");
                continue;
            }

            await Evaluate(calculator, start, goal, candidates);
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        // Sort by cost ascending
        candidates.Sort((a, b) => a.TotalCost.CompareTo(b.TotalCost));

        // If topK <= 1 or only one candidate, use deterministic min
        if (topK <= 1 || candidates.Count == 1)
        {
            return candidates[0].Steps;
        }

        // Pick randomly from top K candidates (clamped to available)
        int k = Math.Min(topK, candidates.Count);
        var chosen = candidates[rng!.Next(k)];
        logger.Info($"PathDiversity: picked candidate {candidates.IndexOf(chosen) + 1}/{k} (cost={chosen.TotalCost:F2})");
        return chosen.Steps;
    }

    private async Task Evaluate(
        IGraphCandidateCalculator calculator,
        Vector3 start,
        Node goal,
        List<TraversalCandidate> candidates)
    {
        logger.Debug($"Running Calculator: {calculator.Key()}");
        TraversalCandidate? candidate = await calculator.CalculateAsync(graph, start, goal, pathfinder);
        if (candidate != null)
        {
            logger.Debug($"  Cost:  {candidate.TotalCost}");
            candidates.Add(candidate);
        }
        else
        {
            logger.Debug("  Cost: N/A");
        }
    }
}
