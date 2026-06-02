using System.Collections.Generic;
using UnityEngine;

public class EntityManager : MonoBehaviour
{
    public static List<AI_Minion> BlueMinions = new();
    public static List<AI_Minion> RedMinions = new();

    public static List<Tower> BlueTowers = new();
    public static List<Tower> RedTowers = new();

    public static Factory BlueFactory;
    public static Factory RedFactory;
}

public enum Team
{
    Blue,
    Red
}