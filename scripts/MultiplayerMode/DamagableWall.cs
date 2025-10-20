// DamageableWall.cs
using System;
using UnityEngine;

public class DamageableWall : MonoBehaviour
{
    public float maxHealth = 50f;
    public float currentHealth;
    public float healthBarDisappearDelay = 3f;

    private float lastHitTime;
    private GameObject healthBar;

    void Awake()
    {
        currentHealth = maxHealth;
        CreateHealthBar();
        lastHitTime = Time.time;
    }

    void CreateHealthBar()
    {
        if (healthBar != null) return;

        healthBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        healthBar.transform.SetParent(transform);
        healthBar.transform.localPosition = new Vector3(0, 1.5f, 0);
        healthBar.transform.localScale = new Vector3(1f, 0.2f, 0.1f);

        Destroy(healthBar.GetComponent<Collider>());
        healthBar.GetComponent<Renderer>().material.color = Color.green;
        healthBar.SetActive(false);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        lastHitTime = Time.time;

        if (healthBar == null) CreateHealthBar();
        if (healthBar != null && !healthBar.activeSelf)
            healthBar.SetActive(true);

        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
            if (healthBar != null)
            {
                Destroy(healthBar);
                healthBar = null;
            }

            // --- only record if this wall was blocking LOS between the two players ---
            bool isBlockingWall = false;
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length >= 2)
            {
                var pcA = players[0];
                var pcB = players[1];
                Vector3 headA = pcA.transform.position + Vector3.up * (pcA.entitySize * 0.5f);
                Vector3 headB = pcB.transform.position + Vector3.up * (pcB.entitySize * 0.5f);
                Vector3 dir = (headB - headA).normalized;
                float dist = Vector3.Distance(headA, headB);

                RaycastHit[] hits = Physics.RaycastAll(headA, dir, dist);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    var go = h.collider.gameObject;
                    if (go == pcA.gameObject || go == pcB.gameObject)
                        continue; // ignore the players themselves

                    // first non-player hit -> if it’s this wall, it’s the blocking wall
                    if (go == this.gameObject)
                        isBlockingWall = true;
                    break;
                }
            }

            if (isBlockingWall)
            {
                float destructionTime = Time.time;
                // now dummyState matches 5 features
                float[] dummyState = new float[5] { 0f, 0f, 0f, 0f, 0f };
                GAOptimizer.Instance.AddConstraint(
                    "server",
                    new GAOptimizer.GAConstraint(dummyState, "wall in between is destroyed", destructionTime)
                );
                GAOptimizer.Instance.AddConstraint(
                    "client",
                    new GAOptimizer.GAConstraint(dummyState, "wall in between is destroyed", destructionTime)
                );
            }

            Destroy(gameObject);
        }
    }

    void UpdateHealthBar()
    {
        if (healthBar == null) return;

        float ratio = Mathf.Clamp01(currentHealth / maxHealth);
        healthBar.transform.localScale = new Vector3(ratio, 0.2f, 0.1f);
    }

    void Update()
    {
        if (healthBar != null && healthBar.activeSelf &&
            Time.time - lastHitTime > healthBarDisappearDelay)
            healthBar.SetActive(false);

        if (healthBar != null && Camera.main != null)
            healthBar.transform.rotation =
                Quaternion.LookRotation(healthBar.transform.position - Camera.main.transform.position);
    }
}
