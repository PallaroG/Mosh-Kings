using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct ImpactData : INetworkSerializable
{
    public float damage;
    public Vector3 direction;
    public Vector3 sourcePosition;
    public float force;
    public float duration;
    public float maxExternalSpeed;
    [Range(0f, 1f)] public float targetControlMultiplier;
    public float externalDeceleration;
    public bool useWallImpactSettings;
    public WallImpactMode wallImpactMode;
    [Range(0f, 1f)] public float wallForceLossMultiplier;
    public float strongWallImpactSpeed;
    public bool reduceControlOnWallImpact;
    [Range(0f, 1f)] public float wallImpactControlMultiplier;

    public ImpactData(
        float damage,
        Vector3 direction,
        float force,
        float duration,
        float maxExternalSpeed,
        float targetControlMultiplier,
        Vector3 sourcePosition = default,
        float externalDeceleration = 0f,
        bool useWallImpactSettings = false,
        WallImpactMode wallImpactMode = WallImpactMode.Slide,
        float wallForceLossMultiplier = -1f,
        float strongWallImpactSpeed = 0f,
        bool reduceControlOnWallImpact = true,
        float wallImpactControlMultiplier = -1f)
    {
        this.damage = damage;
        this.direction = direction;
        this.sourcePosition = sourcePosition;
        this.force = force;
        this.duration = duration;
        this.maxExternalSpeed = maxExternalSpeed;
        this.targetControlMultiplier = targetControlMultiplier;
        this.externalDeceleration = externalDeceleration;
        this.useWallImpactSettings = useWallImpactSettings;
        this.wallImpactMode = wallImpactMode;
        this.wallForceLossMultiplier = wallForceLossMultiplier;
        this.strongWallImpactSpeed = strongWallImpactSpeed;
        this.reduceControlOnWallImpact = reduceControlOnWallImpact;
        this.wallImpactControlMultiplier = wallImpactControlMultiplier;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref damage);
        serializer.SerializeValue(ref direction);
        serializer.SerializeValue(ref sourcePosition);
        serializer.SerializeValue(ref force);
        serializer.SerializeValue(ref duration);
        serializer.SerializeValue(ref maxExternalSpeed);
        serializer.SerializeValue(ref targetControlMultiplier);
        serializer.SerializeValue(ref externalDeceleration);
        serializer.SerializeValue(ref useWallImpactSettings);

        int wallImpactModeValue = (int)wallImpactMode;
        serializer.SerializeValue(ref wallImpactModeValue);
        if (serializer.IsReader)
            wallImpactMode = (WallImpactMode)wallImpactModeValue;

        serializer.SerializeValue(ref wallForceLossMultiplier);
        serializer.SerializeValue(ref strongWallImpactSpeed);
        serializer.SerializeValue(ref reduceControlOnWallImpact);
        serializer.SerializeValue(ref wallImpactControlMultiplier);
    }
}
