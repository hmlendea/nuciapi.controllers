namespace NuciAPI.Controllers
{
    public class NuciApiNoneAuthorisation : NuciApiAuthorisation
    {
        internal NuciApiNoneAuthorisation() : base("None") { }

        protected override void PerformAuthorisation(string authorisationData)
        {
            // No authorisation
        }
    }
}
