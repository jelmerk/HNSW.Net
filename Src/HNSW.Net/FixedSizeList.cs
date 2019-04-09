// <copyright file="FixedSizeList.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Wrapper around array to be unblock adding removing items api
    /// </summary>
    /// <typeparam name="T">The type of items in the array.</typeparam>
    [SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "By design")]
    public class FixedSizeList<T> : IList<T>, IReadOnlyList<T>
    {
        private readonly T[] buffer;
        private int border;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedSizeList{T}"/> class.
        /// </summary>
        /// <param name="size">The size of the list.</param>
        public FixedSizeList(int size)
        {
            if (size < 0)
            {
                throw new ArgumentException($"The {nameof(size)} must be non negative integer", nameof(size));
            }

            this.buffer = new T[size];
            this.border = -1;
        }

        /// <inheritdoc/>
        public int Count => this.border + 1;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index > this.border)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return this.buffer[index];
            }

            set
            {
                if (index < 0 || index > this.border)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                this.buffer[index] = value;
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            if (this.Count >= this.buffer.Length)
            {
                throw new InvalidOperationException("Out of space in the list");
            }

            this.buffer[++this.border] = item;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            this.border = -1;
        }

        /// <inheritdoc/>
        public bool Contains(T item)
        {
            return this.IndexOf(item) > -1;
        }

        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0)
            {
                throw new ArgumentException($"The {nameof(arrayIndex)} must be non negative integer", nameof(arrayIndex));
            }

            if (this.Count > array.Length - arrayIndex)
            {
                throw new ArgumentException("Not enough space in the array");
            }

            Copy(this.buffer, 0, array, arrayIndex, this.Count);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <inheritdoc/>
        public int IndexOf(T item)
        {
            for (int i = 0; i < this.border; ++i)
            {
                if (EqualityComparer<T>.Default.Equals(this.buffer[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <inheritdoc/>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > this.border)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (this.Count >= this.buffer.Length)
            {
                throw new InvalidOperationException("Out of space in the list");
            }

            Copy(this.buffer, index, this.buffer, index + 1, this.border - index);
            this.buffer[index] = item;
            this.border++;
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            var i = this.IndexOf(item);
            if (i == -1)
            {
                return false;
            }

            this.RemoveAt(i);
            return true;
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            if (index < 0 || index > this.border)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Copy(this.buffer, index + 1, this.buffer, index, this.border - index);
            this.border--;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Equivalent of <see cref="Array.Copy(Array, int, Array, int, int)"/>
        /// Motivation for having this method https://stackoverflow.com/a/33865267
        /// </summary>
        /// <param name="source">The source array to copy from.</param>
        /// <param name="sourceOffset">The offset in the source array.</param>
        /// <param name="target">The targer array to copy to.</param>
        /// <param name="targetOffset">The offset in the target array.</param>
        /// <param name="count">The number of items to copy.</param>
        private static void Copy(T[] source, int sourceOffset, T[] target, int targetOffset, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                int s = sourceOffset + i;
                int t = targetOffset + i;
                target[t] = source[s];
            }
        }

        /// <summary>
        /// Enumerator for the <see cref="FixedSizeList{T}"/>
        /// </summary>
        private class Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly FixedSizeList<T> list;
            private int cursor;

            /// <summary>
            /// Initializes a new instance of the <see cref="Enumerator"/> class.
            /// </summary>
            /// <param name="list">The parent list.</param>
            public Enumerator(FixedSizeList<T> list)
            {
                this.cursor = -1;
                this.list = list;
            }

            /// <inheritdoc/>
            public T Current => this.list.buffer[this.cursor];

            /// <inheritdoc/>
            object IEnumerator.Current => this.list.buffer[this.cursor];

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                return ++this.cursor <= this.list.border;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                this.cursor = -1;
            }
        }
    }
}
