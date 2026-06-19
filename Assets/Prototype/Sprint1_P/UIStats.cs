using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIStats : MonoBehaviour
{
    public TextMeshProUGUI hitPoints;
    public TextMeshProUGUI ammunitionCount;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI statsInfo;

    private int maxAmmunition;

    // compatibility test
    public Image colorCheck;

    public void UIHealthPoints(int currentHitPoints)
    {
        hitPoints.text = "HP: " + currentHitPoints;
    }

    public void UIMaxAmmo(int max)
    {
        maxAmmunition = max;
    }

    public void UIAmmo(int current)
    {
        ammunitionCount.text = current + " / " + maxAmmunition;   
    }

    public void Dropped(WeaponCollection dropped)
    {
        if (dropped == null) {
            ammunitionCount.text = null;
        } 
    }

    public void ColorChecker(bool comp)
    {
        if (comp) {
            colorCheck.color = Color.green;
        } else {
            colorCheck.color = Color.red;
        }
    }

    public void ShowDamage(int damage)
    {
        damageText.text = damage.ToString();
    }

    //public void ShowStats(GunData Calc)
    //{
    //    statsInfo.text =
    //        "Stats: " + "\n"
    //        + "CritDmg Base: " + Calc.baseCritDmg + "\n"
    //        + "CritDmg Addition: " + Calc.critDmg + "\n"
    //        + "CritDmg Total: " + (Calc.baseCritDmg + Calc.critDmg) + "\n"
    //        + "\n"
    //        + "Attack Power Base: " + Calc.attackBase + "\n"
    //        + "Attack Power Flat Addition: " + Calc.attackFlat + "\n"
    //        + "Attack Power Percentage Addition: " + Calc.attackPercentage + "\n"
    //        + "Attack Power Total: " + (Calc.attackBase + Calc.attackFlat + (Calc.attackBase * Calc.attackPercentage)) + "\n"
    //        + "\n"
    //        + "Crit Change: " + Calc.critChange;
    //}

    void Update()
    {
        
    }
}
