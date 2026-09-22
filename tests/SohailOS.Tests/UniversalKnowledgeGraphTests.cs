using System.Text.Json.Nodes;
using Xunit;

namespace SohailOS.Tests;

public sealed class UniversalKnowledgeGraphTests
{
    [Fact]
    public void Builder_creates_repository_nodes_and_upstream_fork_edges()
    {
        var repositories = new[]
        {
            new SohailOS.Ecosystem.LiveRepository(1, "central", "kaido6sanb6/SohailOS-Central", "main", null, false, false),
            new SohailOS.Ecosystem.LiveRepository(2, "fork", "kaido6sanb6/fork", "main", null, false, true)
        };

        var inventory = new JsonArray(
            new JsonObject
            {
                ["full_name"] = "kaido6sanb6/SohailOS-Central",
                ["fork"] = false,
                ["upstream"] = null,
                ["license"] = "MIT"
            },
            new JsonObject
            {
                ["full_name"] = "kaido6sanb6/fork",
                ["fork"] = true,
                ["upstream"] = "upstream/project",
                ["license"] = "MIT"
            });

        var graph = SohailOS.Ecosystem.KnowledgeGraphBuilder.Build(repositories, inventory);

        Assert.Contains(graph.Nodes, x => x.Id == "repo:1" && x.Type == "Repository");
        Assert.Contains(graph.Edges, x =>
            x.From == "repo:2" &&
            x.To == "upstream/project" &&
            x.Type == "FORK_OF");
        Assert.Contains(graph.Edges, x =>
            x.From == "kaido6sanb6/fork" &&
            x.To == "kaido6sanb6/SohailOS-Central" &&
            x.Type == "KNOWLEDGE_SOURCE_FOR");
    }

    [Fact]
    public void Live_corpus_sources_are_explicit_untrusted_graph_nodes()
    {
        var repositories = new[]
        {
            new SohailOS.Ecosystem.LiveRepository(1, "central", "kaido6sanb6/SohailOS-Central", "main", null, false, false),
            new SohailOS.Ecosystem.LiveRepository(2, "leaks", "kaido6sanb6/system_prompts_leaks", "main", null, false, true)
        };

        var graph = SohailOS.Ecosystem.KnowledgeGraphBuilder.Build(repositories, new JsonArray());

        var node = Assert.Single(graph.Nodes, x => x.Id == "repo:2");
        Assert.Equal("untrusted-data", node.Trust);
        Assert.Contains(graph.Edges, x =>
            x.From == "kaido6sanb6/system_prompts_leaks" &&
            x.To == "kaido6sanb6/SohailOS-Central" &&
            x.Type == "RESEARCH_CORPUS_FOR");
    }
}
