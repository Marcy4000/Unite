public enum ObjectiveType { Zapdos, Drednaw, Rotom, Registeel }

public static class ObjectivesDatabase 
{
    public static ObjectiveType GetObjectiveType(string objectiveName)
    {
        objectiveName = objectiveName.ToLower();

        switch (objectiveName)
        {
            case "zapdos":
                return ObjectiveType.Zapdos;
            case "drednaw":
                return ObjectiveType.Drednaw;
            case "rotom":
                return ObjectiveType.Rotom;
            case "registeel":
                return ObjectiveType.Registeel;
            default:
                return ObjectiveType.Zapdos;
        }
    }
}
