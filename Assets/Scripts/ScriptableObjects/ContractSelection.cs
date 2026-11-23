namespace ScriptableObjects
{
    /// <summary>
    /// The contract picked on the lobby ready board. Every peer writes it when the board's
    /// networked choice changes, so it survives the network scene load into Game, where
    /// <c>MissionManager</c> reads it in place of the contract set in the Inspector.
    /// Null means nothing was picked (Game opened straight from the Editor, tutorial, prototypes).
    /// </summary>
    public static class ContractSelection
    {
        public static ContractsSO Selected { get; set; }
    }
}
