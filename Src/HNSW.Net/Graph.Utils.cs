// <copyright file="Graph.Utils.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <content>
    /// The part with the auxiliary graph tools.
    /// </content>
    internal partial class Graph<TItem, TDistance>
    {
        /// <summary>
        /// Runs breadth first search.
        /// </summary>
        /// <param name="core">The graph core.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="layer">The layer of the graph where to run BFS.</param>
        /// <param name="visitAction">The action to perform on each node.</param>
        internal static void BFS(Core core, Node entryPoint, int layer, Action<Node> visitAction)
        {
            var visitedIds = new HashSet<int>();
            var expansionQueue = new Queue<int>(new[] { entryPoint.Id });

            while (expansionQueue.Any())
            {
                var currentNode = core.Nodes[expansionQueue.Dequeue()];
                if (!visitedIds.Contains(currentNode.Id))
                {
                    visitAction(currentNode);
                    visitedIds.Add(currentNode.Id);
                    foreach (var neighbourId in currentNode[layer])
                    {
                        expansionQueue.Enqueue(neighbourId);
                    }
                }
            }
        }

        /// <summary>
        /// Special struct for distance cache, segments [a,b] and [b,a] are equal.
        /// </summary>
        internal struct Segment : IEquatable<Segment>
        {
            /// <summary>
            /// Identifier of the point A
            /// </summary>
            internal int A;

            /// <summary>
            /// Identifier of the point B
            /// </summary>
            internal int B;

            /// <summary>
            /// Initializes a new instance of the <see cref="Segment"/> struct.
            /// </summary>
            /// <param name="a">Identifier of the point <see cref="A"/></param>
            /// <param name="b">Identifier of the point <see cref="B"/></param>
            internal Segment(int a, int b)
            {
                this.A = a;
                this.B = b;
            }

            /// <inheritdoc/>
            public bool Equals(Segment other)
            {
                if (this.A == other.A && this.B == other.B)
                {
                    // if items are the same
                    // this is [a; b] and other is [a; b]
                    return true;
                }
                else if (this.A == other.B && this.B == other.A)
                {
                    // if items are symmetric
                    // this is [a; b] and other is [b; a]
                    return true;
                }

                return false;
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return obj is Segment && this.Equals((Segment)obj);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                unchecked
                {
                    // commutative hash code hash([a; b]) == hash([b; a])
                    return (int)(((uint)this.A + 1) * ((uint)this.B + 1));
                }
            }
        }
    }
}