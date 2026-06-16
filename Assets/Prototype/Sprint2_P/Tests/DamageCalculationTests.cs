using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class DamageCalculationTests
{
    private GameObject obj;
    private DamageCalculation dmgCalc;
    private GunData gun;
    [SetUp]
    public void TestCollection() {
        obj = new GameObject();
        dmgCalc = obj.AddComponent<DamageCalculation>();
        gun = new GunData();
    }
    // A Test behaves as an ordinary method
    [Test]
    public void DamageCalc_NoCrit_ReturnNormalDmg()
    {
        // Use the Assert class to test conditions
        gun.attackBase = 100;
        gun.attackFlat = 50;
        gun.attackPercentage = 250;

        gun.baseCritDmg = 50;
        gun.critDmg = 100;
        gun.critChange = 0;

        int result = dmgCalc.CalculateDamage(gun);
        int expectedDmg = 400;
        Assert.AreEqual(expectedDmg, result);
    }

    [Test]
    public void DamageCalc_Crit_ReturnCritDmg()
    {
        // Use the Assert class to test conditions
        gun.attackBase = 100;
        gun.attackFlat = 50;
        gun.attackPercentage = 250;

        gun.baseCritDmg = 50;
        gun.critDmg = 100;
        gun.critChange = 100;

        int result = dmgCalc.CalculateDamage(gun);
        int expectedDmg = 800;
        Assert.AreEqual(expectedDmg, result);
    }

    [Test]
    public void CritProc_CritChange100_ReturnTrue()
    {
        // Use the Assert class to test conditions
        gun.critChange = 100;
        bool result = dmgCalc.CritProcs(gun);
        Assert.IsTrue(result);
    }

    [Test]
    public void CritProc_CritChange0_ReturnFalse()
    {
        // Use the Assert class to test conditions
        gun.critChange = 0;
        bool result = dmgCalc.CritProcs(gun);
        Assert.IsFalse(result);
    }
}
