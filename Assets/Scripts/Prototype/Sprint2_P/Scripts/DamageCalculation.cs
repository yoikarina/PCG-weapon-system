using UnityEngine;
using Random = UnityEngine.Random;

public class DamageCalculation : MonoBehaviour
{

    public UIStats Stats;

    public int finalDmg;
    public float calculatedDmg;

    [Header("base Stats")]
    public float baseCritDmg;
    public float attackBase;

    [Header("Stats buff")]
    public float critDmg;
    public float critChange;
    public float attackFlat;
    public float attackPercentage;

    private int maximumCritChange = 100;


    public int DamageCalc(GunData Calc)
    {
        baseCritDmg = Calc.baseCritDmg;
        critDmg = Calc.critDmg;
        attackBase = Calc.attackBase;
        critChange = Calc.critChange;   
        attackFlat = Calc.attackFlat;
        attackPercentage = Calc.attackPercentage;

        if (Stats != null) {
            Stats.ShowStats(Calc);
        }

        float totalPower = attackBase + attackFlat + (attackBase * (attackPercentage / 100f));

        int random = Random.Range(0, maximumCritChange);
        if (random < critChange) {
            Debug.Log("Gets here");
            float CritDmg = baseCritDmg + critDmg;
            float critMulti = 1f + (CritDmg / 100f);
            //calculatedDmg = attackBase * (1f + (CritDmg / 100f)) + attackFlat + (attackBase * attackPercentage);
            calculatedDmg = totalPower * critMulti;
            Debug.Log("Damage: " + calculatedDmg);
        } else {
            //calculatedDmg = attackBase + attackFlat + (attackBase * (int)attackPercentage);
            calculatedDmg = totalPower;
        }

        finalDmg = (int)calculatedDmg;
        if (Stats != null) {
            Stats.ShowDamage(finalDmg);
        }
        return finalDmg;
    }

    public int CalculateDamage(GunData calc)
    {
        float calculatedDmg;
        float totalPower = calc.attackBase + calc.attackFlat + (calc.attackBase * (calc.attackPercentage / 100f));

        if (calc.critChange == 100) {
            float critMulti = 1f + (calc.critDmg / 100f);
            calculatedDmg = totalPower * critMulti;
        } else {
            calculatedDmg = totalPower;
        }

            return (int)calculatedDmg;
    }

    public bool CritProcs(GunData calc)
    {
        if (calc.critChange == 100) {
            return true;
        } else {
            return false;
        }
    }

    //public int TestDamage()
    //{
    //    attackBase = 100;
    //    attackFlat = 50;
    //    attackPercentage = 250;

    //    float totalPower = attackBase + attackFlat + (attackBase * (attackPercentage / 100f));


    //    return (int)totalPower;
    //}


}
