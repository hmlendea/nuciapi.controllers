namespace NuciAPI.Controllers
{
    /// <summary>
    /// Accepts every request without performing any authorisation check.
    /// </summary>
    public sealed class NuciApiNoneAuthorisation : NuciApiAuthorisation
    {
        internal NuciApiNoneAuthorisation() : base("None")
        {
        }

        /// <inheritdoc/>
        protected override void PerformAuthorisation(string authorisationData)
        {
        }
    }
}
