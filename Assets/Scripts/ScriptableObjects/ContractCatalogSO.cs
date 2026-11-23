using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    /// <summary>
    /// The contracts the host can pick on the lobby ready board, in the order the arrows walk them.
    /// </summary>
    [CreateAssetMenu(fileName = "ContractCatalog", menuName = "ScriptableObjects/Game/Contract Catalog")]
    public class ContractCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ContractsSO> contracts = new List<ContractsSO>();

        public int Count => contracts.Count;

        public ContractsSO Get(int index)
        {
            if (contracts.Count == 0) return null;

            return contracts[Wrap(index)];
        }

        /// <summary>Index into the list, looping both ways so the arrows never run off the end.</summary>
        public int Wrap(int index)
        {
            if (contracts.Count == 0) return 0;

            int wrapped = index % contracts.Count;
            return wrapped < 0 ? wrapped + contracts.Count : wrapped;
        }
    }
}
