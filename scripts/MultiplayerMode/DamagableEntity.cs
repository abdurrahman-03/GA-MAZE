using UnityEngine;

public class DamageableEntity : MonoBehaviour
{
    public float maxHealth = 100f;
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
        if (healthBar != null) return; // already created

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
        // already dead? ignore further hits
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        lastHitTime = Time.time;

        // recreate bar if it was destroyed for some reason
        if (healthBar == null) CreateHealthBar();

        if (healthBar != null && !healthBar.activeSelf)
            healthBar.SetActive(true);

        UpdateHealthBar();

        // reached zero -> "death" logic
        if (currentHealth <= 0f)
        {
            if (healthBar != null)
            {
                Destroy(healthBar);
                healthBar = null;
            }
            // TODO: add player death handling here (respawn, game over, etc.)
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
                Quaternion.LookRotation(healthBar.transform.position -
                Camera.main.transform.position);
    }
}
