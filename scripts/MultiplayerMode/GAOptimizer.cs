// GAOptimizer.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GAOptimizer : MonoBehaviour
{
    #region Singleton & Constraint Persistence

    public static GAOptimizer Instance;

    [Serializable]
    public class GAConstraint
    {
        // State vector layout:
        // [tileDist, visibility, hit1, hit2, chakra]
        public float[] state;
        public string chosenAction;
        public float epsilon;
        public float timestamp;

        public GAConstraint(float[] state, string chosenAction, float timestamp, float epsilon = 0.01f)
        {
            if (state.Length != 5)
                throw new ArgumentException("State vector must be length 5.");
            this.state = state;
            this.chosenAction = chosenAction;
            this.epsilon = epsilon;
            this.timestamp = timestamp;
        }
    }

    [Serializable]
    private class ConstraintListWrapper
    {
        public List<GAConstraint> constraints = new List<GAConstraint>();
    }

    // fixed roles: server & client
    private static readonly string[] fileRoles = { "server", "client" };
    // merged file accumulates both roles across games
    private const string mergedFileName = "ga_constraints_merged.json";

    private Dictionary<string, List<GAConstraint>> playerConstraints;

    private void Awake()
    {
        Debug.Log("GAOptimizer: Writing constraints to " + Application.persistentDataPath);

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        playerConstraints = new Dictionary<string, List<GAConstraint>>();

        // Clear ONLY per-role files; leave merged intact
        foreach (var role in fileRoles)
        {
            string path = GetFilePath(role);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"GAOptimizer: Deleted existing file for '{role}'");
            }
            playerConstraints[role] = new List<GAConstraint>();
            Debug.Log($"GAOptimizer: Initialized empty constraints for '{role}'");
        }
        // NOTE: we do NOT delete the merged file; it accumulates across runs
    }

    /// <summary>
    /// Adds a new constraint for 'role', applies cross-file filters, then saves.
    /// </summary>
    public void AddConstraint(string role, GAConstraint newConstraint)
    {
        string otherRole = role == fileRoles[0] ? fileRoles[1] : fileRoles[0];
        var otherList = playerConstraints[otherRole];

        // --- PRE-FILTERING: skip adding certain actions if conflicting in other file ---
        if (newConstraint.chosenAction == "escaping")
        {
            if (otherList.Exists(c =>
                (c.chosenAction == "blocking" || c.chosenAction == "Teleport") &&
                Mathf.Abs(c.timestamp - newConstraint.timestamp) <= 0.5f))
            {
                Debug.Log($"GAOptimizer: Skipped recording 'escaping' @{newConstraint.timestamp:F2}s due to conflict");
                return;
            }
        }
        else if (newConstraint.chosenAction == "showing up")
        {
            if (otherList.Exists(c =>
                c.chosenAction == "Teleport" &&
                Mathf.Abs(c.timestamp - newConstraint.timestamp) <= 0.5f))
            {
                Debug.Log($"GAOptimizer: Skipped recording 'showing up' @{newConstraint.timestamp:F2}s due to teleport conflict");
                return;
            }
        }

        // --- ADD this constraint ---
        playerConstraints[role].Add(newConstraint);
        Debug.Log($"GAOptimizer: [{role}] +{newConstraint.chosenAction}@{newConstraint.timestamp:F2}s");
        SaveConstraintsForRole(role);

        // --- ESCAPING BLOCK: prune closer player's escaping only if timestamps match within 0.3s ---
        if (newConstraint.chosenAction == "escaping")
        {
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length >= 2)
            {
                var pServer = players[0];
                var pClient = players[1];

                Vector3 headS = pServer.transform.position + Vector3.up * (pServer.entitySize * 0.5f);
                Vector3 headC = pClient.transform.position + Vector3.up * (pClient.entitySize * 0.5f);
                Vector3 dir = (headC - headS).normalized;
                float dist = Vector3.Distance(headS, headC);

                RaycastHit[] hits = Physics.RaycastAll(headS, dir, dist);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                Collider blockingWall = null;
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    if (h.collider.GetComponent<DamageableWall>() != null)
                    {
                        blockingWall = h.collider;
                        break;
                    }
                }

                if (blockingWall != null)
                {
                    Vector3 wallPos = blockingWall.transform.position;
                    float dS = Vector3.Distance(headS, wallPos);
                    float dC = Vector3.Distance(headC, wallPos);

                    bool serverCloser = dS <= dC;
                    string closerRole = serverCloser ? fileRoles[0] : fileRoles[1];
                    string otherRoleLocal = serverCloser ? fileRoles[1] : fileRoles[0];

                    var closeList = playerConstraints[closerRole];
                    var otherEscList = playerConstraints[otherRoleLocal];

                    int closeIdx = -1, otherIdx = -1;
                    for (int i = closeList.Count - 1; i >= 0; i--)
                        if (closeList[i].chosenAction == "escaping") { closeIdx = i; break; }
                    for (int j = otherEscList.Count - 1; j >= 0; j--)
                        if (otherEscList[j].chosenAction == "escaping") { otherIdx = j; break; }

                    if (closeIdx >= 0 && otherIdx >= 0)
                    {
                        float tsClose = closeList[closeIdx].timestamp;
                        float tsOther = otherEscList[otherIdx].timestamp;
                        if (Mathf.Abs(tsClose - tsOther) <= 0.3f)
                        {
                            closeList.RemoveAt(closeIdx);
                            Debug.Log($"GAOptimizer: Removed 'escaping' from '{closerRole}' @ {tsClose:F2}s matching other @ {tsOther:F2}s");
                            SaveConstraintsForRole(closerRole);
                        }
                    }
                }
            }
        }

        // --- MUTUAL REMOVAL FOR SHOWING UP & WALL DESTRUCTION ---
        if (newConstraint.chosenAction == "showing up")
        {
            var localList = playerConstraints[role];
            GAConstraint wallConstraint = null;
            float ts = newConstraint.timestamp;
            foreach (var c in localList)
            {
                if (c.chosenAction == "wall in between is destroyed" &&
                    Mathf.Abs(c.timestamp - ts) <= 0.3f)
                {
                    wallConstraint = c;
                    break;
                }
            }
            if (wallConstraint != null)
            {
                localList.Remove(wallConstraint);
                localList.Remove(newConstraint);
                Debug.Log($"GAOptimizer: Removed 'showing up' @{ts:F2}s and 'wall in between is destroyed' @{wallConstraint.timestamp:F2}s from '{role}'");
                SaveConstraintsForRole(role);
                return;
            }
        }

        // --- POST-FILTER: remove stale actions in other file ---
        bool otherChanged = false;

        if (newConstraint.chosenAction == "blocking" || newConstraint.chosenAction == "Teleport")
        {
            int removed = otherList.RemoveAll(c =>
                c.chosenAction == "escaping" &&
                Mathf.Abs(c.timestamp - newConstraint.timestamp) <= 0.5f);
            if (removed > 0)
            {
                Debug.Log($"GAOptimizer: Removed {removed} 'escaping' from '{otherRole}' due to {newConstraint.chosenAction}@{newConstraint.timestamp:F2}s");
                otherChanged = true;
            }
        }

        if (newConstraint.chosenAction == "Teleport")
        {
            int removed = otherList.RemoveAll(c =>
                c.chosenAction == "showing up" &&
                Mathf.Abs(c.timestamp - newConstraint.timestamp) <= 0.5f);
            if (removed > 0)
            {
                Debug.Log($"GAOptimizer: Removed {removed} 'showing up' from '{otherRole}' due to Teleport@{newConstraint.timestamp:F2}s");
                otherChanged = true;
            }
        }

        if (otherChanged)
            SaveConstraintsForRole(otherRole);
    }

    /// <summary>
    /// Merges both server + client files into a single persistent file.
    /// Called once when a player is eliminated and the game freezes.
    /// </summary>
    public void MergeConstraints()
    {
        string mergedPath = Path.Combine(Application.persistentDataPath, mergedFileName);

        ConstraintListWrapper mergedWrapper;
        if (File.Exists(mergedPath))
        {
            string existingJson = File.ReadAllText(mergedPath);
            mergedWrapper = JsonUtility.FromJson<ConstraintListWrapper>(existingJson);
            if (mergedWrapper == null) mergedWrapper = new ConstraintListWrapper();
        }
        else
        {
            mergedWrapper = new ConstraintListWrapper();
        }

        mergedWrapper.constraints.AddRange(playerConstraints[fileRoles[0]]);
        mergedWrapper.constraints.AddRange(playerConstraints[fileRoles[1]]);

        string mergedJson = JsonUtility.ToJson(mergedWrapper, true);
        File.WriteAllText(mergedPath, mergedJson);
        Debug.Log($"GAOptimizer: Merged constraints into '{mergedFileName}', total entries now {mergedWrapper.constraints.Count}");
    }

    private void SaveConstraintsForRole(string role)
    {
        var wrapper = new ConstraintListWrapper
        {
            constraints = playerConstraints[role]
        };
        string json = JsonUtility.ToJson(wrapper, true);
        string path = GetFilePath(role);
        File.WriteAllText(path, json);
        Debug.Log($"GAOptimizer: Wrote {playerConstraints[role].Count} entries to '{role}'");
    }

    private string GetFilePath(string role)
        => Path.Combine(Application.persistentDataPath, $"ga_constraints_{role}.json");

    #endregion

    #region GA Parameters & Core

    private readonly string[] actions =
        { "Laser", "blocking", "hindering", "escaping", "showing up", "Teleport", "charging" };

    private const int numFeatures = 5;
    private const int geneLength = 7 * numFeatures;

    public int populationSize = 50;
    public int numGenerations = 100;
    public float mutationRate = 0.1f;
    public float crossoverRate = 0.7f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("GAOptimizer: Starting GA");
            StartCoroutine(RunGA());
        }
    }

    private IEnumerator RunGA()
    {
        var population = new List<float[]>();
        for (int i = 0; i < populationSize; i++)
        {
            var indiv = new float[geneLength];
            for (int g = 0; g < geneLength; g++)
                indiv[g] = UnityEngine.Random.Range(-1f, 1f);
            population.Add(indiv);
        }

        float bestFit = float.NegativeInfinity;
        float[] bestIndiv = null;

        for (int gen = 0; gen < numGenerations; gen++)
        {
            var fits = new List<float>();
            foreach (var indiv in population)
            {
                float f = EvaluateIndividual(indiv);
                fits.Add(f);
                if (f > bestFit)
                {
                    bestFit = f;
                    bestIndiv = (float[])indiv.Clone();
                }
            }
            Debug.Log($"Gen {gen} Best fitness {bestFit:F2}");

            var newPop = new List<float[]>();
            while (newPop.Count < populationSize)
            {
                var p1 = Tournament(population, fits, 2);
                var p2 = Tournament(population, fits, 2);

                float[] c1, c2;
                if (UnityEngine.Random.value < crossoverRate)
                    UniformCrossover(p1, p2, out c1, out c2);
                else
                {
                    c1 = (float[])p1.Clone();
                    c2 = (float[])p2.Clone();
                }

                Mutate(c1, mutationRate);
                Mutate(c2, mutationRate);

                newPop.Add(c1);
                if (newPop.Count < populationSize) newPop.Add(c2);
            }

            population = newPop;
            yield return null;
        }

        Debug.Log($"GAOptimizer: Done. Best fitness {bestFit:F2}");
        PrintIndividual(bestIndiv);
    }

    private float EvaluateIndividual(float[] indiv)
    {
        var allCons = new List<GAConstraint>();
        foreach (var kv in playerConstraints)
            allCons.AddRange(kv.Value);

        float score = 0f;
        foreach (var cons in allCons)
        {
            int chosen = Array.IndexOf(actions, cons.chosenAction);
            if (chosen < 0) continue;

            for (int alt = 0; alt < actions.Length; alt++)
            {
                if (alt == chosen) continue;
                float diff = 0f;
                for (int f = 0; f < numFeatures; f++)
                    diff += (indiv[chosen * numFeatures + f] - indiv[alt * numFeatures + f]) * cons.state[f];

                float margin = diff - cons.epsilon;
                score += margin >= 0 ? margin : 10f * margin;
            }
        }
        return score;
    }

    private float[] Tournament(List<float[]> pop, List<float> fits, int tSize)
    {
        int best = -1;
        float bestFit = float.NegativeInfinity;
        for (int i = 0; i < tSize; i++)
        {
            int idx = UnityEngine.Random.Range(0, pop.Count);
            if (fits[idx] > bestFit)
            {
                bestFit = fits[idx];
                best = idx;
            }
        }
        return pop[best];
    }

    private void UniformCrossover(float[] p1, float[] p2, out float[] c1, out float[] c2)
    {
        c1 = new float[geneLength];
        c2 = new float[geneLength];
        for (int i = 0; i < geneLength; i++)
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

    private void Mutate(float[] indiv, float rate)
    {
        for (int i = 0; i < geneLength; i++)
            if (UnityEngine.Random.value < rate)
                indiv[i] = Mathf.Clamp(indiv[i] + UnityEngine.Random.Range(-0.1f, 0.1f), -1f, 1f);
    }

    private void PrintIndividual(float[] indiv)
    {
        if (indiv == null)
        {
            Debug.Log("GAOptimizer: No individual.");
            return;
        }
        for (int a = 0; a < actions.Length; a++)
        {
            string s = $"{actions[a]}:";
            for (int f = 0; f < numFeatures; f++)
                s += $" {indiv[a * numFeatures + f]:F2}";
            Debug.Log("GAOptimizer: " + s);
        }
    }

    #endregion
}
