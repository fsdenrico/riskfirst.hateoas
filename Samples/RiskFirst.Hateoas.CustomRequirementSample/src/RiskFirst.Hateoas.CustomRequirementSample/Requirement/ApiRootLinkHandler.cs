namespace RiskFirst.Hateoas.CustomRequirementSample.Requirement
{
    public class ApiRootLinkHandler : LinksHandler<ApiRootLinkRequirement>
    {
        protected override Task HandleRequirementAsync(LinksHandlerContext context, ApiRootLinkRequirement requirement)
        {
            var route = context.RouteMap.GetRoute("ApiRoot"); // Assumes your controller has a named route "ApiRoot".
            context.Links.Add(new LinkSpec(requirement.Id, route));
            context.Handled(requirement);
            return Task.CompletedTask;
        }
    }
}