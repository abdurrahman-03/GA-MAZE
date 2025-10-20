using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GAOptimizer : MonoBehaviour
{
    public enum CrossoverType { Uniform, SinglePoint }

    [Header("GA Parameters")]
    [Tooltip("Number of individuals per generation")]
    public int populationSize = 50;

    [Tooltip("How many generations to run")]
    public int numGenerations = 100;

    [Header("Selection Settings")]
    [Tooltip("Number of competitors in each tournament")]
    public int tournamentSize = 2;

    [Header("Crossover Settings")]
    [Tooltip("Chance that two parents will crossover")]
    [Range(0f, 1f)]
    public float crossoverRate = 0.7f;

    [Tooltip("Type of crossover to use")]
    public CrossoverType crossoverType = CrossoverType.Uniform;

    [Header("Mutation Settings")]
    [Tooltip("Chance that any gene will mutate")]
    [Range(0f, 1f)]
    public float mutationRate = 0.1f;

    [Tooltip("Maximum magnitude of mutation step")]
    public float mutationStepSize = 0.1f;

    [Serializable]
    public class GAConstraint
    {
        public float[] state;
        public string chosenAction;
        public float timestamp;
    }

    [Serializable]
    private class ConstraintListWrapper
    {
        public List<GAConstraint> constraints;
    }

    public static GAOptimizer Instance { get; private set; }

    private List<GAConstraint> mergedConstraints;
    private readonly string[] actions = { "Laser", "blocking", "hindering", "escaping", "showing up", "Teleport", "charging" };
    private const int numFeatures = 5;

    [HideInInspector] public float[] bestIndividual;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        LoadMergedConstraints();
    }

    void Start()
    {
        StartCoroutine(RunGA());
    }

    private void LoadMergedConstraints()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "ga_constraints_merged.json");
        if (!File.Exists(path))
        {
            Debug.LogError("GAOptimizer: merged constraints file missing!");
            mergedConstraints = new List<GAConstraint>();
            return;
        }

        string json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<ConstraintListWrapper>(json);
        mergedConstraints = wrapper?.constraints ?? new List<GAConstraint>();
        Debug.Log($"GAOptimizer: loaded {mergedConstraints.Count} constraints");
    }

    private IEnumerator RunGA()
    {
        int geneLen = actions.Length * numFeatures;
        var population = new List<float[]>();
        var fits = new List<float>();

        // Initialize random population
        for (int i = 0; i < populationSize; i++)
        {
            var indiv = new float[geneLen];
            for (int g = 0; g < geneLen; g++)
                indiv[g] = UnityEngine.Random.Range(-1f, 1f);
            population.Add(indiv);
        }

        float bestFit = float.NegativeInfinity;

        for (int gen = 0; gen < numGenerations; gen++)
        {
            fits.Clear();
            for (int i = 0; i < population.Count; i++)
            {
                float f = EvaluateIndividual(population[i]);
                fits.Add(f);
                if (f > bestFit)
                {
                    bestFit = f;
                    bestIndividual = (float[])population[i].Clone();
                }
            }

            Debug.Log($"GAOptimizer: Generation {gen} best fitness = {bestFit:F2}");

            // Reproduction
            var newPop = new List<float[]>();
            while (newPop.Count < populationSize)
            {
                var p1 = TournamentSelection(population, fits);
                var p2 = TournamentSelection(population, fits);
                float[] c1, c2;

                if (UnityEngine.Random.value < crossoverRate)
                {
                    if (crossoverType == CrossoverType.SinglePoint)
                        SinglePointCrossover(p1, p2, out c1, out c2);
                    else
                        UniformCrossover(p1, p2, out c1, out c2);
                }
                else
                {
                    c1 = (float[])p1.Clone();
                    c2 = (float[])p2.Clone();
                }

                Mutate(c1);
                Mutate(c2);
                newPop.Add(c1);
                if (newPop.Count < populationSize) newPop.Add(c2);
            }
            population = newPop;
            yield return null;
        }

        Debug.Log($"GAOptimizer: Done. Best fitness = {bestFit:F2}");
        PrintIndividual(bestIndividual);
    }

    private float EvaluateIndividual(float[] indiv)
    {
        float score = 0f;
        foreach (var c in mergedConstraints)
        {
            int idx = Array.IndexOf(actions, c.chosenAction);
            if (idx < 0) continue;

            for (int alt = 0; alt < actions.Length; alt++)
            {
                if (alt == idx) continue;

                float diff = 0f;
                for (int f = 0; f < numFeatures; f++)
                    diff += (indiv[idx * numFeatures + f] - indiv[alt * numFeatures + f]) * c.state[f];

                float margin = diff - 0.01f;
                score += margin >= 0 ? margin : 10f * margin;
            }
        }
        return score;
    }

    private float[] TournamentSelection(List<float[]> pop, List<float> fits)
    {
        int best = -1;
        float bestFit = float.NegativeInfinity;
        for (int i = 0; i < tournamentSize; i++)
        {
            int r = UnityEngine.Random.Range(0, pop.Count);
            if (fits[r] > bestFit)
            {
                bestFit = fits[r];
                best = r;
            }
        }
        return pop[best];
    }

    private void UniformCrossover(float[] p1, float[] p2, out float[] c1, out float[] c2)
    {
        int len = p1.Length;
        c1 = new float[len];
        c2 = new float[len];
        for (int i = 0; i < len; i++)
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                c1[i] = p1[i];
                c2[i] = p2[i];
            }
            else
            {
                c1[i] = p2[i];
                c2[i] = p1[i];
            }
        }
    }

    private void SinglePointCrossover(float[] p1, float[] p2, out float[] c1, out float[] c2)
    {
        int len = p1.Length;
        int point = UnityEngine.Random.Range(1, len);
        c1 = new float[len];
        c2 = new float[len];
        for (int i = 0; i < len; i++)
        {
            if (i < point)
            {
                c1[i] = p1[i];
                c2[i] = p2[i];
            }
            else
            {
                c1[i] = p2[i];
                c2[i] = p1[i];
            }
        }
    }

    private void Mutate(float[] indiv)
    {
        for (int i = 0; i < indiv.Length; i++)
        {
            if (UnityEngine.Random.value < mutationRate)
            {
                float delta = UnityEngine.Random.Range(-mutationStepSize, mutationStepSize);
                indiv[i] = Mathf.Clamp(indiv[i] + delta, -1f, 1f);
            }
        }
    }

    private void PrintIndividual(float[] indiv)
    {
        if (indiv == null)
        {
            Debug.Log("GAOptimizer: No best individual found.");
            return;
        }

        for (int a = 0; a < actions.Length; a++)
        {
            string line = $"{actions[a]} weights:";
            for (int f = 0; f < numFeatures; f++)
                line += $" {indiv[a * numFeatures + f]:F2}";
            Debug.Log("GAOptimizer " + line);
        }
    }
}
