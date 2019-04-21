using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Rave
{
    /// <summary>
    /// Represents a Rave payment settings
    /// </summary>
    public class RavePaymentSettings : ISettings
    {

        /// <summary>
        /// Gets or sets SecretKey
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Gets or sets PublicKey
        /// </summary>
        public string PublicKey { get; set; }

        /// <summary>
        /// Gets or sets an Encryptkey
        /// </summary>
        public string Encryptkey { get; set; }


        ///// <summary>
        ///// Gets or sets a Redirect_url
        ///// </summary>
        //public string Redirect_url { get; set; }


        /// <summary>
        /// Gets or sets a value for url
        /// </summary>
        public bool live { get; set; }


    }
}