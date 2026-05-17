using System;

public class ResourceChangedEventArgs : EventArgs
{
    public int Amount;
    public int CurrentResourceAmount;
    public Resources ResourceType;
}
