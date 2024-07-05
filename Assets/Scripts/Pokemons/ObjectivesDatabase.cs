public enum ObjectiveType { Zapdos, Drednaw, Rotom }

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
            default:
                return ObjectiveType.Zapdos;
        }
    }
}
