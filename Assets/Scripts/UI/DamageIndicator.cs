using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamageIndicator : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    private Rigidbody rb;

    public void ShowDamage(int damage, DamageType damageType)
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
        Destroy(gameObject, 1f);
    }

    public void ShowHeal(int heal)
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
        damageText.text = heal.ToString();
        damageText.color = Color.green;
        Destroy(gameObject, 1f);
    }
}
