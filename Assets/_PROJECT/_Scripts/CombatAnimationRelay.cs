using UnityEngine;

public class CombatAnimationRelay : MonoBehaviour
{
    // Arraste o seu objeto Player (que tem o PlayerMeleeCombat) para cá no Inspector
    public PlayerMeleeCombat meleeCombat;

    // Estas funções serão chamadas pela animação da UI e repassadas ao script do Player
    public void AE_Hit_Straight()
    {
        if (meleeCombat != null) meleeCombat.AE_Hit_Straight();
    }

    public void AE_Hit_Overhead()
    {
        if (meleeCombat != null) meleeCombat.AE_Hit_Overhead();
    }

    public void AE_Hit_Uppercut()
    {
        if (meleeCombat != null) meleeCombat.AE_Hit_Uppercut();
    }
}