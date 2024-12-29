using TMPro;
using UnityEngine;

public class DamageIndicator : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private SpriteRenderer critImage;

    [SerializeField] private Sprite[] critSprites;

    private Rigidbody rb;

    public void ShowDamage(int damage, DamageType damageType, bool crit)
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
        damageText.text = damage.ToString();
        switch (damageType)
        {
            case DamageType.Physical:
                damageText.color = Color.red;
                break;
            case DamageType.Special:
                damageText.color = Color.magenta;
                break;
            case DamageType.True:
                damageText.color = Color.white;
                break;
        }

        if (crit)
        {
            critImage.gameObject.SetActive(true);
            critImage.sprite = critSprites[(int)damageType];
            damageText.fontSize = 6.2f;
        }
        else
        {
            critImage.gameObject.SetActive(false);
            damageText.fontSize = 4f;
        }

        Destroy(gameObject, 1f);
    }

    public void ShowHeal(int heal)
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
        damageText.text = heal.ToString();
        damageText.color = Color.green;
        damageText.fontSize = 4.5f;
        critImage.gameObject.SetActive(false);
        Destroy(gameObject, 1f);
    }
}
