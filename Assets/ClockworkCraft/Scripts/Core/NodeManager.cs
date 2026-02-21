using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Tracks all active ResourceNodes on the map.
    /// Handles depletion bookkeeping so MapGenerator and Workers don't need to.
    /// Tile takeover itself is triggered by the Worker that dealt the killing blow
    /// (Worker calls GridManager.MoveUnit from its own OnKilledNode handler).
    /// </summary>
    public class NodeManager : MonoBehaviour
    {
        public static NodeManager Instance { get; private set; }

        private Dictionary<Vector2Int, ResourceNode> nodes = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterNode(ResourceNode node)
        {
            nodes[new Vector2Int(node.GridX, node.GridY)] = node;
        }

        public ResourceNode GetNode(int x, int y)
            => nodes.TryGetValue(new Vector2Int(x, y), out var n) ? n : null;

        public void OnNodeDepleted(ResourceNode node)
        {
            nodes.Remove(new Vector2Int(node.GridX, node.GridY));
        }

        public int NodeCount => nodes.Count;
    }
}
