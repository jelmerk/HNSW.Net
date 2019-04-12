// <copyright file="SpanUtils.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;

    /// <summary>
    /// Extensions for <see cref="Span{T}"/>.
    /// </summary>
    internal static class SpanUtils
    {
        /// <summary>
        /// Check a single bit of an integer <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="buffer">The span.</param>
        /// <param name="index">The index of the bit to check.</param>
        /// <returns>True if the bit is set.</returns>
        internal static bool CheckBit(this Span<int> buffer, int index)
        {
            int carrier = buffer[index >> 5];
            return ((1 << (index & 31)) & carrier) != 0;
        }

        /// <summary>
        /// Set a single bit of an integer <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="buffer">The span.</param>
        /// <param name="index">The index of the bit to set.</param>
        /// <param name="value">The value of the bit.</param>
        internal static void SetBit(this Span<int> buffer, int index, bool value)
        {
            if (value)
            {
                int mask = 1 << (index & 31);
                buffer[index >> 5] |= mask;
            }
            else
            {
                int mask = ~(1 << (index & 31));
                buffer[index >> 5] &= mask;
            }
        }
    }
}
