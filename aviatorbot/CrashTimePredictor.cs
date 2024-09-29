using System;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;

public class CrashTimePredictor
{
    private double[] coefficients;
    private int degree;

    public CrashTimePredictor(int polynomialDegree = 3)
    {
        degree = polynomialDegree;
        TrainModel();
    }

    private void TrainModel()
    {
        // Given data points (multiplier, time in seconds)
        double[] multipliers = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        double[] times =       { 9, 14, 17, 20, 22, 24, 25, 26, 27, 28 };

        // Create polynomial features
        var X = CreatePolynomialFeatures(multipliers);

        // Train the model using ordinary least squares
        coefficients = Fit.MultiDim(X.ToRowArrays(), times, intercept: false);
    }

    private Matrix<double> CreatePolynomialFeatures(double[] input)
    {
        int n = input.Length;
        var result = Matrix<double>.Build.Dense(n, degree + 1);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= degree; j++)
            {
                result[i, j] = Math.Pow(input[i], j);
            }
        }

        return result;
    }

    public double Predict(double multiplier)
    {
        var features = new double[degree + 1];
        for (int i = 0; i <= degree; i++)
        {
            features[i] = Math.Pow(multiplier, i);
        }

        return features.Zip(coefficients, (f, c) => f * c).Sum();
    }
}

// Usage example
class Program
{
    static void Mkoko(string[] args)
    {
        var predictor = new CrashTimePredictor();

        // Example predictions
        for (double i = 0; i < 20; i+=0.2)
        { 
            Console.WriteLine($"Predicted crash time for multiplier {i}: {predictor.Predict(i):F2} seconds");
        }
        Console.ReadLine();
    }
}