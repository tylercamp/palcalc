using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ty chatgpt

namespace PalCalc.GenDB
{
    // Classes for JSON parsing
    public class Coord
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class Sample
    {
        public Coord WorldCoords { get; set; }
        public Coord MapCoords { get; set; }
        public Coord ImageCoords { get; set; }
    }

    class MapTransformSolver
    {
        public static void Run(string sampleJsonPath, int sampleMapTexSize)
        {
            // Read and parse JSON
            var json = File.ReadAllText(sampleJsonPath).Split('\n').Select(l => l.Split("//")[0]).ToList();
            var samples = JsonConvert.DeserializeObject<List<Sample>>(string.Join('\n', json));

            // Solve for World->Map
            var worldToMap = ComputeAffineTransform(
                samples.Select(s => s.WorldCoords).ToList(),
                samples.Select(s => s.MapCoords).ToList()
            );

            // Solve for World->Image
            var worldToImage = ComputeAffineTransform(
                samples.Select(s => s.WorldCoords).ToList(),
                samples.Select(s => s.ImageCoords).Select(c => new Coord() { X = c.X / sampleMapTexSize, Y = c.Y / sampleMapTexSize }).ToList()
            );

            // Print 3x3 matrices
            Console.WriteLine("WorldToMapMatrix:");
            PrintMatrix(worldToMap);

            Console.WriteLine("\nWorldToImageMatrix:");
            PrintMatrix(worldToImage);
        }

        /// <summary>
        /// Computes an affine transformation matrix (3×3) that maps source -> target.
        /// The affine transform is:
        ///   [x']   [a b c] [x]
        ///   [y'] = [d e f] [y]
        ///   [1 ]   [0 0 1] [1]
        /// </summary>
        static double[,] ComputeAffineTransform(List<Coord> source, List<Coord> target)
        {
            // We solve for [a b c d e f] in x' = a*x + b*y + c, y' = d*x + e*y + f
            // Build least-squares system: M·params = B
            // M is (2n×6), params is (6×1), B is (2n×1)

            int n = source.Count;
            double[,] M = new double[2 * n, 6];
            double[] B = new double[2 * n];

            for (int i = 0; i < n; i++)
            {
                double x = source[i].X;
                double y = source[i].Y;
                double xPrime = target[i].X;
                double yPrime = target[i].Y;

                // Row for x'
                M[2 * i, 0] = x;
                M[2 * i, 1] = y;
                M[2 * i, 2] = 1;
                M[2 * i, 3] = 0;
                M[2 * i, 4] = 0;
                M[2 * i, 5] = 0;
                B[2 * i] = xPrime;

                // Row for y'
                M[2 * i + 1, 0] = 0;
                M[2 * i + 1, 1] = 0;
                M[2 * i + 1, 2] = 0;
                M[2 * i + 1, 3] = x;
                M[2 * i + 1, 4] = y;
                M[2 * i + 1, 5] = 1;
                B[2 * i + 1] = yPrime;
            }

            double[] solution = SolveLeastSquares(M, B);

            // Convert [a b c d e f] to a 3×3 matrix
            double[,] matrix = new double[3, 3];
            matrix[0, 0] = solution[0];
            matrix[0, 1] = solution[1];
            matrix[0, 2] = solution[2];
            matrix[1, 0] = solution[3];
            matrix[1, 1] = solution[4];
            matrix[1, 2] = solution[5];
            matrix[2, 0] = 0;
            matrix[2, 1] = 0;
            matrix[2, 2] = 1;

            return matrix;
        }

        /// <summary>
        /// Solves M·x = B in a least squares sense using a naive pseudo-inverse approach.
        /// </summary>
        static double[] SolveLeastSquares(double[,] M, double[] B)
        {
            // For brevity, we’ll just implement a simple Normal Equation approach:
            // (M^T M) x = M^T B
            // x = (M^T M)^-1 (M^T B)
            //
            // This is not the most numerically robust method, but it's easy to show in an example.

            int rows = M.GetLength(0);
            int cols = M.GetLength(1);

            // Compute M^T M
            double[,] MT = Transpose(M);
            double[,] MTM = Multiply(MT, M);
            // Compute M^T B
            double[] MTB = Multiply(MT, B);
            // Invert (M^T M)
            double[,] inv = Invert2D(MTM);
            // Then x = inv * (M^T B)
            double[] x = Multiply(inv, MTB);

            return x;
        }

        #region Matrix Helpers

        static double[,] Transpose(double[,] A)
        {
            int r = A.GetLength(0);
            int c = A.GetLength(1);
            double[,] T = new double[c, r];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    T[j, i] = A[i, j];
            return T;
        }

        static double[,] Multiply(double[,] A, double[,] B)
        {
            int rA = A.GetLength(0);
            int cA = A.GetLength(1);
            int rB = B.GetLength(0);
            int cB = B.GetLength(1);

            double[,] result = new double[rA, cB];
            for (int i = 0; i < rA; i++)
                for (int j = 0; j < cB; j++)
                    for (int k = 0; k < cA; k++)
                        result[i, j] += A[i, k] * B[k, j];

            return result;
        }

        static double[] Multiply(double[,] A, double[] v)
        {
            int rA = A.GetLength(0);
            int cA = A.GetLength(1);

            double[] result = new double[rA];
            for (int i = 0; i < rA; i++)
                for (int j = 0; j < cA; j++)
                    result[i] += A[i, j] * v[j];
            return result;
        }

        // Naive matrix inverse for 6×6 (or smaller) normal equation use. 
        // For brevity, we assume it’s invertible and do basic Gauss-Jordan.
        static double[,] Invert2D(double[,] A)
        {
            int n = A.GetLength(0);
            double[,] M = new double[n, n];
            double[,] I = new double[n, n];

            // Copy A into M
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    M[r, c] = A[r, c];
                    I[r, c] = (r == c) ? 1 : 0;
                }

            // Gauss-Jordan
            for (int i = 0; i < n; i++)
            {
                // Pivot
                double pivot = M[i, i];
                if (Math.Abs(pivot) < 1e-12) throw new Exception("Singular matrix");
                for (int c = 0; c < n; c++)
                {
                    M[i, c] /= pivot;
                    I[i, c] /= pivot;
                }

                // Eliminate
                for (int r = 0; r < n; r++)
                {
                    if (r == i) continue;
                    double factor = M[r, i];
                    for (int c = 0; c < n; c++)
                    {
                        M[r, c] -= factor * M[i, c];
                        I[r, c] -= factor * I[i, c];
                    }
                }
            }
            return I;
        }

        static void PrintMatrix(double[,] mat)
        {
            int rows = mat.GetLength(0);
            int cols = mat.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Console.Write(mat[r, c].ToString() + (c == cols - 1 ? "" : " "));
                }
                Console.WriteLine();
            }
        }

        #endregion
    }

}
