using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterAttack : NetworkBehaviour
    {
        [SerializeField] private float distanceToAttack;
        [SerializeField] private float damageAmount;

        public float DistanceToAttack => distanceToAttack;
        
        
    } 
}

