using Microsoft.AspNetCore.Authorization;

namespace Showroom.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string SalesRep = "SalesRep";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, SalesRep, Customer];
}

public static class Policies
{
    public const string ManageVehicles = "ManageVehicles";

    public const string DeleteVehicles = "DeleteVehicles";

    public static AuthorizationOptions AddShowroomPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ManageVehicles, p => p.RequireRole(Roles.Admin, Roles.SalesRep));
        options.AddPolicy(DeleteVehicles, p => p.RequireRole(Roles.Admin));

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        return options;
    }
}
