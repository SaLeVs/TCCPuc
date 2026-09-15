using Monster.HSM;
using Monster.MonsterStates.HuntStates;
using UnityEngine;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterHunt : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly ChaseState chaseState;
        public readonly AttackState attackState;
        
        private float distanceToTarget;
        private float distanceToAttack;
        
        public MonsterHunt(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            chaseState = new ChaseState(stateMachine, this, monsterBrain);
            attackState = new AttackState(stateMachine, this, monsterBrain);
        }
    
        protected override State GetInitialState() => chaseState;

        protected override void OnUpdate(float deltaTime)
        {
            // Held at a door: the forcer already owns the agent and the attack animation. Letting
            // the chase/attack swap run on top of it drives both at once and makes it stutter.
            if (_monsterBrain.IsForcingDoor) return;

            _monsterBrain.MonsterChase.UpdateDistanceFromTarget();

            distanceToTarget = _monsterBrain.MonsterChase.DistanceFromTarget;
            distanceToAttack = _monsterBrain.MonsterAttack.DistanceToAttack;

            // Only swing at something it can actually see. While it is coasting on a lost target's
            // last fix the distance is still real, and without this it attacks through the wall it
            // is chasing you around — stopping, swinging, stopping again instead of running.
            bool canSeeTarget = _monsterBrain._playersInVision.Count > 0;

            // Chase again while recovering, instead of standing in the target's face doing
            // nothing. Waiting out the cooldown at arm's length used to be safer than running,
            // which is backwards for a horror game — now backing off keeps it coming.
            // Note IsOnCooldown is false *during* a swing (it starts when the swing ends), so
            // this never interrupts an attack in progress.
            bool readyToSwing = !_monsterBrain.MonsterAttack.IsOnCooldown;

            if (canSeeTarget && readyToSwing && distanceToTarget <= distanceToAttack)
            {
                if (ActiveChild != attackState)
                {
                    StateMachine.Sequencer.RequestTransition(chaseState, attackState);
                }
            }
            else
            {
                if (ActiveChild != chaseState)
                {
                    StateMachine.Sequencer.RequestTransition(attackState, chaseState);
                }
            }
        }

        protected override State GetTransitionState() => null;
    }
}