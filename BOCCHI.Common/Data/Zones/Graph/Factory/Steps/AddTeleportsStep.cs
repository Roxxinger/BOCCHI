using BOCCHI.Common.Data.Aethernet;
namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddTeleportsStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        Node start = new()
        {
            Type = NodeType.BaseCampReturnPosition,
            Position = zone.GetStartingPosition(),
            Metadata = new TeleportNodeMetadata()
        };

        AethernetData mainAetheryte = zone.GetMainAetheryte();
        Node basecamp = new()
        {
            Type = NodeType.BaseCampAetheryte,
            Position = zone.GetAetherytePosition(),
            Metadata = new TeleportNodeMetadata
            {
                AetheryteId = mainAetheryte.Id,
                Destination = mainAetheryte.GetInteractPosition(),
                DeadRadius = mainAetheryte.DeadRadius,
            }
        };

        List<Node> aethernet = new();
        foreach(AethernetData shard in zone.GetAethernetShards())
        {
            aethernet.Add(new()
            {
                Type = NodeType.AethernetShard,
                Position = shard.Position,
                Metadata = new TeleportNodeMetadata
                {
                    AetheryteId = shard.Id,
                    Destination = shard.Destination,
                    DeadRadius = shard.DeadRadius,
                }
            });
        }

        graph.AddNode(start);
        graph.AddNode(basecamp);
        foreach(Node shard in aethernet)
        {
            graph.AddNode(shard);
        }


        graph.AddEdge(start.Id, basecamp.Id, await config.GetWalkingCost(start.Position, mainAetheryte.Destination), EdgeType.Walk);

        foreach(Node shard in aethernet)
        {
            graph.AddTwoWayEdge(basecamp.Id, shard.Id, config.TeleportCost, EdgeType.Teleport);

            foreach(Node other in aethernet)
            {
                if (other.Id == shard.Id)
                {
                    continue;
                }

                graph.AddEdge(shard.Id, other.Id, config.TeleportCost, EdgeType.Teleport);
            }
        }
    }
}
