using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
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

namespace Nop.Plugin.Payments.Rave.Controllers
{
    public class PaymentRaveController : BasePaymentController
    {

        #region Fields

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

        #endregion

        #region Ctor

        public PaymentRaveController(
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
            ShoppingCartSettings shoppingCartSettings)

        {
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
                //live = ravePaymentSettings.live,
                SecretKey = ravePaymentSettings.SecretKey,
                Publickey = ravePaymentSettings.PublicKey,
                EncryptKey = ravePaymentSettings.Encryptkey

            };
            //if (storeScope > 0)
            //{
            //    model.SecretKey_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.SecretKey, storeScope);
            //    model.BusinessEmail_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.BusinessEmail, storeScope);
            //    model.PdtToken_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.PdtToken, storeScope);
            //    model.PassProductNamesAndTotals_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.PassProductNamesAndTotals, storeScope);
            //    model.AdditionalFee_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.AdditionalFee, storeScope);
            //    model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(ravePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            //}

            return View("~/Plugins/Payments.PayPalStandard/Views/Configure.cshtml", model);
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
            //ravePaymentSettings.UseSandbox = model.UseSandbox;
            ravePaymentSettings.PublicKey = model.Publickey;
            ravePaymentSettings.SecretKey = model.SecretKey;
            ravePaymentSettings.Encryptkey = model.EncryptKey;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            //_settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.PublicKey, model.Publickey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ravePaymentSettings, x => x.Encryptkey, model.EncryptKey_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate PayPal rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.PayPalStandard.RoundingWarning") });

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

        #endregion
    }
}