using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Rave.Components
{
    [ViewComponent(Name = "PaymentRave")]
    public class PaymentRaveViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Rave/Views/PaymentInfo.cshtml");
        }
    }
}
