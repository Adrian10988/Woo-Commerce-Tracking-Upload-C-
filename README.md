# Woo-Commerce-Tracking-Upload-C-
A quick library for uploading tracking to woocommerce using their paid tracking plugin

## Examples:

     var trackingUploader = new WCTrackingUploader("yoursecret", "yourusername", "yoursite");
     trackingUploader.SubmitTrackingInfo("trackingid", "trackinglink", "carrier", DateTime.Now,
     3065663, "woocommerce order id");  
            


The library only has been tested with HTTP and HTTPS tracking uploads. There is a method to delete uploads but has not been tested. It should be pretty simple to expand as the bulk of the complicated code is in the private methods "helpers" region. 

Reference the original docs (they're crappy I know)
https://docs.woocommerce.com/document/shipment-tracking/
