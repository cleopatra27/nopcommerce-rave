using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Rave.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Services.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Nop.Core.Domain.Payments;
using System;
using System.Text;
using Nop.Services.Directory;
using Nop.Core.Domain.Directory;
using System.Security.Cryptography;

namespace Nop.Plugin.Payments.Rave.Controllers
{
    public class PaymentRaveController : BasePaymentController
    {

        #region Fields
        private readonly INotificationService _notificationService;
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPermissionService _permissionService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly RavePaymentSettings _RavePaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;

        #endregion

        #region Ctor

        public PaymentRaveController(
            INotificationService notificationService,
            IWorkContext workContext,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPermissionService permissionService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            ShoppingCartSettings shoppingCartSettings,
            RavePaymentSettings ravePaymentSettings,
            ICurrencyService currencyService,
            CurrencySettings currencySettings)

        {
            this._notificationService = notificationService;
            this._workContext = workContext;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._permissionService = permissionService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._shoppingCartSettings = shoppingCartSettings;
            this._RavePaymentSettings = ravePaymentSettings;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var ravePaymentSettings = _settingService.LoadSetting<RavePaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                Live = ravePaymentSettings.Live,
                SecretKey = ravePaymentSettings.SecretKey,
                PublicKey = ravePaymentSettings.PublicKey,
                EncryptKey = ravePaymentSettings.EncryptKey

            };
            if (storeScope > 0)
            {
                model.Live_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.Live, storeScope);
                model.SecretKey_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.SecretKey, storeScope);
                model.PublicKey_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.PublicKey, storeScope);
                model.EncryptKey_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.EncryptKey, storeScope);
                }

            return View("~/Plugins/Payments.Rave/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var ravePaymentSettings = _settingService.LoadSetting<RavePaymentSettings>(storeScope);

            //save settings
            ravePaymentSettings.Live = model.Live;
            ravePaymentSettings.PublicKey = model.PublicKey;
            ravePaymentSettings.SecretKey = model.SecretKey;
            ravePaymentSettings.EncryptKey = model.EncryptKey;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.Live, model.Live_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.PublicKey, model.PublicKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.EncryptKey, model.EncryptKey_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

           _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate Rave rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.Rave.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }
        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }

        //Use this form for redirection after payment.
        //[Authorize]
        //[AutoValidateAntiforgeryToken]
        public ActionResult ReturnPaymentInfo(IFormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Rave") as RavePaymentProcessor;
            if (processor == null || !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
            {
                throw new NopException("Rave module cannot be loaded");
            }
            string tranx_id = form["Rave_tranx_id"];
            string tranx_status_code = form["Rave_tranx_status_code"];
            string tranx_status_msg = form["Rave_tranx_status_msg"];
            string Rave_tranx_amt = form["Rave_tranx_amt"];
            string Rave_tranx_curr = form["Rave_tranx_curr"];
            string Rave_cust_id = form["Rave_cust_id"];
            string Rave_gway_name = form["Rave_gway_name"];
            string Rave_echo_data = form["Rave_echo_data"];


            _logger.Information("transid: " + tranx_id);
            _logger.Information("tranx_status_code: " + tranx_status_code);
            _logger.Information("tranx_status_msg: " + tranx_status_msg);
            _logger.Information("Rave_echo_data: " + Rave_echo_data);
            _logger.Information("Rave_tranx_amt: " + Rave_tranx_amt);
            _logger.Information("Rave_tranx_curr: " + Rave_tranx_curr);

            var orderGuid = Guid.Parse(Rave_echo_data);
            Order order = _orderService.GetOrderByGuid(orderGuid);

            if (!string.Equals(tranx_status_code, "00", StringComparison.InvariantCultureIgnoreCase))
            {
                var model = new ReturnPaymentInfoModel();
                model.DescriptionText = "Your transaction was unsuccessful.";
                model.OrderId = order.Id;
                model.StatusCode = tranx_status_code;
                model.StatusMessage = tranx_status_msg;

                return View("~/Plugins/Payments.Rave/Views/ReturnPaymentInfo.cshtml", model);
            }

            order.PaymentStatus = PaymentStatus.Paid;
            _orderService.UpdateOrder(order);
            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            //return RedirectToAction("Completed", "Checkout");
        }
        [HttpGet]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        public IActionResult SubmitPaymentInfo(string customorderid)
        {

            string tranxurl = GetRaveUrl();

            Order order = _orderService.GetOrderByCustomOrderNumber(customorderid);
            var sb = new StringBuilder();
            var storeLocation = _webHelper.GetStoreLocation();
            string txref = order.OrderGuid.ToString();
            string PBFPubKey = _RavePaymentSettings.PublicKey;
            string taxpayer_email = order.ShippingAddress?.Email;             
            string payment_method = _RavePaymentSettings.Payment_method;
            string country = order.ShippingAddress?.Country.ThreeLetterIsoCode;
            string currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
            string redirect_url = $"{storeLocation}Plugins/PaymentRave/ReturnPaymentInfo";
            string taxpayer_firstname = order.ShippingAddress?.FirstName;
            string taxpayer_lastname = order.ShippingAddress?.LastName;
            string taxpayer_phone = order.ShippingAddress.PhoneNumber;          
            string custom_title = order.CustomOrderNumber;
            string custom_description = "";
            string custom_logo = _RavePaymentSettings.Custom_logo;
            string amount = "skd";


            string hashstr = this.GenerateSHA256String(amount, country, currency, custom_description, custom_logo,
custom_title, taxpayer_email, taxpayer_firstname, taxpayer_lastname, taxpayer_phone, payment_method, txref, redirect_url);
            sb.AppendLine("<html>");
            sb.AppendLine("<body onload=\"document.submit2gtpay_form.submit()\">");
            sb.AppendLine($"<form name=\"submit2gtpay_form\" action=\"{tranxurl}\" target=\"_self\" method=\"post\">");
            sb.AppendLine($"<input type=\"hidden\" name=\"txref\" value=\"{txref}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"PBFPubKey\" value=\"{PBFPubKey}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"taxpayer_email\" value=\"{taxpayer_email}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"payment_method\" value=\"{payment_method}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"country\" value=\"{country}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"currency\" value=\"{currency}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"redirect_url\" value=\"{redirect_url}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"taxpayer_firstname\" value=\"{taxpayer_firstname}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"taxpayer_lastname\" value=\"{taxpayer_lastname}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"taxpayer_phone\" value=\"{taxpayer_phone}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"custom_title\" value=\"{custom_title}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"custom_description\" value=\"{custom_description}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"custom_logo\" value=\"{custom_logo}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"integrity_hash\" value=\"{hashstr}\" />");
            sb.AppendLine("</form>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            string content = sb.ToString();
            _logger.Information(content);
            return Content(content, "text/html");

        }
        private string GetRaveUrl()
        {
            return _RavePaymentSettings.Live ?
                "https://ravesandboxapi.flutterwave.com/flwv3-pug/getpaidx/api/v2/hosted/pay" :
                "https://api.ravepay.co/flwv3-pug/getpaidx/api/v2/hosted/pay";
        }
        public string GenerateSHA256String(string Amount, string country, string currency, string custom_description, string custom_logo,
             string custom_title, string customer_email, string customer_firstname, string customer_lastname, string customer_phone,
             string payment_method, string txref, string redirect_url)
        {
            var PBFPubKey = _RavePaymentSettings.PublicKey;
            var hashh = PBFPubKey + Amount + country + currency + custom_description + custom_logo + custom_title + customer_email
                    + customer_firstname + customer_lastname + customer_phone + payment_method + txref + redirect_url;
            var key = hashh + _RavePaymentSettings.SecretKey;

            SHA256 sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            byte[] hash = sha256.ComputeHash(bytes);
            return GetStringFromHash(hash);
        }

        private static string GetStringFromHash(byte[] hash)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("X2"));
            }
            return result.ToString();
        }

        #endregion
    }
}