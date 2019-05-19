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
            //This is for return payment
            routeBuilder.MapRoute("Plugin.Payments.Rave.ReturnPaymentInfo", "Plugins/PaymentRave/ReturnPaymentInfo",
                 new { controller = "PaymentRave", action = "ReturnPaymentInfo" });
            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.Rave.CancelOrder", "Plugins/PaymentRave/CancelOrder",
                 new { controller = "PaymentRave", action = "CancelOrder" });
            //SubmitPaymentInfo
           routeBuilder.MapRoute("Plugin.Payments.Rave.SubmitPaymentInfo", "Plugins/PaymentRave/SubmitPaymentInfo",
           new { controller = "PaymentRave", action = "SubmitPaymentInfo" });
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
