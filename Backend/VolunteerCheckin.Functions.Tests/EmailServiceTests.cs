using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class EmailServiceTests
    {
        [TestMethod]
        public void ConstructEmailService_WithDefaults_DoesNotThrow()
        {
            EmailService svc = new("smtp", 25, "user", "pass", "from@example.com", "From");
            svc.ShouldNotBeNull();
        }
    }
}
