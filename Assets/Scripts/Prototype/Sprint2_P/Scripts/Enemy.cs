using UnityEngine;

public class Enemy : MonoBehaviour
{

    //public int hitPoints;
    public float armor;
    public float baseDefense;

    public UIStats stats;

    public void dealDamage(int damage)
    {
        float finalDefense = armor + baseDefense;
        int accumulation = damage - (int)finalDefense;
        if (accumulation < 0) {
        accumulation = 0;
        }
        stats.ShowDamage(accumulation);
    }

}
