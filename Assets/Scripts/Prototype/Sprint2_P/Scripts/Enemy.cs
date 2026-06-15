using System.Collections;
using TMPro;
using UnityEngine;

public class Enemy : MonoBehaviour
{

    //public int hitPoints;
    public float armor;
    public float baseDefense;

    public TMP_Text damageNumber;
    public UIStats stats;

    public void dealDamage(int damage)
    {
        float finalDefense = armor + baseDefense;
        int accumulation = damage - (int)finalDefense;
        if (accumulation < 0) {
        accumulation = 0;
        }

        if (stats != null) {
            //stats.ShowDamage(accumulation);
            damageNumber.SetText(accumulation.ToString());
            StartCoroutine(Vanish());
        }
        
    }

    IEnumerator Vanish()
    {
        yield return new WaitForSeconds(2f);
        damageNumber.SetText("");
    }

}
