// <copyright file="Graph.Core.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;
    using System.Collections.Generic;

    /// <content>
    /// The implemnation of graph core structure.
    /// </content>
    internal partial class Graph<TItem, TDistance>
    {
        /// <summary>
        /// The graph core.
        /// </summary>
        internal class Core
        {
            /// <summary>
            /// The original distance function.
            /// </summary>
            private readonly Func<TItem, TItem, TDistance> distance;

            /// <summary>
            /// The distance cache.
            /// </summary>
            private readonly IDictionary<Segment, TDistance> distanceCache;

            /// <summary>
            /// Initializes a new instance of the <see cref="Core"/> class.
            /// </summary>
            /// <param name="items">The original items.</param>
            /// <param name="distance">The distance function in the items space.</param>
            /// <param name="parameters">The parameters of the world.</param>
            /// <param name="generator">The random number generator to use in <see cref="RandomLayer"/></param>
            internal Core(IReadOnlyList<TItem> items, Func<TItem, TItem, TDistance> distance, SmallWorld<TItem, TDistance>.Parameters parameters, Random generator)
            {
                this.distance = distance;
                this.Parameters = parameters;
                this.Items = items;
                switch (parameters.NeighbourHeuristic)
                {
                    case SmallWorld<TItem, TDistance>.NeighbourSelectionHeuristic.SelectSimple:
                        this.Algorithm = new Node.Algorithm3<TItem, TDistance>(this);
                        break;
                    case SmallWorld<TItem, TDistance>.NeighbourSelectionHeuristic.SelectHeuristic:
                        this.Algorithm = new Node.Algorithm4<TItem, TDistance>(this);
                        break;
                }

                if (this.Parameters.EnableDistanceCacheForConstruction)
                {
                    var capacity = items.Count * items.Count;
                    this.distanceCache = new Dictionary<Segment, TDistance>(capacity);
                }

                var nodes = new FixedSizeList<Node>(items.Count);
                for (int id = 0; id < items.Count; ++id)
                {
                    nodes.Add(this.Algorithm.NewNode(id, RandomLayer(generator, parameters.LevelLambda)));
                }

                this.Nodes = nodes;
            }

            /// <summary>
            /// Gets the graph nodes corresponding to <see cref="Items"/>
            /// </summary>
            public IReadOnlyList<Node> Nodes { get; private set; }

            /// <summary>
            /// Gets the items associated with the <see cref="Nodes"/>
            /// </summary>
            public IReadOnlyList<TItem> Items { get; private set; }

            /// <summary>
            /// Gets the algorithm for allocating and managing nodes capacity.
            /// </summary>
            public Node.Algorithm<TItem, TDistance> Algorithm { get; private set; }

            /// <summary>
            /// Gets parameters of the small world.
            /// </summary>
            public SmallWorld<TItem, TDistance>.Parameters Parameters { get; private set; }

            /// <summary>
            /// Gets the distance between 2 items.
            /// </summary>
            /// <param name="fromId">The identifier of the "from" item.</param>
            /// <param name="toId">The identifier of the "to" item.</param>
            /// <returns>The distance beetween items.</returns>
            public TDistance GetDistance(int fromId, int toId)
            {
                if (this.distanceCache != null)
                {
                    var key = new Segment(fromId, toId);
                    if (this.distanceCache.TryGetValue(key, out TDistance result))
                    {
                        return result;
                    }

                    result = this.distance(this.Items[fromId], this.Items[toId]);
                    this.distanceCache[key] = result;
                    return result;
                }

                return this.distance(this.Items[fromId], this.Items[toId]);
            }

            /// <summary>
            /// Gets the random layer.
            /// </summary>
            /// <param name="generator">The random numbers generator.</param>
            /// <param name="lambda">Poisson lambda.</param>
            /// <returns>The layer value.</returns>
            private static int RandomLayer(Random generator, double lambda)
            {
                var r = -Math.Log(generator.NextDouble()) * lambda;
                return (int)r;
            }
        }
    }
}
