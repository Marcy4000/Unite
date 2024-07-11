using UnityEngine;

public class SylvCalmMind : MoveBase
{
    private StatChange[] calmMindBuffs = new StatChange[3]
    {
        new StatChange(40, Stat.Speed, 3f, true, true, true, 0),
        new StatChange(40, Stat.SpAttack, 3f, true, true, true, 0),
        new StatChange(10, Stat.SpDefense, 3f, true, true, true, 0)
    };

    public SylvCalmMind()
    {
        Name = "Calm Mind";
        Cooldown = 10.0f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (!IsActive)
        {
            return;
        }

        foreach (StatChange buff in calmMindBuffs)
        {
            playerManager.Pokemon.AddStatChange(buff);
        }

        wasMoveSuccessful = true;
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }
}
