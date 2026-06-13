using UnityEngine;

public interface IMinionPool
{
    GameObject GetMinion(GameObject prefab);
    void Return(GameObject obj, GameObject prefab);
}