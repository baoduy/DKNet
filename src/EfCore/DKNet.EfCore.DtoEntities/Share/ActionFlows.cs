using System.ComponentModel;

namespace DKNet.EfCore.DtoEntities.Share;

public enum ActionFlows
{
    None,

    [Description("This will return the raw payload to the Merchant. As like the payload of the QR code.")]
    Payload,

    [Description(
        "This will return a redirection URL to the Merchant. The Merchant can redirect the customer to this URL.")]
    Redirection,

    [Description(
        "This will return an URL of HTML snippet to the Merchant. The Merchant can embed this snippet in their web page.")]
    Embedded
}