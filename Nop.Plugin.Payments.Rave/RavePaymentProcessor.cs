using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;


namespace Nop.Plugin.Payments.Rave
{

    /// <summary>
    /// Support for the Rave payment processor.
    /// </summary>
    public class RavePaymentProcessor : BasePlugin, IPaymentMethod
    {

        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly RavePaymentSettings _RavePaymentSettings;
        #endregion

        #region Ctor

        public RavePaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            RavePaymentSettings ravePaymentSettings)
        {
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._httpContextAccessor = httpContextAccessor;
            this._localizationService = localizationService;
            this._paymentService = paymentService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._RavePaymentSettings = ravePaymentSettings;
        }
        #endregion

        #region Utilities

        /// <summary>
        /// Gets Rave URL
        /// </summary>
        /// <returns></returns>
        private string GetRaveUrl()
        {
            return _RavePaymentSettings.Live ?
                //"https://ravesandboxapi.flutterwave.com/flwv3-pug/getpaidx/api/v2/hosted/pay" :
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


        private IDictionary<string, string> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();

            //variables
            var country = postProcessPaymentRequest.Order.ShippingAddress?.Country.ThreeLetterIsoCode;
            var currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
            var custom_description = "";
            var custom_logo = _RavePaymentSettings.Custom_logo;
            var custom_title = postProcessPaymentRequest.Order.CustomOrderNumber;
            var customer_email = postProcessPaymentRequest.Order.ShippingAddress?.Email;
            var customer_firstname = postProcessPaymentRequest.Order.ShippingAddress?.FirstName;
            var customer_lastname = postProcessPaymentRequest.Order.ShippingAddress?.LastName;
            var customer_phone = postProcessPaymentRequest.Order.ShippingAddress.PhoneNumber;
            var payment_method = _RavePaymentSettings.Payment_method;
            var txref = postProcessPaymentRequest.Order.OrderGuid.ToString();
            var redirect_url = $"{storeLocation}Plugins/PaymentRave/ReturnPaymentInfo";
            var amount = "skd";

            //create query parameters
            return new Dictionary<string, string>
            {

                //payload
                ["txref"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),
                ["PBFPubKey"] = _RavePaymentSettings.PublicKey,
                ["taxpayer_email"] = postProcessPaymentRequest.Order.ShippingAddress?.Email,
                //["amount"] = "utf-8",
                ["payment_method"] = _RavePaymentSettings.Payment_method,
                ["country"] = postProcessPaymentRequest.Order.ShippingAddress?.Country.ThreeLetterIsoCode,
                ["currency"] = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,
                ["redirect_url"] = $"{storeLocation}Plugins/PaymentRave/ReturnPaymentInfo",
                ["taxpayer_firstname"] = postProcessPaymentRequest.Order.ShippingAddress?.FirstName,
                ["taxpayer_lastname"] = postProcessPaymentRequest.Order.ShippingAddress?.LastName,
                ["taxpayer_phone"] = postProcessPaymentRequest.Order.ShippingAddress.PhoneNumber,
                ["custom_title"] = postProcessPaymentRequest.Order.CustomOrderNumber,
                ["custom_description"] = "",
                ["custom_logo"] = _RavePaymentSettings.Custom_logo,
                ["integrity_hash"] = this.GenerateSHA256String(amount, country, currency, custom_description, custom_logo,
                    custom_title, customer_email, customer_firstname, customer_lastname, customer_phone, payment_method, txref, redirect_url)
            };
        }

        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// 
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));
        }


        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new RavePaymentSettings
            {
                Live = true
            });


            //locales  
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.Live", "Live or test?");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.SecretKey", "Secret key, live or test");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.Publickey", "Public key, live or test");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.Custom_logo", "Custom logo, url");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.Encryptkey", "Encryption key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Fields.Encryptkey.Hint", "Enter encyrpt key to charge your customers.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Rave.Instructions", "<p>For plugin configuration follow these steps:<br />                   <br />                   " +
                "1. If you haven't already, create an account on rave.flutterwave.com and sign in<br /> " +
                "2. In the Settings menu (left), choose the API Keys option. " +
                "3. You will see three keys listed, a Public key, a Secret key and Encrypt Key. You will need all three.                    <em>Rave supports test keys and production keys. Use whichever pair is appropraite. There's a switch between test/sandbox.</em>                   4. Paste these keys into the configuration page of this plug-in. (All keys are required.)                    <br />                   <em>Note: If using production keys, the payment form will only work on sites hosted with HTTPS. (Test keys can be used on http sites.) If using test keys,                    use these <a href='https://developer.flutterwave.com/docs/test-cards'>test card numbers from Rave</a>.</em><br />               </p>");

            base.Install();
        }



        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<RavePaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Rave.Fields.SecretKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Rave.Fields.PublicKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Rave.Fields.EncryptKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Rave.Fields.EncryptKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Rave.Instructions");

            base.Uninstall();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        //public string PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {

            string customorderid = postProcessPaymentRequest.Order.CustomOrderNumber;            
            string url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentRave/SubmitPaymentInfo?customorderid={customorderid}";           
            _httpContextAccessor.HttpContext.Response.Redirect(url);
            ////create common query parameters for the request
            //var queryParameters = CreateQueryParameters(postProcessPaymentRequest);

            ////or add only an order total query parameters to the request
            //AddOrderTotalParameters(queryParameters, postProcessPaymentRequest);

            ////remove null values from parameters
            //queryParameters = queryParameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            //    .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

            //var url = QueryHelpers.AddQueryString(GetRaveUrl(), queryParameters);
            //_httpContextAccessor.HttpContext.Response.Redirect(url);

            ////return url;
        }
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return Convert.ToDecimal(0.0);
            //return _paymentService.CalculateAdditionalFee(cart,
            //    _RavePaymentSettings.AdditionalFee, _RavePaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentRave/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentRave";
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to Rave site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.Rave.PaymentMethodDescription"); }
        }

        #endregion
    }
}

