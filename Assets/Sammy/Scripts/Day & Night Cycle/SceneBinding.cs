using System.Collections.Generic;
using UnityEngine;

public class SceneBinding : MonoBehaviour
{
    public string Id;

    private static readonly Dictionary<string, SceneBinding> Registry = new();

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(Id))
            Registry[Id] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(Id) && Registry.TryGetValue(Id, out var me) && me == this)
            Registry.Remove(Id);
    }

    public static GameObject Resolve(string _id)
        => (!string.IsNullOrEmpty(_id) && Registry.TryGetValue(_id, out var sb)) ? sb.gameObject : null;
}