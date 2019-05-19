using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
namespace Nop.Plugin.Payments.Rave.Models
{
    public class ConfigurationModel : BaseNopModel
    {

        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.Live")]
        public bool Live { get; set; }
        public bool Live_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.SecretKey")]
        public string SecretKey { get; set; }
        public bool SecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.PublicKey")]
        public string PublicKey { get; set; }
        public bool PublicKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.EncryptKey")]
        public string EncryptKey { get; set; }
        public bool EncryptKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.Redirect_url")]
        public string Custom_logo { get; set; }
        public bool Custom_logo_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.Payment_method")]
        public string Payment_method { get; set; }
        public bool Payment_method_OverrideForStore { get; set; }

        #endregion
    }
}