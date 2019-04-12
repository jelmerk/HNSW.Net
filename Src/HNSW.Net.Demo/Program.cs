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
            parameters.EnableDistanceCacheForConstruction = true;
            var graph = new SmallWorld<float[], float>(CosineDistance.SIMDForUnits);

            var vectorsGenerator = new Random(42);
            var randomVectors = new List<float[]>();

            // The upper limit for the current distance cache implementation is 65535 points
            for (int i = 0; i < 60_000; i++)
            {
                var randomVector = new float[20];
                for (int j = 0; j < 20; j++)
                {
                    randomVector[j] = (float)vectorsGenerator.NextDouble();
                }

                VectorUtils.NormalizeSIMD(randomVector);
                randomVectors.Add(randomVector);
            }

            graph.BuildGraph(randomVectors, new Random(42), parameters);
        }
    }
}
