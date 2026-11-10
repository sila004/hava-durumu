using System.Reflection;
using Intern.API.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Intern.API.Infrastructure;

// Staj iskeletinin örnek veri yönetimi uçları geliştirmede kalır.
public sealed class SampleControllersFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private static readonly HashSet<Type> SampleControllers =
        [typeof(AbilityController), typeof(EmployeeController), typeof(ExchangeRateController)];

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(t => SampleControllers.Contains(t.AsType())).ToArray())
            feature.Controllers.Remove(controller);
    }
}
