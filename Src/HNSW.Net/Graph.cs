// <copyright file="Graph.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;

    /// <summary>
    /// The implemnation of a hierarchical small world graph.
    /// </summary>
    /// <typeparam name="TItem">The type of items to connect into small world.</typeparam>
    /// <typeparam name="TDistance">The type of distance between items (expect any numeric type: float, double, decimal, int, ...).</typeparam>
    internal partial class Graph<TItem, TDistance>
        where TDistance : IComparable<TDistance>
    {
        /// <summary>
        /// The distance.
        /// </summary>
        private readonly Func<TItem, TItem, TDistance> distance;

        /// <summary>
        /// The core.
        /// </summary>
        private Core core;

        /// <summary>
        /// The entry point.
        /// </summary>
        private Node entryPoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="Graph{TItem, TDistance}"/> class.
        /// </summary>
        /// <param name="distance">The distance function.</param>
        /// <param name="parameters">The parameters of the world.</param>
        internal Graph(Func<TItem, TItem, TDistance> distance, SmallWorld<TItem, TDistance>.Parameters parameters)
        {
            this.distance = distance;
            this.Parameters = parameters;
        }

        /// <summary>
        /// Gets the parameters.
        /// </summary>
        internal SmallWorld<TItem, TDistance>.Parameters Parameters { get; }

        /// <summary>
        /// Creates graph from the given items.
        /// Contains implementation of INSERT(hnsw, q, M, Mmax, efConstruction, mL) algorithm.
        /// Article: Section 4. Algorithm 1.
        /// </summary>
        /// <param name="items">The items to insert.</param>
        /// <param name="generator">The random number generator to distribte nodes acrsoss layers.</param>
        internal void Build(IReadOnlyList<TItem> items, Random generator)
        {
            if (!items?.Any() ?? false)
            {
                return;
            }

            var core = new Core(items, this.distance, this.Parameters, generator);
            var entryPoint = core.Nodes[0];

            for (int nodeId = 1; nodeId < core.Nodes.Count; ++nodeId)
            {
                /*
                 * W ← ∅ // list for the currently found nearest elements
                 * ep ← get enter point for hnsw
                 * L ← level of ep // top layer for hnsw
                 * l ← ⌊-ln(unif(0..1))∙mL⌋ // new element’s level
                 * for lc ← L … l+1
                 *   W ← SEARCH-LAYER(q, ep, ef=1, lc)
                 *   ep ← get the nearest element from W to q
                 * for lc ← min(L, l) … 0
                 *   W ← SEARCH-LAYER(q, ep, efConstruction, lc)
                 *   neighbors ← SELECT-NEIGHBORS(q, W, M, lc) // alg. 3 or alg. 4
                 *     for each e ∈ neighbors // shrink connections if needed
                 *       eConn ← neighbourhood(e) at layer lc
                 *       if │eConn│ > Mmax // shrink connections of e if lc = 0 then Mmax = Mmax0
                 *         eNewConn ← SELECT-NEIGHBORS(e, eConn, Mmax, lc) // alg. 3 or alg. 4
                 *         set neighbourhood(e) at layer lc to eNewConn
                 *   ep ← W
                 * if l > L
                 *   set enter point for hnsw to q
                 */

                // zoom in and find the best peer on the same level as newNode
                var bestPeer = entryPoint;
                var currentNode = core.Nodes[nodeId];
                var currentNodeTravelingCosts = new TravelingCosts<int, TDistance>(core.GetDistance, nodeId);
                for (int layer = bestPeer.MaxLayer; layer > currentNode.MaxLayer; --layer)
                {
                    var bestPeerId = KNearestAtLevel(core, bestPeer, currentNodeTravelingCosts, 1, layer).Single();
                    bestPeer = core.Nodes[bestPeerId];
                }

                // connecting new node to the small world
                for (int layer = Math.Min(currentNode.MaxLayer, entryPoint.MaxLayer); layer >= 0; --layer)
                {
                    var potentialNeighboursIds = KNearestAtLevel(core, bestPeer, currentNodeTravelingCosts, this.Parameters.ConstructionPruning, layer);
                    var bestNeighboursIds = core.Algorithm.SelectBestForConnecting(potentialNeighboursIds, currentNodeTravelingCosts, layer);

                    foreach (var newNeighbourId in bestNeighboursIds)
                    {
                        core.Algorithm.Connect(currentNode, core.Nodes[newNeighbourId], layer);
                        core.Algorithm.Connect(core.Nodes[newNeighbourId], currentNode, layer);

                        // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
                        if (DistanceUtils.Lt(currentNodeTravelingCosts.From(newNeighbourId), currentNodeTravelingCosts.From(bestPeer.Id)))
                        {
                            bestPeer = core.Nodes[newNeighbourId];
                        }
                    }
                }

                // zoom out to the highest level
                if (currentNode.MaxLayer > entryPoint.MaxLayer)
                {
                    entryPoint = currentNode;
                }
            }

            // construction is done
            this.core = core;
            this.entryPoint = entryPoint;
        }

        /// <summary>
        /// Get k nearest items for a given one.
        /// Contains implementation of K-NN-SEARCH(hnsw, q, K, ef) algorithm.
        /// Article: Section 4. Algorithm 5.
        /// </summary>
        /// <param name="destination">The given node to get the nearest neighbourhood for.</param>
        /// <param name="k">The size of the neighbourhood.</param>
        /// <returns>The list of the nearest neighbours.</returns>
        internal IList<SmallWorld<TItem, TDistance>.KNNSearchResult> KNearest(TItem destination, int k)
        {
            // TODO: hack we know that destination id is -1.
            TDistance RuntimeDistance(int x, int y)
            {
                int nodeId = x >= 0 ? x : y;
                return this.distance(destination, this.core.Items[nodeId]);
            }

            var bestPeer = this.entryPoint;
            var destiantionTravelingCosts = new TravelingCosts<int, TDistance>(RuntimeDistance, -1);
            for (int level = this.entryPoint.MaxLayer; level > 0; --level)
            {
                var bestPeerId = KNearestAtLevel(this.core, bestPeer, destiantionTravelingCosts, 1, level).Single();
                bestPeer = this.core.Nodes[bestPeerId];
            }

            return KNearestAtLevel(this.core, bestPeer, destiantionTravelingCosts, k, 0)
                .Select(id => new SmallWorld<TItem, TDistance>.KNNSearchResult
                {
                    Id = id,
                    Item = this.core.Items[id],
                    Distance = RuntimeDistance(id, -1)
                })
                .ToList();
        }

        /// <summary>
        /// Serializes edges of the graph.
        /// </summary>
        /// <returns>Bytes representing edges.</returns>
        internal byte[] Serialize()
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, this.entryPoint.Id);
                formatter.Serialize(stream, this.entryPoint.MaxLayer);

                for (int layer = this.entryPoint.MaxLayer; layer >= 0; --layer)
                {
                    BFS(this.core, this.entryPoint, layer, (node) =>
                    {
                        formatter.Serialize(stream, node);
                    });
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserilaizes graph edges and assigns nodes to the items.
        /// </summary>
        /// <param name="items">The underlying items.</param>
        /// <param name="bytes">The serialized edges.</param>
        internal void Deserialize(IReadOnlyList<TItem> items, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var formatter = new BinaryFormatter();
                int entryId = (int)formatter.Deserialize(stream);
                int maxLayer = (int)formatter.Deserialize(stream);

                for (int level = maxLayer; level >= items.Count; --level)
                {
                    var edges = (Dictionary<int, List<int>>)formatter.Deserialize(stream);
                }
            }
        }

        /// <summary>
        /// Prints edges of the graph.
        /// </summary>
        /// <returns>String representation of the graph's edges.</returns>
        internal string Print()
        {
            var buffer = new StringBuilder();
            for (int layer = this.entryPoint.MaxLayer; layer >= 0; --layer)
            {
                buffer.AppendLine($"[LEVEL {layer}]");
                BFS(this.core, this.entryPoint, layer, (node) =>
                {
                    var neighbours = string.Join(", ", node[layer]);
                    buffer.AppendLine($"({node.Id}) -> {{{neighbours}}}");
                });

                buffer.AppendLine();
            }

            return buffer.ToString();
        }

        /// <summary>
        /// The implementaiton of SEARCH-LAYER(q, ep, ef, lc) algorithm.
        /// Article: Section 4. Algorithm 2.
        /// </summary>
        /// <param name="core">The core of the graph.</param>
        /// <param name="entryPoint">The entry point for the search.</param>
        /// <param name="destinationTravelingCosts">The traveling costs for the search target.</param>
        /// <param name="k">The number of the nearest neighbours to get from the layer.</param>
        /// <param name="layer">The layer to perform search at.</param>
        /// <returns>The list of identifiers of the nearest neighbours at the level.</returns>
        private static IList<int> KNearestAtLevel(Core core, Node entryPoint, TravelingCosts<int, TDistance> destinationTravelingCosts, int k, int layer)
        {
            /*
             * v ← ep // set of visited elements
             * C ← ep // set of candidates
             * W ← ep // dynamic list of found nearest neighbors
             * while │C│ > 0
             *   c ← extract nearest element from C to q
             *   f ← get furthest element from W to q
             *   if distance(c, q) > distance(f, q)
             *     break // all elements in W are evaluated
             *   for each e ∈ neighbourhood(c) at layer lc // update C and W
             *     if e ∉ v
             *       v ← v ⋃ e
             *       f ← get furthest element from W to q
             *       if distance(e, q) < distance(f, q) or │W│ < ef
             *         C ← C ⋃ e
             *         W ← W ⋃ e
             *         if │W│ > ef
             *           remove furthest element from W to q
             * return W
             */

            // prepare tools
            IComparer<int> fartherIsOnTop = destinationTravelingCosts;
            IComparer<int> closerIsOnTop = fartherIsOnTop.Reverse();

            // prepare heaps
            var resultHeap = new BinaryHeap<int>(new List<int>(k + 1) { entryPoint.Id }, fartherIsOnTop);
            var expansionHeap = new BinaryHeap<int>(new List<int>() { entryPoint.Id }, closerIsOnTop);

            // run bfs
            var visited = new HashSet<int>() { entryPoint.Id };
            while (expansionHeap.Buffer.Any())
            {
                // get next candidate to check and expand
                var toExpandId = expansionHeap.Pop();
                var farthestResultId = resultHeap.Buffer.First();
                if (DistanceUtils.Gt(destinationTravelingCosts.From(toExpandId), destinationTravelingCosts.From(farthestResultId)))
                {
                    // the closest candidate is farther than farthest result
                    break;
                }

                // expand candidate
                foreach (var neighbourId in core.Nodes[toExpandId][layer])
                {
                    if (!visited.Contains(neighbourId))
                    {
                        // enque perspective neighbours to expansion list
                        farthestResultId = resultHeap.Buffer.First();
                        if (resultHeap.Buffer.Count < k
                        || DistanceUtils.Lt(destinationTravelingCosts.From(neighbourId), destinationTravelingCosts.From(farthestResultId)))
                        {
                            expansionHeap.Push(neighbourId);
                            resultHeap.Push(neighbourId);
                            if (resultHeap.Buffer.Count > k)
                            {
                                resultHeap.Pop();
                            }
                        }

                        // update visited list
                        visited.Add(neighbourId);
                    }
                }
            }

            return resultHeap.Buffer;
        }
    }
}
