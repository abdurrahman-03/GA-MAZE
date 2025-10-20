using System;
using UnityEngine;

public class DamageableWall : MonoBehaviour
{
    [Header("Wall Health")]
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
        healthBar.name = "WallHealthBar";
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

        float ratio = Mathf.Clamp01(currentHealth / maxHealth);
        healthBar.transform.localScale = new Vector3(ratio, 0.2f, 0.1f);

        if (currentHealth <= 0f)
        {
            Destroy(healthBar);
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (healthBar != null && healthBar.activeSelf &&
            Time.time - lastHitTime > healthBarDisappearDelay)
            healthBar.SetActive(false);

        if (healthBar != null && Camera.main != null)
            healthBar.transform.rotation =
                Quaternion.LookRotation(healthBar.transform.position -
                Camera.main.transform.position);
    }
}
