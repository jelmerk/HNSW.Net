// <copyright file="SmallDistanceCache.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;

    /// <summary>
    /// Cache for distance between 2 points.
    /// Current implementation support cache for up to 65535 points.
    /// </summary>
    /// <typeparam name="TDistance">The type of the distance</typeparam>
    internal class SmallDistanceCache<TDistance>
        where TDistance : struct
    {
        /// <summary>
        /// The cache chunks.
        /// </summary>
        private TDistance[] cache;

        /// <summary>
        /// The presence bits.
        /// </summary>
        private int[] presence;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallDistanceCache{TDistance}"/> class.
        /// </summary>
        /// <param name="pointsCount">
        /// The number of points to allocate cache for.
        /// Maximum supported value is 65535.
        /// </param>
        internal SmallDistanceCache(int pointsCount)
        {
            try
            {
                int capacity = checked((int)(((uint)pointsCount * (uint)(pointsCount + 1)) >> 1));
                this.presence = new int[(capacity >> 5) + 1];
                this.cache = new TDistance[capacity];
            }
            catch (OverflowException e)
            {
                throw new NotSupportedException("Distance cache for large data sets is not supported", e);
            }
        }

        /// <summary>
        /// Tries to get value from the cache.
        /// </summary>
        /// <param name="fromId">The 'from' point identifier.</param>
        /// <param name="toId">The 'to' point identifier.</param>
        /// <param name="distance">The buffer for the result.</param>
        /// <returns>True if the distance value is retrieved from the cache.</returns>
        internal bool TryGetValue(int fromId, int toId, out TDistance distance)
        {
            int key = MakeKey(fromId, toId);
            bool result = (this.presence[key >> 5] & (1 << (key & 31))) != 0;
            distance = result ? this.cache[key] : default;
            return result;
        }

        /// <summary>
        /// Caches the distance value.
        /// </summary>
        /// <param name="fromId">The 'from' point identifier.</param>
        /// <param name="toId">The 'to' point identifier.</param>
        /// <param name="distance">The distance value to cache.</param>
        internal void SetValue(int fromId, int toId, TDistance distance)
        {
            int key = MakeKey(fromId, toId);
            int mask = 1 << (key & 31);

            this.presence[key >> 5] |= mask;
            this.cache[key] = distance;
        }

        private static int MakeKey(int fromId, int toId)
        {
            return fromId > toId
                ? (int)(((uint)fromId * (uint)(fromId + 1)) >> 1) + toId
                : (int)(((uint)toId * (uint)(toId + 1)) >> 1) + fromId;
        }
    }
}
