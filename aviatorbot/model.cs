using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

public class Model
{
    private int timeSteps;
    private float testSize;
    private int nEstimators;
    private int randomState;
    private MLContext mlContext;
    private ITransformer model;
    private DataViewSchema modelSchema;

    public Model(int timeSteps = 10, float testSize = 0.2f, int nEstimators = 100, int randomState = 42)
    {
        this.timeSteps = timeSteps;
        this.testSize = testSize;
        this.nEstimators = nEstimators;
        this.randomState = randomState;
        this.mlContext = new MLContext(seed: randomState);
    }

    private class MultipleTimestepData
    {
        [VectorType(10)]
        public float[] Features { get; set; }
        public float Label { get; set; }
    }

    private (IDataView TrainSet, IDataView TestSet) PrepareData(float[] multipliers)
    {
        var data = new List<MultipleTimestepData>();
        for (int i = 0; i < multipliers.Length - timeSteps; i++)
        {
            data.Add(new MultipleTimestepData
            {
                Features = multipliers.Skip(i).Take(timeSteps).Select(x => (float)x).ToArray(),
                Label = multipliers[i + timeSteps]
            });
        }

        var dataView = mlContext.Data.LoadFromEnumerable(data);

        var splitData = mlContext.Data.TrainTestSplit(dataView, testFraction: testSize);
        return (splitData.TrainSet, splitData.TestSet);
    }

    private void TrainModel(IDataView trainSet)
    {
        var pipeline = mlContext.Transforms.NormalizeMinMax("Features")
            .Append(mlContext.Transforms.Concatenate("Features", "Features"))
            .Append(mlContext.Regression.Trainers.FastTree(numberOfLeaves: 20, numberOfTrees: nEstimators, minimumExampleCountPerLeaf: 10));

        model = pipeline.Fit(trainSet);
        modelSchema = trainSet.Schema;
    }

    public (float prediction, float confidence) PredictMultiplier(float[] recentMultipliers)
    {
        if (recentMultipliers.Length < timeSteps)
        {
            return (1.0f, 0.0f);  // Default prediction if not enough data
        }

        var predictionEngine = mlContext.Model.CreatePredictionEngine<MultipleTimestepData, PredictionOutput>(model, modelSchema);

        var inputData = new MultipleTimestepData
        {
            Features = recentMultipliers.TakeLast(timeSteps).Select(x => (float)x).ToArray()
        };

        var prediction = predictionEngine.Predict(inputData);

        // Adjust the prediction to be slightly lower and cap it
        float adjustedPrediction = Math.Max(1.0f, Math.Min(prediction.Score * 0.8f, 800.0f));

        // Calculate confidence based on how close the prediction is to recent multipliers
        float confidence = 1 - (Math.Abs(adjustedPrediction - recentMultipliers.Average()) / adjustedPrediction);
        confidence = Math.Max(0, Math.Min(confidence, 1));  // Ensure confidence is between 0 and 1

        return (adjustedPrediction, confidence);
    }

    public (float prediction, float confidence) FitAndPredict(float[] multipliers, float[] recentMultipliers)
    {
        var (trainSet, _) = PrepareData(multipliers);
        TrainModel(trainSet);

        return PredictMultiplier(recentMultipliers);
    }

    private class PredictionOutput
    {
        [ColumnName("Score")]
        public float Score;
    }
}