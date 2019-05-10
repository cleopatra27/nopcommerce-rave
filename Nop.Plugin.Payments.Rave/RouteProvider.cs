using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Rave
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //PDT
            routeBuilder.MapRoute("Plugin.Payments.Rave.PDTHandler", "Plugins/Rave/PDTHandler",
                 new { controller = "Rave", action = "PDTHandler" });

            //IPN
            routeBuilder.MapRoute("Plugin.Payments.Rave.IPNHandler", "Plugins/Rave/IPNHandler",
                 new { controller = "Rave", action = "IPNHandler" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.Rave.CancelOrder", "Plugins/Rave/CancelOrder",
                 new { controller = "Rave", action = "CancelOrder" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority
        {
            get { return -1; }
        }
    }
}
