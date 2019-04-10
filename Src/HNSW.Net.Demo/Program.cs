// <copyright file="Program.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net.Demo
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The demo program
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Etry point.
        /// </summary>
        public static void Main()
        {
            var parameters = new SmallWorld<float[], float>.Parameters();
            var graph = new SmallWorld<float[], float>(CosineDistance.NonOptimized);

            var vectorsGenerator = new Random(42);
            var randomVectors = new List<float[]>();
            for (int i = 0; i < 40_000; i++)
            {
                var randomVector = new float[20];
                for (int j = 0; j < 20; j++)
                {
                    randomVector[j] = (float)vectorsGenerator.NextDouble();
                }

                randomVectors.Add(randomVector);
            }

            graph.BuildGraph(randomVectors, new Random(42), parameters);
        }
    }
}
