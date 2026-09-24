using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public enum PathProblem
    {
        /// <summary>Has a path, is not stopped, and has not got anywhere for a while.</summary>
        Stuck,

        /// <summary>Stopped getting anywhere on a partial or invalid path: where it is going is not connected to where it is.</summary>
        Unreachable
    }

    /// <summary>Implemented by the leaf states that move the agent, so they can decide what giving up means for them.</summary>
    public interface IPathProblemHandler
    {
        void OnPathProblem(PathProblem problem);
    }

    /// <summary>
    /// Notices when the agent is meant to be going somewhere and is not getting there.
    ///
    /// <para>Nothing else did. Chase re-pathed at an unreachable target every frame, Search and
    /// Investigate waited on a remaining distance that never shrank, and the animator kept the
    /// walk or run clip ticking at its floor speed the whole time — the monster frozen in place,
    /// legs moving. This only reports; the active state chooses whether to give up, look around
    /// where it is, or wait for a way through to open.</para>
    /// </summary>
    public class MonsterPathWatchdog
    {
        // A little slack over the agent's own stopping distance before "arrived" counts.
        private const float ArriveTolerance = 0.3f;

        private readonly NavMeshAgent _agent;
        private readonly float _stuckSeconds;
        private readonly float _minProgress;

        private Vector3 _anchor;
        private float _anchorTime;
        private bool _hasAnchor;

        public MonsterPathWatchdog(NavMeshAgent agent, float stuckSeconds, float minProgress)
        {
            _agent = agent;
            _stuckSeconds = stuckSeconds;
            _minProgress = minProgress;
        }

        /// <param name="suspended">True while something else owns the agent, such as the door forcer.</param>
        public bool Tick(float now, bool suspended, out PathProblem problem)
        {
            problem = default;

            if (suspended || !WantsToMove())
            {
                _hasAnchor = false;
                return false;
            }

            if (!_hasAnchor || FlatDistance(_agent.transform.position, _anchor) >= _minProgress)
            {
                SetAnchor(now);
                return false;
            }

            // Standing at the end of a complete path is arriving, not being stuck — Chase parked on
            // a target that stopped moving looks exactly like this.
            if (HasArrivedOnCompletePath())
            {
                SetAnchor(now);
                return false;
            }

            if (now - _anchorTime < _stuckSeconds) return false;

            SetAnchor(now);

            problem = _agent.pathStatus == NavMeshPathStatus.PathComplete
                ? PathProblem.Stuck
                : PathProblem.Unreachable;

            return true;
        }

        /// <summary>One line describing the agent, for the logs.</summary>
        public string Describe()
        {
            if (!_agent.isOnNavMesh) return "fora do NavMesh";

            return $"pathStatus {_agent.pathStatus}, restante {_agent.remainingDistance:0.00}m, " +
                   $"vel {_agent.velocity.magnitude:0.00}m/s, isStopped {_agent.isStopped}, " +
                   $"destino {_agent.destination}, posicao {_agent.transform.position}";
        }

        /// <summary>
        /// A pending path still counts. Chase asks for a new one every frame, so pathPending is true
        /// most of the time; treating that as "not moving" reset the timer every frame and the
        /// watchdog never fired once in a chase — the monster stood at a cut-off doorway for over a
        /// minute without a single report.
        /// </summary>
        private bool WantsToMove()
        {
            return _agent.isOnNavMesh && !_agent.isStopped && (_agent.hasPath || _agent.pathPending);
        }

        /// <summary>
        /// Only a settled, complete path can say it has arrived. At the end of a partial path the
        /// remaining distance can read infinity while the agent stands still, which is why Search
        /// used to wait on it forever.
        /// </summary>
        private bool HasArrivedOnCompletePath()
        {
            if (_agent.pathPending || _agent.pathStatus != NavMeshPathStatus.PathComplete) return false;

            float remaining = _agent.remainingDistance;

            return !float.IsInfinity(remaining) && remaining <= _agent.stoppingDistance + ArriveTolerance;
        }

        private void SetAnchor(float now)
        {
            _anchor = _agent.transform.position;
            _anchorTime = now;
            _hasAnchor = true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
