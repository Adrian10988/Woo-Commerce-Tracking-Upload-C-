using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WooCommerceTrackingTester
{
    class Program
    {
        static void Main(string[] args)
        {
            var trackingUploader = new WCTrackingUploader("yoursecret", "yourusername", "yoursite");
            trackingUploader.SubmitTrackingInfo("trackingid", "trackinglink", "carrier", DateTime.Now, "woocommerce order id");

            Console.ReadKey();
        }


       
    }
}
